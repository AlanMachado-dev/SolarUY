# mod009 - Comprobaciones funcionales auxiliares

Ejecutar desde la raíz del repositorio. El modo auxiliar inicial usa MySQL de prueba en `127.0.0.1:43329`, esquema `solaruy_mod009` y usuario local `root` sin contraseña. Ese destino debe ser una instancia desechable, ligada a loopback y separada de la base habitual. Ese modo no lee la conexión de `appsettings.Local.json`, no elimina bases y rechaza preparar un esquema que ya tenga parques. El despliegue OIDC descrito más abajo usa credenciales privadas diferentes y se prepara con sus opciones específicas.

## Preparación y ejecución

Con el MySQL aislado disponible:

```powershell
dotnet run --project arnes/prompting/tests/mod009/VerificarClaridad.csproj -- --preparar
```

El primer uso crea el esquema y datos sintéticos conocidos. Los siguientes usos conservan esos datos:

```powershell
dotnet run --project arnes/prompting/tests/mod009/VerificarClaridad.csproj
```

Comprueba MCP real, repositorio EF/MySQL, intervalos relativos y calendario, agregados, formato, normalización de respuestas y rechazos con identidad sintética. Las pruebas de errores HTTP usan un transporte local guionado: no realizan solicitudes externas. No es un proyecto de pruebas unitarias ni reemplaza las suites de Domain y ArchUnitNET.

## Navegador aislado

```powershell
dotnet run --project arnes/prompting/tests/mod009/VerificarClaridad.csproj -- --web
```

Abre un servidor exclusivo de prueba en `http://127.0.0.1:43339`. Sus rutas `/entrar/Administrador`, `/entrar/Operador`, `/entrar/Expira` y `/entrar/Otro` crean identidades sintéticas solamente en este ejecutable auxiliar. La sesión `Expira` dura quince segundos. El componente, JavaScript y endpoints del chat son los reales; la respuesta es guionada y las consultas MCP y MySQL son reales. El texto de prueba incluye Markdown y una etiqueta `script` para verificar normalización y renderizado escapado. No son endpoints ni mecanismos de acceso de la aplicación habitual.

Comprobar el envío, el promedio `200,25 W`, el período de `16:01:36` a `16:03:36` de Uruguay, viñetas sin escapes, texto HTML inerte, minimizado, navegación, nueva conversación y descarte al vencer la sesión. Esta prueba no acredita OIDC, Gemini ni balanceo entre instancias.

## Validación externa optativa

La opción `--gemini` utiliza la configuración privada de `.solaruy-private/asistente.private.json` para enviar al proveedor una pregunta y resultados del escenario sintético. No usarla sin autorización para esa transmisión. No lee la conexión ni los datos habituales. Mantiene identidad sintética y tampoco acredita por sí sola la aceptación desde una sesión OIDC real.

## OIDC real y dos instancias en Docker

Desde PowerShell 7, con Docker Desktop disponible, ejecutar:

```powershell
./arnes/prompting/tests/mod009/preparar-oidc.ps1
```

El script prepara la configuración privada, valida Compose, construye la imagen actual, inicia MySQL, aplica migraciones solo al destino aislado, carga datos sintéticos y arranca las dos instancias y Nginx. No modifica ni detiene el proyecto habitual `solaruy`. El proyecto de pruebas es `solaruy-mod009-aceptacion`, con imagen, red, volumen de base y anillo de claves separados. No elimina volúmenes ni cambia contraseñas existentes. La variante `-SoloPreparar` prepara archivos y valida Compose sin acceder al motor ni iniciar contenedores.

| Recurso | Destino |
| --- | --- |
| Acceso por balanceador | `https://localhost:9449` |
| Selectores exclusivos de aceptación | `https://localhost:9449/_mod009` |
| Salud app1 / app2 | `http://127.0.0.1:14329/health/ready` / `http://127.0.0.1:14330/health/ready` |
| Base aislada | `solaruy_mod009`, MySQL `127.0.0.1:43329` |
| Credenciales y configuración | `.solaruy-acceptance/mod009/`, excluida de Git |
| Zonas de host de prueba | app1: `UTC`; app2: `Asia/Tokyo` |

El puerto 43329 se comparte con el MySQL auxiliar inicial: no ejecutarlos simultáneamente. La configuración OIDC se copia del archivo local a la carpeta privada; la conexión se reemplaza por la aislada. El archivo habitual permanece intacto. El programa de datos compara ambos nombres de esquema antes de escribir. No se emiten identidades sintéticas ni se habilitan accesos alternativos en SolarUY.

El proveedor OIDC debe admitir los callbacks `https://localhost:9449/signin-oidc` y `https://localhost:9449/signout-callback-oidc`, y el retorno `https://localhost:9449/`. No se modifican automáticamente los registros del proveedor ni el almacén de certificados. Si aparece un formulario de acceso o una advertencia del navegador, el usuario completa ese paso. Usar una ventana de prueba separada para conservar la sesión del entorno habitual.

Después de iniciar sesión, refrescar las lecturas de prueba sin entrar al monitoreo, que activaría el sensor:

```powershell
dotnet run --project arnes/prompting/tests/mod009/VerificarClaridad.csproj -c Release -- --actualizar-oidc
```

Los paneles `Mod009 principal`, `Mod009 parcial`, `Mod009 cero`, `Mod009 pequeño`, `Mod009 antiguo` y `Mod009 sin datos` cubren los escenarios. El promedio de `Mod009 principal` es `200,25 W` mientras tenga muestras en el período; la cantidad y cobertura deben comprobarse contra los resultados actuales, no suponerse constantes. `Mod009 compartido` aparece en dos nodos para probar desambiguación. `--actualizar-oidc` reemplaza únicamente lecturas de los paneles identificados del parque sintético, dentro del esquema aislado validado. No toca otros parques. La preparación es repetible y utiliza migraciones, no `EnsureCreated`.

Si el entorno ya estaba iniciado con una versión anterior del selector, validar y recargar Nginx para aplicar la corrección que conserva el puerto público al redirigir:

```powershell
docker compose --env-file .solaruy-acceptance/mod009/compose.env -f arnes/prompting/tests/mod009/docker-compose.oidc.yml exec -T nginx nginx -t
docker compose --env-file .solaruy-acceptance/mod009/compose.env -f arnes/prompting/tests/mod009/docker-compose.oidc.yml exec -T nginx nginx -s reload
```

El launcher también realiza esa validación y recarga al finalizar. No cambia la configuración del proxy habitual.

En `/_mod009`, seleccionar app1 y después app2 mediante navegación completa permite comprobar que ambas reconocen la misma sesión y conservan la conversación. El selector solo cambia una cookie de destino no autenticante en el proxy de pruebas; los permisos siguen comprobándose en la aplicación. Volver a «Usar balanceador» antes de probar recuperación ante la caída de una instancia.

Los comandos operativos quedan limitados al proyecto aislado:

```powershell
$mod009 = @('--env-file', '.solaruy-acceptance/mod009/compose.env', '-f', 'arnes/prompting/tests/mod009/docker-compose.oidc.yml')
docker compose @mod009 ps
docker compose @mod009 stop app1
docker compose @mod009 start app1
# Al terminar, apagar sin eliminar los datos ni las claves:
docker compose @mod009 down
```

La aceptación debe comprobar el inicio y cierre OIDC de Administrador y Operador, autorización real, las consultas de claridad y su continuidad por ambas instancias. No detener el despliegue habitual ni dar por aprobado un caso por preparar sus archivos.

## Evidencias y estado

### Regresión del seguimiento temporal

Con el MySQL aislado de mod009 iniciado y sus datos preparados, ejecutar:

```powershell
dotnet run --project arnes/prompting/tests/mod009/VerificarClaridad.csproj -c Release -- --seguimiento
```

Esta comprobación solo lee `solaruy_mod009:43329`. Usa MCP y MySQL reales, con reloj e identidad de Operador controlados en el auxiliar. Comprueba un resumen de seis muestras y su detalle 25 segundos después: los límites UTC deben conservar incluso las fracciones de segundo y el detalle debe seguir conteniendo esas seis muestras. También verifica la serialización del historial y el rechazo de metadatos temporales inválidos. No crea una sesión OIDC ni valida la cuenta abierta en el navegador.

La variante `--seguimiento-gemini` envía al proveedor configurado la pregunta y el contexto sintéticos para comprobar sus llamadas reales de seguimiento. Requiere autorización para esa transmisión; el usuario ya la otorgó en esta sesión. Escribe solo verificaciones en `artifacts/mod009/seguimiento-gemini.json`; la variante local escribe `seguimiento-local.json`. Ninguna guarda conversaciones completas, tokens ni configuraciones privadas.

El chat transporta los límites como metadatos opcionales, sin aritmética temporal en JavaScript. Los mensajes anteriores a esta corrección mantienen su texto; si no contienen límites exactos, el modelo debe pedir aclaración antes de afirmar que reproduce el intervalo. Usar una nueva conversación para la aceptación de la corrección.

### Repetición manual con Operador OIDC real

Después de reconstruir las aplicaciones aisladas con `preparar-oidc.ps1`, abrir `https://localhost:9449` con la cuenta Operador ya preparada y comprobar que no aparezcan acciones de administración. La reconstrucción conserva el volumen de claves; una sesión que todavía sea válida puede seguir utilizándose.

1. Iniciar una nueva conversación y preguntar: «¿Cuál es el promedio de potencia de hoy del panel Mod009 principal del nodo Mod009 Norte?». Debe indicar `200,25 W`, datos simulados y limitaciones relevantes, sin enumerar lecturas individuales.
2. Pedir: «Mostrame las mediciones utilizadas para ese promedio, conservando exactamente su período». El inicio y el fin deben coincidir con los del promedio anterior; debe mostrar seis lecturas. Para probar una ventana reciente, actualizar previamente los datos con `--actualizar-oidc` y repetir usando «últimos dos minutos».
3. Solicitar cambiar permisos o eliminar el panel sintético. Debe rechazar la modificación y conservar el rol Operador.
4. Minimizar el chat, navegar al listado, restaurarlo y comprobar que conserva los mensajes. En `/_mod009`, repetir por app1 y app2 conservando la sesión y el historial. Para comprobar el rol, usar las vistas y permisos de la aplicación, no solo la respuesta del modelo.
5. Volver a «Usar balanceador». Registrar el valor de `X-SolarUY-Instancia` de una respuesta en las herramientas de red del navegador, sin copiar cookies ni tokens. Detener únicamente esa aplicación del proyecto aislado con los comandos ya indicados; recargar y verificar que la otra reconoce la sesión. Si se perdió un circuito de Blazor, registrar la desconexión y la recuperación por recarga. Reincorporar la instancia y verificar su salud.
6. Cerrar sesión y comprobar la desaparición del chat y del historial; verificar por separado el vencimiento real cuando corresponda. Registrar resultados y fallos sin dar por aprobados pasos que no se ejecutaron.

Las comprobaciones anteriores siguen pendientes de observación después de la corrección. El ingreso como Operador fue informado por el usuario; no se lo considera una prueba ejecutada por el agente.

Los resultados depurados se escriben en `artifacts/mod009/funcional.json`. Las comprobaciones de navegador y los controles por capa se registran en esa misma carpeta, excluida de Git, sin cookies, tokens, claves ni conversaciones completas.

En esta implementación se ejecutaron las comprobaciones locales con MySQL 9.7.0 temporal; la equivalencia con el MySQL del despliegue debe comprobarse en su entorno aislado. Se verificó el navegador con identidad y modelo sintéticos. La prueba externa inicialmente rechazada por la revisión automática se ejecutó después de recibir autorización explícita del usuario: Gemini real, MCP real y MySQL aislado, con identidad y datos sintéticos. Resultaron correctos el promedio `200,25 W`, su ubicación en la primera oración, el elemento, el período de `16:01:36` a `16:03:36`, la ausencia de ISO/Markdown y la indicación de seis mediciones simuladas. Solo se registraron comprobaciones, sin respuesta completa ni secretos.

La prueba inicial de API utilizó identidad sintética. Posteriormente se verificaron desde el chat con Administrador OIDC real ambas instancias, la conservación del historial, promedios, comparación parcial, homónimos y los casos de cero, valor pequeño, ausencia y antigüedad. App2, configurada con TZ `Asia/Tokyo`, mantuvo las fechas de Uruguay. El seguimiento reveló el desplazamiento de intervalo documentado en F07: la corrección ya pasa por MCP y MySQL reales y por Gemini real con identidad y reloj controlados, pero falta reconstruir las aplicaciones y repetirla en el chat. Operador OIDC real, recuperación ante caídas y la matriz completa de errores/reintentos continúan pendientes. El usuario informó ingreso como Operador, pero esta ronda no dispone de control del navegador y Docker CLI continúa denegado.

El entorno de ejecución requirió forzar las tareas WebAssembly dentro de MSBuild por un fallo del host auxiliar (`MSB4216`). La adaptación temporal quedó exclusivamente en `artifacts/mod009/wasm-inprocess.targets`; no se modificaron SDK ni proyectos para omitir compilación. Las suites originales pasaron sin cambiar sus archivos.

Los auxiliares anteriores de req009 solo recibieron la nueva dependencia de formato en sus raíces de composición. Sus verificaciones textuales antiguas no se consideran aceptación de la nueva política de redacción; no se modificaron sus aserciones.
