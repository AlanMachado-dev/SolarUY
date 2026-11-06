# req009 — Verificación aislada

La [configuración, fórmulas y uso del asistente](../../requerimientos/req009_MCP/configuracion.md) se mantienen junto al requerimiento. Este arnés no lee `appsettings.Local.json`, no usa la base habitual y no altera las instancias req008.

## Destino y reproducción

El destino está fijado y validado por el programa: MySQL desechable `solaruy-req009-mysql`, loopback `127.0.0.1:43319`, base `solaruy_req009`. No cambiar estos valores para apuntar a una base habitual. El contenedor utiliza acceso local de prueba sin contraseña; no se publica fuera de loopback y no contiene datos reales.

Desde la raíz:

```powershell
docker run --detach --name solaruy-req009-mysql --label solaruy.prueba=req009 --publish 127.0.0.1:43319:3306 --env MYSQL_ALLOW_EMPTY_PASSWORD=yes --env MYSQL_DATABASE=solaruy_req009 mysql:8.4
dotnet run --project arnes/prompting/tests/req009/VerificarAsistente.csproj -m:1
```

Esperar a que MySQL esté disponible. El programa exige una base vacía. Para repetir la preparación exclusivamente en ese destino, agregar `-- --reiniciar`; esta opción elimina y recrea solo la base fija anterior.

Los datos incluyen 120 paneles, homónimos en cuatro nodos/dos parques, ceros medidos, panel sin medición, lectura antigua, huecos y distinta cantidad de muestras. Se comprueban tres páginas de elementos, continuidad del historial, resumen global de más de 100 paneles, diferencias conocidas y composición de 40 días en tramos sin promediar promedios de lotes.

Para el componente real, mantener el proceso siguiente abierto:

```powershell
dotnet run --project arnes/prompting/tests/req009/VerificarAsistente.csproj -m:1 -- --web
```

En otra terminal, instalar la dependencia fijada y ejecutar el navegador:

```powershell
npm ci --prefix arnes/prompting/tests/req009 --ignore-scripts
node arnes/prompting/tests/req009/verificar-chat.cjs
```

Se requiere Chromium de Playwright; si falta, ejecutar `npx --prefix arnes/prompting/tests/req009 playwright install chromium`. El servidor de prueba escucha únicamente en HTTPS localhost:43320 con certificado temporal. Sus rutas `/entrar/{rol}` emiten identidades sintéticas y solo existen en este arnés; nunca se registran en SolarUY. El modelo es guionado. MCP, consultas MySQL, componente, endpoints, antiforgery y almacenamiento de pestaña son reales.

Las capturas se guardan en `artifacts/req009/`. Al terminar, detener el proceso del arnés y el contenedor aislado. No detener los servicios habituales. Para eliminar definitivamente el contenedor de prueba, verificar primero su nombre y etiqueta `solaruy.prueba=req009`.

## Resultados y límites

| Área | Verificación realizada |
| --- | --- |
| Domain | 91 pruebas, incluyendo 15 casos nuevos de resúmenes/estado/comparación y cambio observado con cobertura parcial |
| Arquitectura | 17 pruebas existentes, sin modificaciones |
| MCP | Negociación, descubrimiento de seis herramientas e invocación por pipes reales del SDK 2.3.1 |
| Persistencia | MySQL 8.4 aislado; páginas sin omisiones, estados ausentes/antiguos, agregación completa, cursor, comparación y tramos |
| Conversación | Varias llamadas correlacionadas, referencias revalidadas, rechazo de sistema/marca falsa, límite de rondas y cancelación |
| Gemini | Respuestas HTTP sintéticas: 429/Retry-After, indisponibilidad y conservación de firmas/IDs con y sin ID de proveedor |
| Navegador | Componente real, envío, recarga/navegación, cambio de sesión, nueva conversación, texto escapado, rol, antiforgery, móvil y foco |

El modelo actual es `gemini-3.5-flash-lite`, autorizado por el usuario el 2026-11-05 tras los HTTP 503 de 3.8 Flash. La comparación real desde app1 devuelve HTTP 200 y texto tanto sin herramientas como con una mínima y con las seis declaraciones MCP. La cuenta corresponde al mismo proyecto confirmado «Nivel gratuito» por el usuario; no se activó facturación. Las pruebas históricas de 3.8 Flash se conservan. Administrador y Operador iniciaron sesión OIDC real; se verificaron acceso, navegación, envío, reintento sin duplicación y nueva conversación durante un turno pendiente. Ver [cambio documentado](../../requerimientos/req009_MCP/cambio-modelo-20261105.md) y [estado actualizado](resultados.md).

## Despliegue real aislado con el asistente deshabilitado

Se agregó un proyecto de aceptación independiente: HTTPS 9449, diagnóstico 14319/14320, base `solaruy_req009` y MySQL 43319. Usa una imagen y un anillo de Data Protection propios. No modifica las instancias habituales. La configuración de OIDC se toma de la aceptación privada de req008, sin copiarla a Git o evidencias. El archivo de Gemini se monta en solo lectura; una variable del entorno aislado deshabilita el asistente para las comprobaciones sin proveedor.

Este despliegue y el arnés desechable inicial comparten el puerto 43319: ejecutarlos por separado. No ejecutar `--reiniciar` del arnés inicial contra este despliegue.

```powershell
./arnes/prompting/tests/req009/preparar-aceptacion.ps1
$aceptacion = @('--env-file', '.solaruy-acceptance/req009/compose.env', '-f', 'arnes/prompting/tests/req009/docker-compose.aceptacion.yml')
docker compose @aceptacion build app1
docker compose @aceptacion up -d mysql-pruebas
# Esperar a MySQL healthy; preparar exige el esquema aislado vacío.
dotnet run --project arnes/prompting/tests/req009/VerificarAsistente.csproj -m:1 -- --preparar-despliegue
docker compose @aceptacion up -d app1 app2 nginx
dotnet run --project arnes/prompting/tests/req009/VerificarAsistente.csproj -m:1 -- --verificar-despliegue
```

La comprobación firma tickets sintéticos fuera del servidor usando exclusivamente el anillo de aceptación. No agrega endpoints de autenticación ni omite autorización en SolarUY. Verifica ambas instancias por el balanceador, roles/sesión vencida, configuración deshabilitada, consulta habitual de parques y registro real de una medición simulada mediante SignalR en la base de prueba. Posteriormente se completó FUN35 mediante el navegador con sesión OIDC real de Administrador: aviso visible y sensor funcionando. También se completó FUN07 al conservar la sesión y el chat tras detener app1 y recargar sobre app2; después el usuario inició sesión OIDC real como Operador. Ver [resultados y evidencias](resultados.md). Los escenarios conversacionales y la caída durante un turno se registran por separado.

Para continuar con OIDC real, usar una ventana privada separada del entorno habitual y autorizar en el proveedor los callbacks `https://localhost:9449/signin-oidc`, `https://localhost:9449/signout-callback-oidc` y el retorno `https://localhost:9449/`, conservando los existentes. Después de FUN35 se definió `SOLARUY_REQ009_HABILITADO=true` en el entorno exclusivo de aceptación y se recrearon solo sus aplicaciones. El chat de pruebas queda habilitado con `gemini-3.5-flash-lite` en el archivo privado adicional compartido; reiniciar ambas aplicaciones después de modificar ese archivo porque se lee al arranque. No habilitar facturación ni sustituir nuevamente el modelo sin autorización. El apagado independiente es `docker compose @aceptacion stop`; conservar los datos/anillo para continuar la aceptación.

El diagnóstico `--exportar-catalogo` del arnés solo descubre las seis herramientas por MCP y guarda sus definiciones públicas del contrato en `artifacts/req009/catalogo-mcp.json`; no consulta registros. `verificar-gemini-contenedor.ps1` prueba una generación mínima desde app1; con `-ConHerramientas` incluye esas definiciones. Las evidencias guardan estado HTTP/códigos y booleanos de generación, sin cuerpos de respuestas ni claves. Cada ejecución es manual, sin bucles de reintento.

Para una comparación controlada, con app1 de aceptación disponible y el catálogo ya exportado:

```powershell
./arnes/prompting/tests/req009/comparar-gemini.ps1
```

Realiza exactamente un intento por variante: sin herramientas, una declaración mínima y las seis declaraciones MCP reales. Conserva contenedor, modelo, credencial, pregunta y parámetros; solo varían las declaraciones. No ejecuta herramientas, no envía datos de parques/mediciones ni identidad y se detiene si recibe 429. Cada ejecución crea un directorio fechado `artifacts/req009/comparacion-gemini-*` con resultados depurados. La comparación histórica con 3.8 Flash devolvió 503 en los tres casos; la del modelo actual 3.5 Flash-Lite devolvió HTTP 200 y texto en los tres casos. Los diagnósticos individuales guardan archivos `gemini-35-flash-lite*.json` para conservar los históricos. Una muestra por variante no acredita estabilidad ni el ciclo completo de herramientas; ver [resultados.md](resultados.md).

## Estado de aceptación

Las 197 casillas de `tasks.md` están completas. Se verificaron los escenarios desde el origen HTTPS de aceptación con OIDC real de Administrador, Operador y cuenta sin roles, modelo Flash-Lite real y proyecto gratuito confirmado. Se comprobó la caída de una instancia y la conservación de sesión/conversación mediante recarga y reintento. Las inyecciones de cuota, conectividad y transporte se identifican expresamente; no se provocaron fallos ni se prepararon datos en la base habitual.

La ejecución posterior de los pendientes se registra en [aceptacion-20261105.md](aceptacion-20261105.md), con checkboxes actualizados al aprobar cada caso y evidencias depuradas en `artifacts/req009/aceptacion-real.json`. Incluye consultas reales sobre 125 paneles, continuidad entre instancias, caída durante un turno y fallos inyectados claramente separados. El estado final se consulta allí y en `tasks.md`, sin interpretar los apartados históricos como tareas todavía pendientes.

Las herramientas adicionales se ejecutan con el proyecto compilado: `--preparar-escenarios`, `--actualizar-escenarios`, `--verificar-guardas`, `--verificar-guardas-mcp`, `--verificar-regresion`, `--verificar-operaciones-fallo` y `--web-aceptacion`. No ejecutar preparación más de una vez ni usar `--reiniciar` en el despliegue persistente aislado. La página `/_aceptacion/req009/permisos` solo existe en nginx de aceptación y muestra estados HTTP usando la sesión del navegador sin revelar cookies o tokens.

Los overrides `docker-compose.fallo-proveedor.yml` y `docker-compose.retardo-proveedor.yml` son exclusivamente de aceptación. El primero desvía temporalmente Google a loopback en app1; el segundo retrasa CONNECT 45 segundos mediante un proxy interno sin puertos públicos, descifrado TLS ni registro de datos. Restaurar siempre app1 con solo el compose base y retirar el contenedor auxiliar al terminar. Las comprobaciones finales verifican ambas instancias saludables, sin esos overrides activos.

Advertencias preexistentes: NU1903 de `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.12 (GHSA-g27p-qf6j-c96c y GHSA-qjpx-jqm5-h2c4). No se modificaron paquetes ajenos a req009 para ocultarlas. El entorno restringido no iniciaba el host WebAssembly; los controles se ejecutaron con procesos .NET habilitados y `-m:1`, sin modificar los tests estructurales.
