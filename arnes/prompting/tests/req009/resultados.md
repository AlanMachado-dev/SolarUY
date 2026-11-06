# req009 — Estado de verificación

Fecha: 2026-11-05. Se desplegaron dos instancias exclusivas de aceptación, sin desplegar cambios sobre las instancias habituales. Se conservaron la base habitual, `appsettings.Local.json`, los diagramas y los tests estructurales. Los cambios previos de req008 permanecen en el árbol de trabajo.

Aceptación completa: las 197 casillas de `tasks.md` están marcadas; se aprobaron las 24 que quedaban pendientes. La [ejecución de aceptación del 2026-11-05](aceptacion-20261105.md) registra datos ampliados a 125 paneles, fallos encontrados, correcciones por capa, OIDC real y comprobaciones de balanceo. Domain tiene 91/91 pruebas correctas, arquitectura 17/17 y compilación sin errores. Los apartados históricos conservan los impedimentos observados antes de ese cierre.

## Últimas comprobaciones aprobadas

FUN08: cuenta real sin roles, sin chat y GET/POST 403; anónimo y sesión realmente vencida, GET/POST 401. FUN10: chat disponible en las páginas habituales y en las altas de parque, nodo y panel como Administrador, sin guardar formularios. FUN13: en la misma pestaña de la conversación anterior, ingreso con otra cuenta autorizada y recarga dejan el chat vacío. Evidencias depuradas: `oidc-casos-reales.json`, `paginas-chat-oidc-real.json`, `aceptacion-real.json`, `FUN08-sin-roles.png`, `FUN13-sesion-vencida.png` y `FUN10-formulario-administrador.png` en `artifacts/req009`.

El cierre de código está en `cierre-aceptacion-build.txt`, `cierre-aceptacion-domain.txt` y `cierre-aceptacion-arquitectura.txt`. Se mantienen las advertencias NU1903 preexistentes. Ambas instancias de aceptación quedaron saludables con la imagen final, sin proxy ni DNS de fallo; el contenedor auxiliar de retardo fue retirado. El despliegue habitual conserva req008.

## Modelo actual: Gemini 3.5 Flash-Lite

El usuario autorizó expresamente el cambio el 2026-11-05. Se actualizaron el contrato de configuración, el archivo privado adicional, requirements, specs, tasks y los cuatro diagnósticos. Se conserva proveedor, clave, nivel gratuito y arquitectura MCP; no hay cambio automático de modelo. Ver [detalle del cambio](../../requerimientos/req009_MCP/cambio-modelo-20261105.md).

La comparación real del modelo `gemini-3.5-flash-lite` del 2026-11-05 a las 22:50 UTC produjo HTTP 200, texto y finalización STOP en las tres variantes: sin herramientas (0,86 s), una mínima (0,78 s) y seis declaraciones MCP (0,84 s). No se ejecutaron herramientas ni se enviaron datos operativos/identidad en este diagnóstico. FUN06 se verificó de nuevo y permanece completa para el modelo actual. Evidencia: `artifacts/req009/comparacion-gemini-20261105T225012386Z/resumen.json`. Una muestra por variante no acredita estabilidad ni aceptación de las preguntas completas.

Controles posteriores al cambio: compilación correcta con cero errores, Domain 87/87 y arquitectura 17/17; sintaxis PowerShell válida. Evidencias `cambio-modelo-build.txt`, `cambio-modelo-domain.txt` y `cambio-modelo-arquitectura.txt` en `artifacts/req009`. Permanecen las advertencias NU1903 preexistentes.

Se reconstruyeron y recrearon únicamente app1/app2 de aceptación; ambas saludables. La sesión OIDC real de Operador se conservó. Al reintentar la pregunta global pendiente desde el chat se obtuvo una respuesta operativa con resumen y estado actual; el adaptador registró tres pasos Exito. Contraste SQL de solo lectura sobre datos simulados: coinciden 4 paneles, 2 con muestras, 8 muestras, mínimo 0 W, máximo 441,24 W, promedio de muestras 122,72 W, suma de promedios por panel 225,96 W y estados 5 Operativo / 1 EnFalla / 2 Desactivado. Las instancias habituales siguen con la imagen req008.

En esa primera consulta FUN14 no se marcó: la respuesta llamó «promedio ponderado» a la suma de promedios por panel, pese a aclarar la fórmula entre paréntesis. La ejecución posterior corrigió la etiqueta y aprobó el caso sobre 125 paneles. Se conservan como historia del cambio `cambio-modelo-verificacion.json`, `cambio-modelo-eventos.json`, `cambio-modelo-contraste-datos.txt`, `chat-gemini-35-flash-lite.png`, `cambio-modelo-imagen.txt` y `cambio-modelo-despliegue.txt` en `artifacts/req009`.

## Historial: configuración y diagnóstico de Gemini 3.8 Flash

El archivo privado adicional se validó sin mostrar la clave. El usuario confirmó que el proyecto indica «Nivel gratuito» en AI Studio. Se completó una generación mínima real de `gemini-3.8-flash` desde app1 de aceptación: HTTP 200 y texto generado. FUN06 completa: disponibilidad básica de la API y nivel gratuito confirmado, sin activar facturación ni sustituir el modelo. Evidencia depurada: `artifacts/req009/gemini-real-contenedor.json`.

Las consultas completas siguen pendientes: una prueba con las seis declaraciones de funciones descubiertas mediante MCP recibió HTTP 503 `UNAVAILABLE`, registrado en `gemini-real-contenedor-herramientas.json`. Solo se enviaron los nombres/descripciones/tipos previstos por req009 y una pregunta mínima fija; la revisión local encontró cero campos privados en los esquemas. La aprobación automática inicial bloqueó esa prueba por considerar interno el catálogo; se reconsideró y autorizó después de presentar el alcance explícito de req009 y la inspección del payload. No se enviaron credenciales dentro del cuerpo, registros de parques/mediciones ni identidad del usuario en ese diagnóstico. La clave de autenticación se pasó exclusivamente por stdin al curl del contenedor, sin imprimirse. Los intentos anteriores desde Windows con `SocketException`/HTTP 0 y el HTTP 503 anterior no acreditaban generación; se conservan como diagnóstico, distinguiéndolos del HTTP 200 posterior. No se establecen reintentos automáticos.

## Historial: comparación controlada de Gemini 3.8 Flash

El 2026-11-05 entre 22:42:32 y 22:42:43 UTC se ejecutaron tres solicitudes consecutivas desde el mismo contenedor, con la misma clave/modelo, pregunta fija «Responde solamente OK.», salida máxima de 512 tokens, pensamiento `low`, tiempo máximo de 60 segundos y selección de funciones AUTO predeterminada. Solo cambiaron las declaraciones de herramientas. Un intento por variante, sin reintentos; ninguna herramienta ejecutada ni datos operativos o identidad enviados.

| Variante | HTTP / estado | Duración | Mensaje indica alta demanda | Texto generado |
| --- | --- | --- | --- | --- |
| Sin herramientas | 503 / UNAVAILABLE | 2,67 s | Sí | No |
| Una herramienta mínima | 503 / UNAVAILABLE | 4,03 s | Sí | No |
| Seis herramientas MCP reales | 503 / UNAVAILABLE | 3,48 s | Sí | No |

La indisponibilidad se reproduce sin MCP y el mensaje del proveedor indica alta demanda en las tres solicitudes. Esta muestra respalda un impedimento de disponibilidad del servicio; no valida los esquemas ni demuestra que no existan otros problemas en una conversación completa. No se recibió 429 ni un error 400 de esquema. No se modificaron facturación, modelo ni código de producción. Las aceptaciones conversacionales siguen pendientes.

Ejecución histórica de `comparar-gemini.ps1` antes del cambio a Flash-Lite. Evidencia depurada: `artifacts/req009/comparacion-gemini-20261105T224232859Z/resumen.json` y `caso-1.json` a `caso-3.json`. Después de añadir este diagnóstico auxiliar se ejecutaron las reglas arquitectónicas: 17/17 correctas, con las advertencias NU1903 preexistentes. El script actual utiliza el modelo autorizado nuevo.

## Historial: primer acceso de Operador y envío desde el chat

El usuario inició sesión como Operador por Auth0. Se verificaron rol visible, listado y detalle de parques, chat visible y ausencia de «Registrar parque solar». La navegación directa al alta devolvió «Acceso denegado», conservando la sesión de Operador.

Después de completar FUN35 se habilitó el asistente únicamente en `solaruy-req009-aceptacion` mediante `SOLARUY_REQ009_HABILITADO=true`; las aplicaciones se recrearon conservando el anillo de Data Protection. La sesión OIDC real de Operador siguió válida. Se envió la pregunta de aceptación global y se observaron el mensaje único, «Consultando…» y el envío duplicado deshabilitado. El primer intento informó fallo del proveedor; un reintento manual conservó un único mensaje y terminó por límite temporal. El error no se presentó como falta de mediciones. La pregunta se conservó al minimizar/restaurar, navegar a un parque y recargar. En esa etapa no hubo respuesta operativa: FUN11 y las preguntas quedaron pendientes hasta la aceptación posterior con Flash-Lite.

FUN12 completa: se reanudó el turno pendiente y, con «Consultando…» visible, se pulsó «Nueva conversación». Desapareció el mensaje y se habilitó la entrada. Después de terminar el turno en el servidor y de volver al listado, la conversación permaneció vacía y el contexto descartado no reapareció. Evidencias depuradas: `oidc-real-operador.json` y `chat-oidc-operador.jpg`. FUN08 quedó pendiente en esa etapa y se completó después con las sesiones OIDC reales; los rechazos sintéticos del arnés siguen documentados como complemento separado.

## Despliegue de aceptación independiente de Gemini

Proyecto Docker `solaruy-req009-aceptacion`, imagen exclusiva `solaruy:req009-aceptacion`, origen `https://localhost:9449`, MySQL loopback 43319/esquema `solaruy_req009`, diagnósticos 14319/14320 y anillo de Data Protection exclusivo. Ambas instancias se comprobaron con el asistente deshabilitado por configuración del entorno aislado.

FUN35 completa: arranque y disponibilidad de ambas instancias, indisponibilidad informada por el endpoint de sesión, POST antiforgery con código `Deshabilitado`, listado real de parques y registro de una medición simulada mediante el hub real. Las comprobaciones iniciales usaron identidades sintéticas. Después, con inicio OIDC real de Administrador efectuado por el usuario, se observó el aviso «El asistente no está configurado. Las demás funciones continúan disponibles.» y los controles de envío deshabilitados. El chat conservó su despliegue al navegar entre listado, detalle de parque, nodo y monitoreo. El simulador real confirmó un nuevo envío y la potencia/fecha se actualizaron con el asistente deshabilitado. Se abandonó el monitoreo al finalizar para detener el sensor de esa pantalla.

FUN07 completa: acceso por Auth0 real como Administrador en `https://localhost:9449`; chat visible en app1. Se detuvo exclusivamente `app1` del proyecto de aceptación; apareció el aviso de conexión anterior no disponible. Después de pulsar «Recargar página», la página y el chat siguieron autorizados como Administrador sobre app2, sin repetir el inicio de sesión. Los logs depurados registraron HTTP 200 desde las direcciones de app1/app2, previamente vinculadas a sus contenedores. App1 se restauró al terminar. Ese primer control acreditó acceso OIDC al chat en ambas instancias; FUN36/FUN37 se completaron posteriormente con turnos reales y caída durante un turno pendiente.

Evidencias en artifacts/req009: `despliegue-verificacion.txt`, `despliegue-aislado.json`, `oidc-real-administrador.json`, `chat-oidc-administrador.jpg` y `oidc-cambio-instancia.txt`. Se conserva únicamente la captura recortada al chat, inspeccionada para excluir identidad, cookies y tokens. Controles posteriores al código del arnés: compilación, Domain 87/87 y arquitectura 17/17; permanecen las advertencias NU1903 preexistentes.

## Controles por capa

| Cierre | Compilación | Domain | ArchUnitNET |
| --- | --- | --- | --- |
| Base inicial | Correcta | 76/76 | 17/17 |
| Domain | Correcta | 87/87 | 17/17 |
| Application | Correcta | 87/87 | 17/17 |
| Infrastructure | Correcta | 87/87 | 17/17 |
| Presentation y revisión final | Correcta | 87/87 | 17/17 |
| Correcciones y cierre de aceptación | Correcta | 91/91 | 17/17 |

Evidencias: `artifacts/req009/ejecucion.md`, archivos `*-build.txt`, `*-domain.txt`, `*-arquitectura.txt`, `funcional.txt`, `chat-funcional.txt` y `chat-movil.png`. Las ejecuciones usan `-m:1` y permisos de procesos .NET para superar el impedimento del host WebAssembly del entorno restringido. Permanecen las advertencias NU1903 preexistentes documentadas en configuración.

## Criterios de requirements.md

| Criterio | Verificación aprobada | Tipo de evidencia |
| --- | --- | --- |
| 1. Permisos | Administrador/Operador reales; sin roles 403, anónimo y sesión realmente vencida 401. Marca, mensajes e identidad fabricados rechazados | OIDC real; guardas MCP/HTTP auxiliares con contextos sintéticos identificados |
| 2. Chat | Disponible en páginas habituales y tres altas de Administrador; envío, indicador, minimización y controles | Navegador real; móvil, foco y accesibilidad complementados por arnés |
| 3. Continuidad | Mensajes/referencias conservados al navegar/recargar; nueva conversación cancela; cierre/vencimiento y nuevo Administrador no recuperan historial | OIDC y chat reales; complementos sintéticos separados |
| 4. Contexto | Panel 3 homónimos, consulta desde monitoreo, cambio de panel con aclaración y descarte al salir | Gemini/MCP reales, datos simulados autorizados |
| 5. MCP | SDK 2.3.1 por pipes, catálogo e invocaciones reales, ciclo completo desde el chat | Gemini/MCP reales y comprobaciones auxiliares |
| 6. Consultas | Búsqueda, pertenencias, estado, historial, resumen de 125 paneles y comparación contrastados | Gemini/MySQL/MCP reales; paginación y volumen complementados por arnés |
| 7. Tiempo | Hoy/ayer con igual tramo y mañana local 06–12 (UTC 09–15); límites y zona explícitos | Interpretación real; ISO y bordes temporales auxiliares |
| 8. Precisión | Suma de promedios por panel, diferencias conocidas y cambio observado separado de conclusión | Domain 91/91, contraste SQL y respuestas reales; composición de 40 días auxiliar |
| 9. Incompletitud | Cero medido frente a ausencia, fechas antiguas, cobertura parcial, sin ganador ni causas inventadas | Respuestas reales y contrato de estado nullable comprobado por MCP |
| 10. Actualización | Seguimiento del nodo correcto tras nuevas lecturas: 60 paneles EnFalla; mensajes anteriores idénticos | Gemini/MCP reales, SQL independiente y comparación de texto visible |
| 11. Solo lectura | Solicitudes de alta/modificación/eliminación rechazadas, seis herramientas de lectura y datos sin cambios solicitados | Gemini real, catálogo MCP y contraste SQL |
| 12. Fallos | Error de proveedor aislado, cuota/espera, fallo MCP, reintento y operaciones habituales disponibles | DNS/HTTP 429/corte de streams inyectados e identificados; recuperación real, sin agotar cuota |
| Balanceo | Turnos reales en app1/app2; caída antes de respuesta, recarga/reintento conserva sesión y confirmados | Caída real; retardo CONNECT inyectado sin descifrar TLS; Gemini real al reintentar |

No quedan casillas pendientes. Las aceptaciones reales y los fallos inyectados están diferenciados en la ejecución detallada; las simulaciones no se presentan como incidentes externos ni como ingresos OIDC reales. No se habilitó facturación; el cambio a 3.5 Flash-Lite fue autorizado expresamente y las evidencias históricas de 3.8 Flash se conservan.
