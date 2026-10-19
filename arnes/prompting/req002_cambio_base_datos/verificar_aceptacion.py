"""Aceptación MySQL independiente. Solo escribe en un esquema efímero nuevo identificado.

Requiere SOLARUY_ACEPTACION_CONNECTION y SOLARUY_ACEPTACION_BASE. No importa req001.
El proyecto auxiliar se construye exclusivamente dentro de artifacts/req002.
"""
from __future__ import annotations

import argparse
import base64
import concurrent.futures
import hashlib
import html
from html.parser import HTMLParser
import http.cookiejar
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import struct
import subprocess
import sys
import threading
import time
import traceback
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]
SERVER = ROOT / "SolarUY"
ARTIFACTS = ROOT / "artifacts" / "req002"
HIDDEN = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
SUCCESS = "Parque solar registrado"
DUPLICATE = "Ya existe un parque solar con ese nombre."
GENERIC = "No se pudo registrar el parque solar. Intentá nuevamente."
LENGTH = "El nombre del parque solar no puede superar los 100 caracteres."


class Formulario(HTMLParser):
    def __init__(self, pagina):
        super().__init__()
        self.hidden = {}
        self.names = {}
        self.feed(pagina)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "input" and "name" in attrs:
            if attrs.get("type") == "hidden":
                self.hidden[attrs["name"]] = attrs.get("value", "")
            if "id" in attrs:
                self.names[attrs["id"]] = attrs["name"]


class Cliente:
    def __init__(self, base):
        self.base = base
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, fields=None):
        data = None if fields is None else urllib.parse.urlencode(fields).encode()
        try:
            response = self.opener.open(self.base + path, data=data, timeout=25)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, html.unescape(response.read().decode("utf-8"))

    def login(self, nombre, password):
        code, body = self.request("/cuenta/iniciar-sesion")
        assert code == 200
        fields = Formulario(body).hidden | {"Nombre": nombre, "Password": password}
        return self.request("/cuenta/autenticar", fields)

    def crear(self, nombre, ubicacion="Montevideo", extra=None, token=True):
        code, body = self.request("/parques-solares/nuevo")
        assert code == 200
        form = Formulario(body)
        fields = form.hidden | {form.names["nombre"]: nombre, form.names["ubicacion"]: ubicacion}
        if extra:
            fields.update(extra)
        if not token:
            fields.pop("__RequestVerificationToken", None)
        return self.request("/parques-solares/nuevo", fields)


class Aceptacion:
    def __init__(self, args):
        self.args = args
        self.run_id = secrets.token_hex(6)
        self.directory = ARTIFACTS / ("ejecucion-" + self.run_id)
        self.directory.mkdir(parents=True)
        self.environment = os.environ.copy()
        local_path = SERVER / "appsettings.Local.json"
        local_options = json.loads(local_path.read_text(encoding="utf-8-sig")) if local_path.exists() else {}
        readonly = (os.environ.get("SOLARUY_ACEPTACION_READONLY_CONNECTION")
                    or local_options.get("ConnectionStrings", {}).get("SolarUyAceptacionLectura", ""))
        if readonly:
            self.environment["SOLARUY_ACEPTACION_READONLY_CONNECTION"] = readonly
        self.connection = os.environ.get("SOLARUY_ACEPTACION_CONNECTION", "")
        self.database = os.environ.get("SOLARUY_ACEPTACION_BASE", "")
        self.environment["ConnectionStrings__SolarUy"] = self.connection
        self.environment["SOLARUY_ACEPTACION_BASE"] = self.database
        self.private = self.directory / "permisos-privados.json"
        self.created = False
        self.readonly_user = None
        self.process = None
        self.thread = None
        self.results = []
        self.sensitive = [self.connection, readonly, os.environ.get("SOLARUY_ACEPTACION_ADMIN_CONNECTION", "")]
        self.base = f"http://127.0.0.1:{args.puerto}"
        self.passwords = {}
        self.helper_dll = self.directory / "auxiliar" / "bin" / "Debug" / "net10.0" / "VerificarPersistencia.dll"

    def depurar(self, text):
        for value in sorted(filter(None, self.sensitive), key=len, reverse=True):
            text = text.replace(value, "[secreto omitido]")
        text = re.sub(r"(?i)(password|pwd|user id|server|database)\s*=\s*[^;\r\n]*", r"\1=[omitido]", text)
        return text

    def command(self, command, env=None, input=None, log=None, timeout=180):
        result = subprocess.run(command, cwd=ROOT, env=env or self.environment,
                                input=input, text=True, encoding="utf-8", errors="replace",
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                creationflags=HIDDEN, timeout=timeout)
        if log:
            (self.directory / log).write_text(self.depurar(result.stdout), encoding="utf-8")
        if result.returncode:
            raise RuntimeError(f"Falló {Path(command[0]).name}; código {result.returncode}; "
                               + self.depurar(result.stdout[-1800:]))
        return result.stdout

    def helper(self, mode, *args, input=None, env=None):
        output = self.command(["dotnet", str(self.helper_dll), mode, *map(str, args)],
                              input=input, env=env)
        report = json.loads(output.splitlines()[-1])
        assert report["correcto"]
        return report["resultado"]

    def check(self, name, action):
        value = action()
        self.results.append({"caso": name, "estado": "correcto", "detalle": value})
        print(name + ": correcto", flush=True)
        self.save()
        return value

    def save(self):
        result = {"base_efimera": self.database, "casos": self.results,
                  "solo_infraestructura": self.args.solo_infraestructura,
                  "aceptacion_completa": False}
        path = self.directory / "resultados.json"
        path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        (ARTIFACTS / "ultima-ejecucion.json").write_text(
            json.dumps({"evidencia": str(path)}, ensure_ascii=False, indent=2), encoding="utf-8")

    def preparar(self):
        assert self.connection, "Falta SOLARUY_ACEPTACION_CONNECTION: no se ejecutó aceptación MySQL."
        assert re.fullmatch(r"solaruy_req002_aceptacion_[a-f0-9]{12}", self.database), "Base efímera no identificada."
        for tool in ("dotnet",):
            assert shutil.which(tool), "Falta herramienta: " + tool
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", self.args.puerto))
        auxiliary = self.directory / "auxiliar"
        auxiliary.mkdir()
        project = ("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType>"
                   "<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>"
                   "<Nullable>enable</Nullable><AssemblyName>VerificarPersistencia</AssemblyName>"
                   "</PropertyGroup><ItemGroup><ProjectReference Include=\""
                   + escape(str(SERVER / "SolarUY.csproj")) + "\" /></ItemGroup></Project>")
        (auxiliary / "VerificarPersistencia.csproj").write_text(project, encoding="utf-8")
        shutil.copyfile(Path(__file__).with_name("verificar_persistencia.cs"), auxiliary / "Program.cs")
        self.command(["dotnet", "build", str(auxiliary / "VerificarPersistencia.csproj")], log="auxiliar-build.log")
        self.check("I34-configuracion-sintaxis-prioridades", lambda: self.helper("configuracion"))
        self.check("P07-esquema-efimero-nuevo", lambda: self.helper("preparar"))
        self.created = True
        (self.directory / "recursos-creados.json").write_text(json.dumps({"base": self.database}), encoding="utf-8")
        self.migrar("migracion-primera.log")
        self.check("I29-esquema-efectivo", lambda: self.helper("esquema"))
        self.check("I35-I36-I37-repositorio-real", lambda: self.helper("repositorio"))
        before = self.helper("conteo")
        self.migrar("migracion-segunda.log")
        assert self.helper("conteo") == before, "La segunda migración alteró los registros."
        self.check("I30-actualizacion-repetible", lambda: {"registros_antes_y_despues": before})
        try:
            self.check("I37-permisos-reales", lambda: self.helper("permisos", self.private))
            private = json.loads(self.private.read_text(encoding="utf-8"))
            self.readonly_user = private["usuario"]
            self.sensitive.append(private["conexion"])
        except RuntimeError as error:
            if not any(f'"codigo":{code}' in str(error) for code in (1044, 1142, 1227)):
                raise
            self.results.append({"caso": "I37-permisos-reales", "estado": "pendiente",
                                 "detalle": ("La cuenta de lectura requiere SELECT sobre el esquema efímero."
                                             if self.environment.get("SOLARUY_ACEPTACION_READONLY_CONNECTION")
                                             else "Se requiere una cuenta con permisos para crear usuarios y otorgar SELECT.")})
            self.save()
        self.check("I31-I32-I33-aplicacion-y-fabrica", self.configuration_consumers)

    def migrar(self, log, env=None):
        self.command(["dotnet", "ef", "database", "update", "--no-build", "--project",
                      "SolarUY/SolarUY.csproj", "--startup-project", "SolarUY/SolarUY.csproj"], log=log, env=env)

    def identities(self):
        for i, rol in enumerate(("Administrador", "Usuario", "administrador")):
            nombre = f"req002-{self.run_id}-{i}"
            password = secrets.token_urlsafe(24)
            self.passwords[rol] = (nombre, password)
            self.sensitive.append(password)
            salt = secrets.token_bytes(16)
            derived = hashlib.pbkdf2_hmac("sha512", password.encode(), salt, 100000, 32)
            hashed = base64.b64encode(b"\x01" + struct.pack(">III", 2, 100000, 16) + salt + derived).decode()
            self.sensitive.append(hashed)
            prefix = f"Autenticacion__Usuarios__{i}__"
            self.environment[prefix + "Nombre"] = nombre
            self.environment[prefix + "PasswordHash"] = hashed
            self.environment[prefix + "Roles__0"] = rol
        # Solo se preservan si se pide un recorrido manual con el servidor activo.
        if self.args.mantener_servidor:
            (self.directory / "identidades-privadas.json").write_text(
                json.dumps(self.passwords), encoding="utf-8")

    def start(self, connection=None, source="env", variable_connection=None, manual=False):
        environment = self.environment.copy()
        environment["ASPNETCORE_ENVIRONMENT"] = "Development"
        environment["DOTNET_ENVIRONMENT"] = "Development"
        if source != "argumentos":
            environment["ConnectionStrings__SolarUy"] = connection or self.connection
        arguments = []
        if source == "local":
            environment.pop("ConnectionStrings__SolarUy", None)
        elif source == "argumentos":
            if variable_connection:
                environment["ConnectionStrings__SolarUy"] = variable_connection
            arguments = ["--ConnectionStrings:SolarUy", connection or self.connection]
        manual_log = (self.directory / "servidor-manual.log").open("w", encoding="utf-8") if manual else None
        self.process = subprocess.Popen(
            ["dotnet", str(SERVER / "bin/Debug/net10.0/SolarUY.dll"), "--urls", self.base,
             "--environment", "Development", "--contentRoot", str(SERVER), *arguments],
            cwd=ROOT, env=environment, stdout=manual_log or subprocess.PIPE, stderr=subprocess.STDOUT,
            encoding="utf-8", errors="replace", creationflags=HIDDEN)
        process = self.process
        log_path = self.directory / f"servidor-{process.pid}.log"

        def log():
            with log_path.open("w", encoding="utf-8") as output:
                for line in process.stdout:
                    output.write(self.depurar(line))
                    output.flush()
        if manual_log:
            manual_log.close()
            self.thread = None
        else:
            self.thread = threading.Thread(target=log, daemon=True)
            self.thread.start()
        for _ in range(100):
            if self.process.poll() is not None:
                raise RuntimeError("El servidor terminó antes de estar disponible; revisar el log depurado.")
            try:
                if Cliente(self.base).request("/cuenta/iniciar-sesion")[0] == 200:
                    return
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(.2)
        raise RuntimeError("El servidor no estuvo disponible dentro del plazo.")

    def configuration_consumers(self):
        self.identities()
        path = SERVER / "appsettings.Local.json"
        original = path.read_bytes() if path.exists() else None
        written = None
        secondary_env = None
        secondary_file = self.directory / "conexion-secundaria-privada.json"

        def local(connection):
            nonlocal written
            written = json.dumps({"ConnectionStrings": {"SolarUy": connection}}).encode()
            path.write_bytes(written)

        try:
            local(self.connection)
            local_env = self.environment.copy()
            local_env.pop("ConnectionStrings__SolarUy", None)
            result = self.helper("fabrica", env=local_env)
            assert result["baseConectada"] == self.database
            self.start(source="local")
            self.assert_success(self.admin(), "Config local " + self.run_id)
            self.stop()
            self.helper("secundario", secondary_file)
            secondary = json.loads(secondary_file.read_text(encoding="utf-8"))
            (self.directory / "recursos-creados.json").write_text(json.dumps({
                "base": self.database, "base_secundaria": secondary["baseDatos"]}), encoding="utf-8")
            self.sensitive.append(secondary["conexion"])
            secondary_env = self.environment.copy()
            secondary_env["ConnectionStrings__SolarUy"] = secondary["conexion"]
            secondary_env["SOLARUY_ACEPTACION_BASE"] = secondary["baseDatos"]
            self.migrar("migracion-secundaria.log", env=secondary_env)
            local(secondary["conexion"])
            result = self.helper("fabrica")
            assert result["baseConectada"] == self.database
            self.start()
            self.assert_success(self.admin(), "Config entorno " + self.run_id)
            self.stop()
            # La conexión válida por argumentos sobrescribe una variable inválida.
            arg_env = self.environment.copy()
            arg_env["ConnectionStrings__SolarUy"] = secondary["conexion"]
            result = self.helper("fabrica", "--environment", "Development",
                                 "--ConnectionStrings:SolarUy", self.connection, env=arg_env)
            assert result["baseConectada"] == self.database
            self.start(self.connection, source="argumentos", variable_connection=secondary["conexion"])
            self.assert_success(self.admin(), "Config argumentos " + self.run_id)
            self.stop()
            assert self.helper("conteo", env=secondary_env) == 0, "Se escribió en el destino de menor prioridad."
            return {"local": True, "entorno": True, "argumentos": True, "entornoDevelopment": True,
                    "segundoEsquemaSinEscrituras": secondary["baseDatos"]}
        finally:
            self.stop()
            if written is not None:
                assert path.read_bytes() == written, "La configuración local cambió concurrentemente; no se sobrescribió."
                if original is None:
                    path.unlink()
                else:
                    path.write_bytes(original)
            if secondary_env:
                self.helper("limpiar", env=secondary_env)
                secondary_file.unlink()

    def stop(self):
        if self.process and self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=10)
        if self.thread:
            self.thread.join(timeout=5)
        self.process = None

    def admin(self):
        client = Cliente(self.base)
        code, body = client.login(*self.passwords["Administrador"])
        assert code == 200 and "Registrar parque solar" in body
        return client

    def rows(self, name):
        return self.helper("consulta", input=name)

    def assert_success(self, client, name, location="Montevideo", extra=None):
        code, body = client.crear(name, location, extra)
        assert code == 200 and SUCCESS in body, "No se confirmó el alta de " + name
        assert "0 MW" in body
        rows = self.rows(name)
        assert len(rows) == 1, "La consulta no devolvió exactamente el parque recién creado: " + name
        row = rows[0]
        assert row["Nombre"] == unicodedata.normalize("NFC", name.strip()), "El texto visible difiere de NFC."
        assert row["Ubicacion"] == location.strip()
        assert row["ID"] in body and row["Nombre"] in body and row["Ubicacion"] in body, "La confirmación difiere de los datos persistidos."
        self.assert_public(body)
        return row

    def assert_public(self, body):
        for value in filter(None, self.sensitive):
            assert value not in body, "Se expuso un secreto."
        for value in ("MySqlException", "DbUpdateException", "INSERT INTO", "SELECT `", "StackTrace", "ConnectionStrings", "Password="):
            assert value not in body, "Se expuso un detalle técnico."

    def functional(self):
        self.identities()
        self.start()
        admin = self.admin()
        name = "HTTP " + self.run_id
        self.check("F06-alta-HTTP-y-consulta-MySQL", lambda: self.assert_success(admin, name, "  Salto  "))
        self.stop()
        self.start()
        admin = self.admin()
        code, body = admin.crear("  " + name.lower() + "  ")
        assert code == 200 and DUPLICATE in body and len(self.rows(name)) == 1
        self.check("F07-persistencia-tras-reinicio", lambda: True)
        accent = "José " + self.run_id
        self.assert_success(admin, accent)
        assert DUPLICATE in admin.crear("  Jose\u0301 " + self.run_id + "  ")[1], "No se detectó la equivalencia Form C."
        self.assert_success(admin, "Jose " + self.run_id)
        self.assert_success(admin, "Parque Solar " + self.run_id)
        self.assert_success(admin, "Parque  Solar " + self.run_id)
        self.check("F08-equivalencias-y-diferencias", lambda: True)
        for valid, invalid in (("a" * 100, "a" * 101), ("🌞" * 100, "🌞" * 101),
                               ("e\u0301" * 100, "e\u0301" * 101)):
            self.assert_success(admin, valid)
            count = self.helper("conteo")
            code, body = admin.crear(invalid)
            assert code == 200 and LENGTH in body and SUCCESS not in body
            assert self.helper("conteo") == count
            # El control conserva el texto original; HTMLParser ya decodifica entidades.
            assert html.escape(invalid, quote=True) in html.escape(body, quote=True) or invalid in body
        self.check("F09-limites-Unicode-sin-truncamiento", lambda: True)
        clients = [self.admin(), self.admin()]
        concurrent_name = "Concurrente " + self.run_id
        gate = threading.Barrier(2)

        def concurrent_create(client):
            gate.wait(timeout=10)
            return client.crear(concurrent_name)
        with concurrent.futures.ThreadPoolExecutor(2) as executor:
            responses = list(executor.map(concurrent_create, clients))
        assert sum(SUCCESS in body for _, body in responses) == 1
        assert sum(DUPLICATE in body for _, body in responses) == 1
        assert len(self.rows(concurrent_name)) == 1
        self.check("F10-concurrencia-HTTP-e-indice", lambda: True)
        count = self.helper("conteo")
        for invalid_name, invalid_location, message in (("", "Salto", "El nombre es obligatorio."),
                                                        ("Válido", "  ", "La ubicación es obligatoria.")):
            code, body = admin.crear(invalid_name, invalid_location)
            assert code == 200 and message in body and SUCCESS not in body
        assert count == self.helper("conteo")
        forced_id = "11111111-1111-1111-1111-111111111111"
        row = self.assert_success(admin, "Manipulado " + self.run_id, extra={
            "Modelo.ID": forced_id, "Modelo.CapacidadTotalEstimadaMw": "999",
            "Modelo.Nodos[0].Nombre": "intruso"})
        assert row["ID"] != forced_id
        self.check("F11-obligatorios-y-formulario-manipulado", lambda: True)
        count = self.helper("conteo")
        _, page = admin.request("/parques-solares/nuevo")
        token_fields = Formulario(page).hidden | {"Modelo.Nombre": "Intruso", "Modelo.Ubicacion": "Salto"}
        anon = Cliente(self.base)
        assert "Registrar parque solar" not in anon.request("/parques-solares/nuevo")[1]
        assert SUCCESS not in anon.request("/parques-solares/nuevo", token_fields)[1]
        for role in ("Usuario", "administrador"):
            client = Cliente(self.base)
            client.login(*self.passwords[role])
            assert "No tenés permiso" in client.request("/parques-solares/nuevo")[1]
            assert SUCCESS not in client.request("/parques-solares/nuevo", token_fields)[1]
        code, body = admin.crear("Sin token " + self.run_id, token=False)
        assert code == 400 and SUCCESS not in body and count == self.helper("conteo")
        self.check("F12-autorizacion-exacta-y-antifalsificacion", lambda: True)
        self.assert_public(page)
        for path in ("/appsettings.Local.json", "/appsettings.Local.example.json"):
            code, body = anon.request(path)
            assert code == 404
            self.assert_public(body)
        self.check("F14-respuestas-y-archivos-privados", lambda: True)
        self.stop()
        failures = [(re.sub(r"(?i)port\s*=\s*\d+", "Port=1", self.connection)
                     + ";Connection Timeout=2;", "conexion")]
        if self.private.exists():
            private = json.loads(self.private.read_text(encoding="utf-8"))
            failures.append((private["conexion"], "permisos"))
        for connection, suffix in failures:
            self.start(connection)
            client = self.admin()
            name = "Error " + suffix + self.run_id
            count = self.helper("conteo")
            code, body = client.crear(name)
            assert code == 200 and GENERIC in body and SUCCESS not in body and DUPLICATE not in body
            assert name in body and self.helper("conteo") == count
            self.assert_public(body)
            self.stop()
        self.check("F13-indisponibilidad", lambda: True)
        self.helper("fallo-escritura", "activar")
        try:
            self.start()
            client = self.admin()
            count = self.helper("conteo")
            code, body = client.crear("Fallo escritura " + self.run_id)
            assert code == 200 and GENERIC in body and SUCCESS not in body and DUPLICATE not in body
            assert self.helper("conteo") == count
            self.assert_public(body)
        finally:
            self.stop()
            self.helper("fallo-escritura", "desactivar")
        self.check("F13-fallo-de-escritura-sin-alta-parcial", lambda: True)
        self.check("F14-evidencias-depuradas", self.review_logs)
        if self.args.mantener_servidor:
            self.start(manual=True)
            (self.directory / "servidor-manual.json").write_text(json.dumps({
                "pid": self.process.pid, "url": self.base, "base": self.database,
                "nota": "F15 pendiente: recorrido manual con teclado; recursos preservados."}), encoding="utf-8")

    def review_logs(self):
        for path in self.directory.glob("servidor-*.log"):
            content = path.read_text(encoding="utf-8")
            for value in filter(None, self.sensitive):
                assert value not in content, "Se registró un secreto."
            for value in ("INSERT INTO", "SELECT `", "MySql.Data.MySqlClient.MySqlException", "StackTrace", "Password="):
                assert value not in content, "Se registraron SQL o excepciones completas."
        return True

    def finish(self, success):
        keep = success and self.args.mantener_servidor and not self.args.solo_infraestructura
        if keep:
            self.results.append({"caso": "F15-teclado", "estado": "pendiente", "detalle": "Servidor preservado para revisión manual."})
            print("Servidor y esquema preservados para revisión con teclado: " + self.base)
        else:
            self.stop()
            if self.created:
                args = [self.readonly_user] if self.readonly_user else []
                self.check("F16-limpieza-exclusiva-recursos-creados", lambda: self.helper("limpiar", *args))
            for name in ("permisos-privados.json", "identidades-privadas.json"):
                private_file = self.directory / name
                if private_file.exists():
                    private_file.unlink()
        self.save()


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--puerto", type=int, default=5285)
    parser.add_argument("--solo-infraestructura", action="store_true")
    parser.add_argument("--mantener-servidor", action="store_true")
    args = parser.parse_args()
    acceptance = Aceptacion(args)
    success = False
    try:
        acceptance.preparar()
        if not args.solo_infraestructura:
            acceptance.functional()
        success = True
    except Exception as error:
        frame = traceback.extract_tb(error.__traceback__)[-1]
        detail = acceptance.depurar(f"{type(error).__name__}: {error}; {Path(frame.filename).name}:{frame.lineno}")
        acceptance.results.append({"caso": "ejecucion", "estado": "fallo", "detalle": detail})
        print("Aceptación pendiente o fallida: " + detail, file=sys.stderr)
    finally:
        try:
            acceptance.finish(success)
        except Exception as error:
            success = False
            acceptance.results.append({"caso": "limpieza", "estado": "fallo", "detalle": acceptance.depurar(str(error))})
            acceptance.save()
    pending = any(result["estado"] == "pendiente" for result in acceptance.results)
    return 0 if success and not pending else 1


if __name__ == "__main__":
    sys.exit(main())
