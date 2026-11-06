# req008 - Configuración y verificación del balanceador

Todos los comandos se ejecutan desde la raíz de SolarUY, junto a `SolarUY.slnx`, usando PowerShell 7. Se necesitan Docker Desktop con contenedores Linux, Docker Compose y .NET 10. Las pruebas de navegador requieren Node.js y Playwright, con Microsoft Edge instalado o el navegador configurado mediante `SOLARUY_BROWSER`.

## Ejecución habitual con la base actual

El Compose de la raíz levanta Nginx, `app1` y `app2`. Ambas aplicaciones montan `SolarUY/appsettings.Local.json` en solo lectura y utilizan la base actual, sus credenciales y la configuración OIDC existente. No se crea otro MySQL, no se copian los datos ni se ejecutan migraciones al iniciar. Si existen migraciones pendientes, aplicarlas una sola vez como operación previa; req008 no introduce ninguna.

Cuando la conexión utiliza loopback, las aplicaciones sustituyen esa dirección por `host.docker.internal` solo en memoria. Se conserva el mismo MySQL, puerto, base, usuario y contraseña. MySQL debe admitir la conexión procedente de Docker Desktop; comprobar la escucha, el firewall y los permisos del usuario sin publicar el servicio innecesariamente. El archivo original permanece intacto.

Preparar una sola vez los certificados privados:

```powershell
./arnes/prompting/tests/req008/preparar-certificados.ps1
```

Se crean archivos en `.solaruy-private/`, excluida de Git y del contexto Docker. El script conserva los certificados existentes: no rotarlos ni borrar el volumen de claves de una instalación en uso. Para usar el navegador sin advertencias, importar explícitamente el certificado público `.solaruy-private/localhost.crt` en el almacén de confianza del usuario. Nunca importar ni compartir `localhost.key`, `claves.pfx` o `claves-password.txt`. El directorio restringe sus permisos al usuario que lo prepara; en Linux, los montajes privados también deben ser legibles por el usuario `app` del contenedor (UID 1654).

En Windows, si ya existe un certificado HTTPS de desarrollo de .NET confiable para el usuario, puede exportarse para Nginx sin instalar certificados nuevos ni modificar el almacén de confianza:

```powershell
./arnes/prompting/tests/req008/preparar-certificados.ps1 -UsarCertificadoDesarrolloConfiable
```

Esta opción sustituye solo los dos archivos TLS, conservando el certificado de Data Protection y el anillo compartido. Si Nginx ya estaba iniciado, validar con `docker compose exec nginx nginx -t` y recargar con `docker compose exec nginx nginx -s reload`.

El acceso predeterminado es `https://localhost:8080`. Si el puerto está ocupado, definir `SOLARUY_PUERTO=8443` en un archivo `.env` local de la raíz; `.env.example` muestra las opciones públicas disponibles. Ajustar también los puertos de diagnóstico o la subred si se superponen con otros servicios. Nginx debe mantener la IP indicada por `SOLARUY_PROXY_IP`, que también usan las aplicaciones como proxy confiable.

```powershell
docker compose build
docker compose up -d
docker compose ps
docker compose logs -ft app1 app2 nginx
```

Abrir el origen HTTPS elegido. Los puertos HTTP 4000 y 4001 están ligados a loopback y permiten consultar `/health/ready`; son de diagnóstico. La navegación autenticada se hace a través de Nginx. El endpoint de salud devuelve `Disponible` o `No disponible` sin detalles y está bloqueado en el acceso público del balanceador.

La imagen se construye con los componentes Razor presentes durante la restauración, necesarios para que .NET 10 publique los recursos de Blazor. Los archivos privados se montan al ejecutar y no se copian a la imagen. El volumen `claves` conserva el anillo compartido de Data Protection, cifrado con el certificado común.

Nginx admite cabeceras ampliadas para las cookies fragmentadas que puede generar el acceso OIDC y conserva todos los `Set-Cookie` del servidor junto con su propia cookie de afinidad.

## Configuración del proveedor OIDC

Conservar el cliente y el proveedor actuales. Añadir, sin eliminar las direcciones ya utilizadas, los callbacks del origen elegido:

- Inicio: `https://localhost:8080/signin-oidc`.
- Cierre: `https://localhost:8080/signout-callback-oidc`.
- Retorno posterior al cierre: `https://localhost:8080/`.
- Si se usa 8443 o 9443, registrar las direcciones equivalentes con ese puerto y los orígenes web que requiera el proveedor.

Confirmar los roles Administrador y Operador en las cuentas de aceptación y el tipo de claim ya configurado. La sesión conserva su cookie segura, vencimiento de 30 minutos y ausencia de renovación deslizante. No desactivar validaciones de tokens, nonce, correlación ni PKCE.

La aceptación OIDC real requiere iniciar y cerrar sesión con el proveedor y verificar ambos roles desde ese origen. Las herramientas sintéticas de esta carpeta no configuran el proveedor, no emiten tokens OIDC y no sustituyen esa aceptación.

## Caída y reincorporación de una instancia

Para una comprobación de fallos utilizar el proyecto aislado descrito a continuación. Los comandos de operación habituales son:

```powershell
docker compose stop app1
docker compose start app1
```

La afinidad se basa en `SolarUY_Afinidad`, independiente de la cookie autenticada. Varias sesiones independientes pueden atenderse en ambas instancias incluso desde una misma IP. Las solicitudes de una sesión conservan su destino disponible; no se espera alternancia entre instancias en cada solicitud.

La detección de Nginx es pasiva: un error observado marca el destino como fallido durante `fail_timeout=2s`. Una solicitud posterior puede probarlo al terminar ese intervalo. No se realizan sondeos cada dos segundos ni se garantiza detectar una caída sin tráfico. Los healthchecks de Compose organizan el arranque, pero no cambian la lista de destinos de Nginx durante la ejecución. Si no hay destinos disponibles, el proxy responde `502/504`.

Los circuitos de Blazor permanecen en memoria de la instancia que los creó. La pantalla informa una desconexión, intenta reconectar y permite recargar si se perdió el circuito. La recarga consulta nuevamente los datos persistidos y conserva la sesión autenticada reconocida por la otra instancia. La recuperación de un servidor también puede requerir una recarga para sesiones que usaron un destino de reemplazo. Las conexiones WebSocket ya abiertas no se trasladan.

Para apagar el entorno conservando las claves:

```powershell
docker compose down
```

No usar `down -v` en el entorno habitual: eliminaría el anillo compartido y volvería inválidas las sesiones que dependen de él. El MySQL actual es externo a este Compose y no se elimina al apagarlo.

## Entorno aislado de aceptación

Las escrituras y fallos se verifican en el proyecto `solaruy-req008-aceptacion`, con MySQL 8.4.8 y esquema `solaruy_req008`. Sus credenciales aleatorias y configuración se guardan en `.solaruy-acceptance/`, fuera de Git y de las evidencias. El script copia la configuración OIDC actual a esa carpeta privada y reemplaza únicamente la conexión para las pruebas. El arranque habitual sigue usando el archivo original.

Las configuraciones nuevas de aceptación usan HTTPS en 9443, diagnóstico de aplicaciones en `127.0.0.1:14000/14001` y MySQL en `127.0.0.1:13308`. Así pueden ejecutarse junto al entorno habitual. Las configuraciones privadas ya preparadas se conservan: si tienen puertos anteriores que se superponen, ajustar exclusivamente `SOLARUY_PUERTO`, `SOLARUY_DIAGNOSTICO_1` y `SOLARUY_DIAGNOSTICO_2` en `.solaruy-acceptance/compose.env`. Registrar los callbacks OIDC del puerto seleccionado. La recuperación de fallos solo debe detener contenedores del proyecto de aceptación.

```powershell
./arnes/prompting/tests/req008/preparar-aceptacion.ps1
$aceptacion = @('--env-file', '.solaruy-acceptance/compose.env', '-f', 'docker-compose.yml', '-f', 'arnes/prompting/tests/req008/docker-compose.aceptacion.yml')
docker compose @aceptacion up -d mysql-pruebas
# Esperar a que MySQL indique healthy antes del paso siguiente.
docker compose @aceptacion ps
dotnet run --project arnes/prompting/tests/req008/VerificarBalanceo.csproj -c Release -- preparar
docker compose @aceptacion up -d
```

`preparar` valida servidor, puerto y esquema y comprueba que este último no coincida con el habitual antes de migrar o insertar. Reutiliza las migraciones y prepara dos paneles de prueba sin cambiar el dominio ni el esquema del requerimiento. Si hay un problema, muestra códigos depurados, sin cadenas de conexión ni mensajes que puedan incluir datos privados. La opción de recuperación de clave pública de MySQL se habilita únicamente para esta conexión aislada.

### Herramientas auxiliares con sesiones sintéticas

Se ejecutan contra la aplicación y MySQL reales, pero crean cookies de prueba fuera del servidor usando el anillo exclusivo de aceptación. El servidor no incorpora endpoints de acceso de prueba ni omite su autenticación o autorización.

Preparar las claves de prueba después de abrir la página de inicio de sesión, que inicializa el anillo. `curl --insecure` se utiliza únicamente con el certificado local del entorno aislado:

```powershell
curl.exe --insecure --silent --output NUL https://localhost:9443/cuenta/iniciar-sesion
New-Item -ItemType Directory -Path .solaruy-acceptance/claves -Force | Out-Null
docker cp solaruy-req008-aceptacion-app1-1:/app/claves/. .solaruy-acceptance/claves
dotnet run --project arnes/prompting/tests/req008/VerificarBalanceo.csproj -c Release -- sesiones
npm install --prefix arnes/prompting/tests/req008
$env:SOLARUY_BASE_URL = 'https://localhost:9443'
node arnes/prompting/tests/req008/verificar-funcional.cjs --caidas
```

Las cookies sintéticas se guardan solo en archivos privados y vencen a los 30 minutos; repetir `sesiones` si expiran. La herramienta usa Microsoft Edge por defecto. Para usar el Chromium de Playwright, instalarlo y adaptar la opción de navegador según la instalación disponible. En Codex puede utilizarse el paquete incluido mediante `SOLARUY_PLAYWRIGHT`, sin instalar dependencias en el repositorio.

La comprobación genera `artifacts/req008/funcional.json` y capturas sin valores de cookies, tokens o claves. Cubre reparto y afinidad, dos circuitos reales, latencia de mediciones, aislamiento entre paneles, orden de lecturas, formularios con antiforgery entre instancias, altas existentes y retiro de suscripciones. También verifica un rechazo real de INSERT mediante un trigger exclusivo de la base aislada, que elimina en `finally`. Si la herramienta se interrumpe antes de limpiar, restablecer el escenario con:

```powershell
dotnet run --project arnes/prompting/tests/req008/VerificarBalanceo.csproj -c Release -- restaurar-escritura
```

Con `--caidas` se detienen y reincorporan instancias aisladas, se verifica el aviso y la recarga de Blazor y se comprueba la respuesta sin destinos. La herramienta también interrumpe el MySQL aislado para comprobar la recuperación de consultas, prueba Long Polling, deja circuitos sin interacción y recrea una instancia para comprobar su resolución por nombre. Revisar `funcional.json`: solo los criterios que tienen un resultado satisfactorio fueron ejecutados. Ante un error, no interpretar los casos siguientes como aprobados; volver a iniciar los servicios aislados con `docker compose @aceptacion up -d`.

El escenario C5.4 tiene una herramienta separada:

```powershell
node arnes/prompting/tests/req008/verificar-no-reintento.cjs
```

Esta herramienta bloquea temporalmente las escrituras de la tabla de parques del MySQL aislado. Envía un formulario válido a app1, comprueba que su INSERT haya llegado al servidor y termina abruptamente ese contenedor antes del commit. Después libera el bloqueo y verifica una respuesta `502/504` y cero inserciones en la otra instancia: la solicitud enviada no fue reproducida. El bloqueo se libera al finalizar o al cerrar la conexión auxiliar, con un límite de espera de 40 segundos. El resultado depurado queda en `artifacts/req008/no-reintento.json`. No ejecutar esta herramienta contra la base habitual.

### Aceptación externa y evidencias

Ejecutar las verificaciones C1 a C7 de `tasks.md` con sesiones OIDC reales de ambos roles. Para comprobar que ambas instancias reconocen la misma sesión, conservar la cookie autenticada y borrar únicamente `SolarUY_Afinidad` en una sesión sin circuitos abiertos; repetir la navegación hasta observar ambos valores de `X-SolarUY-Instancia`. No guardar valores de cookies en las evidencias.

Para realizarlo desde el navegador sin inspeccionar cookies, el siguiente auxiliar selecciona un destino exclusivamente en el proyecto aislado y habilita dos páginas temporales de aceptación. No modifica el `nginx.conf` habitual ni agrega endpoints al servidor SolarUY:

```powershell
./arnes/prompting/tests/req008/seleccionar-instancia-oidc.ps1 -Destino app1
# Sin circuitos abiertos, visitar https://localhost:9443/__req008/renovar-afinidad.
# Confirmar el rol y el acceso; contrastar el destino con los registros depurados.
./arnes/prompting/tests/req008/seleccionar-instancia-oidc.ps1 -Destino app2
# Repetir la misma visita, conservando la sesión autenticada.
./arnes/prompting/tests/req008/seleccionar-instancia-oidc.ps1 -Destino ambas
```

El retorno conserva el puerto público y elimina únicamente la cookie de afinidad. Para comprobar antiforgery, abrir y completar un formulario con app1 seleccionada; seleccionar app2 y enviarlo sin recargar el formulario. Utilizar sólo datos de aceptación. En `/__req008/permisos-oidc`, el botón intenta negociar con el hub existente mediante la sesión real del navegador: se espera 403 con Operador y 401 sin sesión. No envía mediciones ni lee respuestas con tokens, cookies o credenciales. Administrador prueba el envío desde el sensor existente de la página de monitoreo aislada.

El manejador existente `SesionCookieEventsHandler` permite verificar los metadatos de una emisión real: HttpOnly, Secure, SameSite, persistencia, duración y renovación. Si se habilita temporalmente su categoría de diagnóstico en la configuración privada aislada, hacerlo con autorización del usuario y sin aumentar los niveles globales ni los de los protocolos OIDC. Reiniciar únicamente las aplicaciones aisladas; registrar sólo la línea `Sesión OIDC emitida.` y restablecer después su nivel previo. La evidencia de duración corresponde al intervalo emitido del ticket; no equivale a haber esperado treinta minutos.

Registrar la instancia que confirma una medición y el tiempo hasta verla en la otra pantalla; el objetivo es hasta tres segundos después del guardado. Al apagar una instancia, conservar evidencia de la interrupción, del destino disponible y de la recuperación por recarga. Registrar los resultados de inicio, cierre, permisos y formularios con el proveedor real por separado de `tipo=auxiliar_sesiones_sinteticas`.

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Si la aplicación abierta bloquea archivos Debug, ejecutar esos mismos controles con `-c Release` sin detener procesos ajenos. Los tests estructurales no se modifican para acomodar cambios. Las advertencias NU1903 que ya aparecen en la línea inicial deben registrarse como preexistentes.

Apagar exclusivamente el proyecto de aceptación conservando los datos para revisión:

```powershell
docker compose @aceptacion down
```

Los volúmenes del proyecto de aceptación pueden eliminarse expresamente después de revisar las evidencias. No borrar volúmenes del proyecto habitual ni el MySQL existente. Los certificados, claves y configuraciones privadas no son evidencias y no deben adjuntarse al repositorio.
