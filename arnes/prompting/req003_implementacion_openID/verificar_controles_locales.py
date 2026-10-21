"""Aceptación HTTP local sin proveedor, sin sesión fabricada y sin acceso a MySQL.

Ejecutar después de compilar. No sustituye F02–F28 con identidades Auth0 reales.
Solo inicia y termina su propio proceso. No lee ni modifica configuración privada.
"""

import datetime
import html.parser
import http.cookiejar
import json
import os
from pathlib import Path
import socket
import ssl
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request


RAIZ = Path(__file__).resolve().parents[3]
SALIDA = RAIZ / "artifacts" / "req003"
ENTORNO = SALIDA / "entorno-local"
ENSAMBLADO = RAIZ / "SolarUY/bin/Debug/net10.0/SolarUY.dll"
BASE = "https://localhost:7178"


class SinRedireccion(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Campos(html.parser.HTMLParser):
    def __init__(self):
        super().__init__()
        self.campos = {}

    def handle_starttag(self, tag, attrs):
        datos = dict(attrs)
        if tag == "input" and "name" in datos:
            self.campos[datos["name"]] = datos.get("value", "")


def variables():
    entorno = {clave: valor for clave, valor in os.environ.items()
               if not clave.lower().startswith(("openidconnect", "autenticacion", "connectionstrings"))}
    entorno.update({
        "ASPNETCORE_ENVIRONMENT": "Production",
        "DOTNET_ENVIRONMENT": "Production",
        "ConnectionStrings__SolarUy":
            "Server=localhost;Database=solaruy_req003_sin_conexion;User ID=no-utilizado;SslMode=Required;",
        "OpenIdConnect__Authority": "https://proveedor-no-configurado.invalid/",
        "OpenIdConnect__ClientId": "control-local-sin-proveedor",
        "OpenIdConnect__ClientSecret": "valor-ficticio-no-utilizable",
        "OpenIdConnect__RolesClaimType": "https://solaruy.example/roles",
    })
    return entorno


def iniciar(entorno, salida):
    return subprocess.Popen(
        ["dotnet", str(ENSAMBLADO), "--contentRoot", str(ENTORNO), "--urls", BASE],
        cwd=RAIZ, env=entorno, stdout=salida, stderr=subprocess.STDOUT,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))


def detener(proceso):
    if proceso.poll() is None:
        proceso.terminate()
        try:
            proceso.wait(timeout=10)
        except subprocess.TimeoutExpired:
            proceso.kill()
            proceso.wait(timeout=5)


def ejecutar(resultados):
    SALIDA.mkdir(parents=True, exist_ok=True)
    ENTORNO.mkdir(exist_ok=True)
    (ENTORNO / "appsettings.json").write_text("{}", encoding="utf-8")
    if not ENSAMBLADO.exists():
        raise RuntimeError("Compilar la solución antes del recorrido HTTP.")
    with socket.socket() as puerto:
        puerto.bind(("localhost", 7178))  # Rechaza un servidor ajeno; nunca lo detiene.

    def registrar(caso, correcto):
        resultados.append({"caso": caso, "correcto": bool(correcto)})
        print(caso + (": correcto" if correcto else ": FALLÓ"), flush=True)
        if not correcto:
            raise RuntimeError("Falló el control: " + caso)

    # Sin Autenticacion:Usuarios, cada opción incompleta falla antes de servir tráfico.
    variantes = [
        ("Authority", "http://no-permitido.invalid/"),
        ("Authority", "https://usuario:valor@no-permitido.invalid/"),
        ("Authority", "https://no-permitido.invalid/#fragmento"),
        ("ClientId", ""), ("ClientSecret", ""), ("RolesClaimType", ""),
        ("ClientSecret", "<secreto-pendiente>"),
    ]
    for indice, (clave, valor) in enumerate(variantes, 1):
        entorno = variables()
        entorno["OpenIdConnect__" + clave] = valor
        proceso = iniciar(entorno, subprocess.PIPE)
        try:
            try:
                salida = proceso.communicate(timeout=40)[0]
            except subprocess.TimeoutExpired:
                detener(proceso)
                salida = proceso.communicate(timeout=5)[0]
                (SALIDA / "diagnostico-arranque.log").write_bytes(salida)
                raise RuntimeError("El arranque no terminó; revisar diagnostico-arranque.log.") from None
            texto = salida.decode("utf-8", errors="replace")
            registrar(f"opciones-invalidas-{indice}-{clave}", proceso.returncode != 0
                      and "OpenIdConnect:" + clave in texto
                      and "valor-ficticio-no-utilizable" not in texto
                      and "usuario:valor" not in texto)
        finally:
            detener(proceso)

    cookies = http.cookiejar.CookieJar()
    navegador = urllib.request.build_opener(
        urllib.request.HTTPSHandler(context=ssl.create_default_context()),
        urllib.request.HTTPCookieProcessor(cookies), SinRedireccion())

    def solicitar(ruta, datos=None):
        cuerpo = None if datos is None else urllib.parse.urlencode(datos).encode()
        peticion = urllib.request.Request(BASE + ruta, data=cuerpo)
        try:
            respuesta = navegador.open(peticion, timeout=5)
        except urllib.error.HTTPError as error:
            respuesta = error
        with respuesta:
            return respuesta.status, respuesta.headers, respuesta.read().decode("utf-8", errors="replace")

    with (SALIDA / "servidor-local.log").open("w", encoding="utf-8") as log:
        proceso = iniciar(variables(), log)
        try:
            for _ in range(40):
                if proceso.poll() is not None:
                    raise RuntimeError("El servidor de aceptación no pudo iniciar; revisar servidor-local.log.")
                try:
                    estado, _, pagina = solicitar("/cuenta/iniciar-sesion?ReturnUrl=%2Fparques-solares%2Fnuevo")
                    if estado == 200:
                        break
                except (urllib.error.URLError, OSError):
                    time.sleep(0.25)
            else:
                raise RuntimeError("El servidor no respondió por HTTPS con un certificado confiable.")

            campos = Campos()
            campos.feed(pagina)
            registrar("inicio-externo-ssr", "Iniciar sesión con proveedor externo" in pagina
                      and 'name="Password"' not in pagina and 'name="Nombre"' not in pagina
                      and 'data-enhance="false"' in pagina)
            registrar("formulario-retorno-y-antifalsificacion", campos.campos.get("ReturnUrl")
                      == "/parques-solares/nuevo" and bool(campos.campos.get("__RequestVerificationToken")))
            for ruta, datos in [("/parques-solares/nuevo", None), ("/parques-solares/nuevo", {}),
                                ("/cuenta/cerrar-sesion", {})]:
                estado, cabeceras, _ = solicitar(ruta, datos)
                registrar("anonimo-" + ruta + ("-post" if datos is not None else "-get"),
                          estado == 302 and "/cuenta/iniciar-sesion" in cabeceras.get("Location", ""))

            for error, mensaje in [("cancelado", "Se canceló"), ("proveedor", "No se pudo iniciar"),
                                   ("cierre", "La sesión de SolarUY se cerró")]:
                estado, _, texto = solicitar("/cuenta/iniciar-sesion?error=" + error + "&error_description=DETALLE_PRIVADO")
                registrar("mensaje-" + error, estado == 200 and mensaje in texto and "DETALLE_PRIVADO" not in texto)

            for indice in range(11):
                datos = {} if indice == 0 else {"__RequestVerificationToken": "invalido"}
                estado, cabeceras, _ = solicitar("/cuenta/autenticar", datos)
                registrar("antifalsificacion" if indice == 0 else f"limite-intento-{indice + 1}-http-{estado}",
                          estado == (400 if indice < 10 else 429)
                          and not cabeceras.get("Location"))

            estado, cabeceras, _ = solicitar("/signin-oidc?state=invalido&code=no-utilizable")
            registrar("callback-invalido-fuera-del-limite", estado == 302
                      and cabeceras.get("Location") == "/cuenta/iniciar-sesion?error=proveedor"
                      and "SolarUY.Sesion.Oidc=" not in " ".join(cabeceras.get_all("Set-Cookie", [])))
            registrar("sin-cookie-de-sesion", not any(cookie.name.startswith("SolarUY.Sesion") for cookie in cookies))
        finally:
            detener(proceso)

if __name__ == "__main__":
    evidencia = {
        "fecha_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "entorno": "Windows, HTTPS localhost:7178, configuración aislada ficticia, sin conexión MySQL ni Auth0",
        "alcance": "Controles locales únicamente; aceptación externa pendiente",
        "controles": [],
    }
    try:
        ejecutar(evidencia["controles"])
        evidencia["correcto"] = True
    except Exception as error:
        evidencia["correcto"] = False
        evidencia["error"] = type(error).__name__
        raise
    finally:
        SALIDA.mkdir(parents=True, exist_ok=True)
        (SALIDA / "controles-locales.json").write_text(
            json.dumps(evidencia, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Controles HTTP correctos: {len(evidencia['controles'])}. Auth0 real pendiente.")
