"""Verificación funcional HTTP con servidor y SQLite reales, sin pruebas unitarias externas."""
import argparse
import concurrent.futures
import copy
import html
from html.parser import HTMLParser
import http.cookiejar
import json
import os
from pathlib import Path
import secrets
import sqlite3
import subprocess
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

from generar_hash import generar_hash

ROOT = next(parent for parent in Path(__file__).resolve().parents if (parent / "SolarUY.slnx").is_file())
BASE = "http://127.0.0.1:5284"


class Formularios(HTMLParser):
    def __init__(self, documento):
        super().__init__()
        self.forms = []
        self.current = None
        self.feed(documento)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "form":
            self.current = {"action": attrs.get("action", ""), "inputs": {}}
            self.forms.append(self.current)
        elif tag == "input" and self.current is not None and attrs.get("name"):
            self.current["inputs"][attrs["name"]] = attrs.get("value", "")

    def handle_endtag(self, tag):
        if tag == "form":
            self.current = None


class SinRedireccion(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Cliente:
    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookies), SinRedireccion())

    def request(self, ruta, datos=None):
        data = None if datos is None else urllib.parse.urlencode(datos).encode()
        request = urllib.request.Request(BASE + ruta, data=data)
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.status, html.unescape(response.read().decode()), dict(response.headers)

    def login(self, usuario):
        status, body, _ = self.request("/cuenta/iniciar-sesion")
        assert status == 200, (status, body[:500])
        form = next(form for form in Formularios(body).forms
                    if form["action"] == "/cuenta/autenticar")
        datos = dict(form["inputs"], Nombre=usuario["Nombre"], Password=usuario["Password"])
        status, body, headers = self.request("/cuenta/autenticar", datos)
        assert status == 302 and headers["Location"] == "/parques-solares/nuevo", (status, body[:500])

    def formulario(self):
        status, body, _ = self.request("/parques-solares/nuevo")
        assert status == 200, (status, body[:500])
        form = next(form for form in Formularios(body).forms
                    if form["inputs"].get("_handler") == "alta-parque-solar")
        return body, form["inputs"]

    def alta(self, nombre, ubicacion, extras=None, datos=None):
        if datos is None:
            _, datos = self.formulario()
        datos = dict(datos)
        nombre_key = next(key for key in datos if key.endswith(".Nombre"))
        ubicacion_key = next(key for key in datos if key.endswith(".Ubicacion"))
        datos[nombre_key] = nombre
        datos[ubicacion_key] = ubicacion
        datos.update(extras or {})
        return self.request("/parques-solares/nuevo", datos)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mantener-servidor", action="store_true")
    args = parser.parse_args()
    salida = ROOT / "artifacts" / "req001" / ("aceptacion-" + uuid.uuid4().hex[:10])
    salida.mkdir(parents=True)
    base = salida / "solaruy.db"
    entorno = os.environ.copy()
    entorno["ASPNETCORE_ENVIRONMENT"] = "Development"
    entorno["ConnectionStrings__SolarUy"] = "Data Source=" + str(base)
    usuarios = []
    for index, (nombre, rol) in enumerate([
            ("administrador-aceptacion", "Administrador"),
            ("usuario-aceptacion", "Usuario"), ("rol-minusculas", "administrador")]):
        password = secrets.token_urlsafe(24)
        usuario = {"Nombre": nombre, "Password": password, "PasswordHash": generar_hash(password), "Roles": [rol]}
        usuarios.append(usuario)
        entorno[f"Autenticacion__Usuarios__{index}__Nombre"] = nombre
        entorno[f"Autenticacion__Usuarios__{index}__PasswordHash"] = usuario["PasswordHash"]
        entorno[f"Autenticacion__Usuarios__{index}__Roles__0"] = rol
    (salida / "identidades-efimeras.json").write_text(json.dumps(usuarios), encoding="utf8")
    subprocess.run(["dotnet", "ef", "database", "update", "--no-build", "--project",
                    "SolarUY/SolarUY.csproj", "--startup-project", "SolarUY/SolarUY.csproj"],
                   cwd=ROOT, env=entorno, check=True, stdout=subprocess.DEVNULL)
    log_path = salida / "servidor.log"
    log = log_path.open("w", encoding="utf8")
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    proceso = subprocess.Popen(["dotnet", str(ROOT / "SolarUY/bin/Debug/net10.0/SolarUY.dll"),
                                "--urls", BASE], cwd=ROOT / "SolarUY", env=entorno,
                               stdout=log, stderr=subprocess.STDOUT, creationflags=flags)
    resultados = []
    completo = False

    def registrar(ids, evidencia):
        resultados.append({"tareas": ids, "resultado": "correcto", "evidencia": evidencia})
        print(ids + ": " + evidencia, flush=True)

    def contar():
        with sqlite3.connect(base) as db:
            return db.execute("SELECT COUNT(*) FROM ParquesSolares").fetchone()[0]

    try:
        for _ in range(100):
            if proceso.poll() is not None:
                raise RuntimeError(log_path.read_text(encoding="utf8"))
            try:
                if Cliente().request("/")[0] == 200:
                    break
            except urllib.error.URLError:
                time.sleep(.1)
        else:
            raise RuntimeError("El servidor no inició a tiempo.")

        anonimo = Cliente()
        for metodo in [None, {"_handler": "alta-parque-solar", "Modelo.Nombre": "Intruso", "Modelo.Ubicacion": "Salto"}]:
            status, body, headers = anonimo.request("/parques-solares/nuevo", metodo)
            assert status == 302 and "/cuenta/iniciar-sesion" in headers["Location"], (status, body[:300])
        assert contar() == 0
        registrar("F12 F13", "GET y POST anónimos rechazados; cero escrituras.")

        admin = Cliente()
        admin.login(usuarios[0])
        documento, inputs = admin.formulario()
        assert "Registrar parque solar" in documento
        assert len([key for key in inputs if key.startswith("Modelo.")]) == 2
        assert "0 MW" in documento
        status, body, _ = admin.alta("  Parque Sur  ", "  Montevideo  ")
        assert status == 200 and "Parque solar registrado" in body, (status, body[-3000:])
        with sqlite3.connect(base) as db:
            row = db.execute("SELECT ID, Nombre, ClaveNombre, Ubicacion FROM ParquesSolares").fetchone()
            assert uuid.UUID(row[0]).int > 0
            assert row[1:] == ("Parque Sur", "PARQUE SUR", "Montevideo"), row
            assert str(uuid.UUID(row[0])) in body.lower()
            assert "Capacidad" not in [col[1] for col in db.execute("PRAGMA table_info(ParquesSolares)")]
            assert not any(term in name.lower() for (name,) in db.execute(
                "SELECT name FROM sqlite_master WHERE type='table'") for term in ["nodo", "inversor", "panel"])
        assert "0 MW" in body
        registrar("F01 F02 F06 F18", "Alta confirmada, GUID generado, datos limpios, 0 MW y sin topología.")

        status, body, _ = admin.alta("Parque Norte", "Salto")
        assert status == 200 and "Parque solar registrado" in body
        with sqlite3.connect(base) as db:
            assert db.execute("SELECT COUNT(DISTINCT ID) FROM ParquesSolares").fetchone()[0] == 2
        registrar("F03", "Dos nombres distintos reciben IDs distintos.")

        for nombre, ubicacion, mensaje in [("", "Salto", "El nombre es obligatorio."),
                ("   ", "Salto", "El nombre es obligatorio."), ("Inválido", "", "La ubicación es obligatoria."),
                ("Inválido", "   ", "La ubicación es obligatoria.")]:
            antes = contar()
            status, body, _ = admin.alta(nombre, ubicacion)
            assert status == 200 and mensaje in body and contar() == antes
            assert "Parque solar registrado" not in body
            form = next(form for form in Formularios(body).forms if form["inputs"].get("_handler") == "alta-parque-solar")
            assert form["inputs"]["Modelo.Nombre"] == nombre
            assert form["inputs"]["Modelo.Ubicacion"] == ubicacion
        registrar("F04 F05", "Nombre y ubicación vacíos o en blanco rechazados sin escrituras.")

        for nombre in ["Parque Sur", " parque sur "]:
            antes = contar()
            status, body, _ = admin.alta(nombre, "Ubicación conservada")
            assert status == 200 and "Ya existe un parque solar con ese nombre." in body and contar() == antes
            form = next(form for form in Formularios(body).forms if form["inputs"].get("_handler") == "alta-parque-solar")
            assert form["inputs"]["Modelo.Nombre"] == nombre
            assert form["inputs"]["Modelo.Ubicacion"] == "Ubicación conservada"
        registrar("F07 F08 F21", "Duplicados exactos y equivalentes rechazados; entradas conservadas.")

        assert "Parque solar registrado" in admin.alta("Parque José", "Salto")[1]
        antes = contar()
        assert "Ya existe un parque solar con ese nombre." in admin.alta("Parque Jose\u0301", "Salto")[1]
        assert contar() == antes
        for nombre in ["Parque Jose", "Parque  Sur"]:
            assert "Parque solar registrado" in admin.alta(nombre, "Salto")[1]
        registrar("F09 F10", "Unicode canónico duplicado; acentos y espacios interiores siguen siendo distintos.")

        barrera = threading.Barrier(2)
        clientes = [Cliente(), Cliente()]
        for cliente in clientes:
            for cookie in admin.cookies:
                cliente.cookies.set_cookie(copy.copy(cookie))
        forms = [cliente.formulario()[1] for cliente in clientes]

        def concurrir(index):
            barrera.wait(timeout=10)
            return clientes[index].alta(["Concurrente", " concurrente "][index], "Salto", datos=forms[index])

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            respuestas = list(pool.map(concurrir, range(2)))
        assert all(respuesta[0] == 200 for respuesta in respuestas)
        assert sum("Parque solar registrado" in respuesta[1] for respuesta in respuestas) == 1
        assert sum("Ya existe un parque solar con ese nombre." in respuesta[1] for respuesta in respuestas) == 1
        with sqlite3.connect(base) as db:
            assert db.execute("SELECT COUNT(*) FROM ParquesSolares WHERE ClaveNombre='CONCURRENTE'").fetchone()[0] == 1
        registrar("F11", "Dos POST concurrentes: exactamente un parque y un conflicto.")

        for usuario in usuarios[1:]:
            cliente = Cliente()
            cliente.login(usuario)
            antes = contar()
            for metodo in [None, dict(inputs, **{"Modelo.Nombre": "Prohibido", "Modelo.Rol": "Administrador"})]:
                status, body, headers = cliente.request("/parques-solares/nuevo", metodo)
                assert status == 302 and "/cuenta/acceso-denegado" in headers["Location"], (status, body[:300])
            assert contar() == antes
            assert "Acceso denegado" in cliente.request("/cuenta/acceso-denegado")[1]
        registrar("F14 F15", "GET/POST sin rol exacto denegados, incluido administrador en minúsculas.")

        falso_id = str(uuid.uuid4())
        status, body, _ = admin.alta("Datos manipulados", "Salto", {
            "Modelo.ID": falso_id, "Modelo.CapacidadTotalEstimadaMw": "999", "Modelo.Rol": "Usuario",
            "Modelo.Usuario": "otro", "Rol": "Administrador", "ID": falso_id})
        assert status == 200 and "Parque solar registrado" in body and "0 MW" in body
        with sqlite3.connect(base) as db:
            assert db.execute("SELECT ID FROM ParquesSolares WHERE ClaveNombre='DATOS MANIPULADOS'").fetchone()[0].lower() != falso_id
        registrar("F16", "ID, capacidad, identidad y rol manipulados no cambian los datos ni los permisos.")

        antes = contar()
        datos_sin_token = dict(inputs)
        datos_sin_token.pop("__RequestVerificationToken")
        assert admin.alta("Sin token", "Salto", datos=datos_sin_token)[0] == 400
        assert contar() == antes
        registrar("F17", "POST sin token antifalsificación rechazado con 400 y sin escrituras.")

        with sqlite3.connect(base) as db:
            db.execute("CREATE TRIGGER BloquearAltaAceptacion BEFORE INSERT ON ParquesSolares "
                       "WHEN NEW.ClaveNombre='FALLO CONTROLADO' BEGIN SELECT RAISE(ABORT,'fallo tecnico de aceptacion'); END")
        antes = contar()
        try:
            status, body, _ = admin.alta("Fallo controlado", "Salto")
            assert status == 200 and "No se pudo registrar el parque solar. Intentá nuevamente." in body
            assert "Parque solar registrado" not in body and "Ya existe un parque solar" not in body
            assert "SqliteException" not in body and "fallo tecnico de aceptacion" not in body
            assert contar() == antes
            assert "Error al registrar un parque solar." in log_path.read_text(encoding="utf8")
        finally:
            with sqlite3.connect(base) as db:
                db.execute("DROP TRIGGER BloquearAltaAceptacion")
        registrar("F19 F20", "Fallo real de SQLite: mensaje genérico, sin éxito/duplicado falso; detalle en servidor.")

        documento, clean = admin.formulario()
        assert clean["Modelo.Nombre"] == "" and clean["Modelo.Ubicacion"] == ""
        assert "Parque solar registrado" in admin.alta("Otro parque", "Salto")[1]
        registrar("F22", "Nuevo GET presenta formulario limpio y permite otra alta independiente.")
        (salida / "resultado.json").write_text(json.dumps(resultados, ensure_ascii=False, indent=2), encoding="utf8")
        (ROOT / "artifacts/req001/ultima-aceptacion.json").write_text(json.dumps({
            "directorio": str(salida), "pid": proceso.pid, "url": BASE}), encoding="utf8")
        print("Evidencia: " + str(salida), flush=True)
        print("Verificaciones HTTP completas.", flush=True)
        completo = True
    finally:
        if not args.mantener_servidor or not completo:
            proceso.terminate()
            proceso.wait(timeout=15)
        log.close()


if __name__ == "__main__":
    main()
