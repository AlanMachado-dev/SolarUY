# req009 — Ejecución de pendientes de aceptación

Destino exclusivo: `solaruy-req009-aceptacion`, HTTPS localhost:9449, MySQL loopback 43319/esquema `solaruy_req009`. Modelo real `gemini-3.5-flash-lite`, mismo proyecto gratuito confirmado por el usuario. Las identidades reales se registran solo por rol; no se guardan claves, cookies, tokens ni identidad en evidencias.

## Correcciones encontradas mediante las pruebas

- Etiqueta estadística: las instrucciones distinguen la suma de promedios por panel del promedio de muestras y omiten la suma de muestras como si fuera potencia total o energía.
- Pregunta global: consulta totales completos con detalle de una fila, evitando que el detalle de más de 100 paneles exceda el contexto. Las búsquedas/comparaciones mantienen paginación explícita.
- Comparación parcial: Domain expone `CambioObservado`, distinto de `Conclusion`; Application filtra diferencias observadas y mantiene `NoConcluyente`. Cuatro nuevas pruebas Domain impiden inventar cambios sin muestras o convertir cobertura parcial en ganador.
- Referencias múltiples: se reprodujo una NullReferenceException en el mapeo de parámetros del proveedor MySQL para colecciones de Guid. Infrastructure construye predicados OR con parámetros escalares y conversión existente a char(36), sin concatenar SQL ni cargar registros para filtrar en memoria. Se comprueban búsqueda y estado con múltiples IDs por MCP real.
- Contexto de página y conversación: Application entrega al modelo solo nombres/pertenencias simples ya revalidados por MCP en el turno. Evita búsquedas redundantes y contabiliza ese contenido en el presupuesto. Un complemento con modelo sintético y MySQL/MCP reales verifica que ambos parques homónimos llegan al contexto; el comportamiento final se comprueba por separado con Gemini real.
- Instrucciones de estado: copiar los totales completos sin deducirlos del detalle filtrado y conservar la clasificación/fecha de lecturas antiguas.
- Estado sin lecturas: el detalle enviado al modelo contiene solo panel, última lectura nullable y vigencia calculada por Domain. Se eliminan resúmenes vacíos con sumas cero del resultado de estado para impedir que se interpreten como una lectura de 0 W. El adaptador conserva el texto histórico sin añadir bloques visibles de referencias; Application ya aporta sus pertenencias revalidadas por MCP.
- Preparación de aceptación: conserva el archivo de entorno existente al repetirse y genera la configuración de nginx exclusivo que sirve la página diagnóstica de permisos. No cambia la configuración del despliegue habitual.

Cada corrección de capa se compiló y pasó Domain y ArchUnitNET antes de continuar. Estado actual: Domain 91/91, arquitectura 17/17, compilación sin errores. Persisten las advertencias NU1903 preexistentes. Evidencias de controles: `artifacts/req009/aceptacion-*.txt`; cierre adicional de Domain en `aceptacion-domain-correccion-*.txt`.

## Casos aprobados

Las comprobaciones del chat usan OIDC real de Operador, Gemini real y MCP real salvo donde se identifica una inyección. Evidencia por caso: `artifacts/req009/aceptacion-real.json` (hechos/booleanos depurados, sin cuerpos de conversación).

| Tarea | Resultado y evidencia |
| --- | --- |
| FUN08 | Administrador OIDC real con chat en ambas instancias (evidencia previa `oidc-real-administrador.json`), Operador real con chat y GET 200. Anónimo y sesión OIDC realmente vencida: sin acceso al chat, GET/POST 401. Cuenta real sin roles reconocidos iniciada por el usuario: chat ausente y GET/POST 403. `oidc-casos-reales.json`, `FUN08-sin-roles.png`. Los rechazos externos se observaron con las sesiones reales; las guardas sintéticas se conservan como complemento separado. |
| FUN09 | Endpoint real rechaza marca, mensaje de sistema y referencias inventadas; MCP real rechaza contextos sintéticos ausentes/vencidos, otro rol e identidad fabricada. `guardas-endpoint.json`, `guardas-mcp.json`. Los contextos de rechazo son sintéticos, identificados como tales. |
| FUN10 | Chat disponible con OIDC real en inicio, weather, counter, listado, detalle de parque, detalle de nodo y monitoreo; como Administrador también en altas de parque, nodo y panel. Entrada/envío habilitados y minimización/restauración comprobados, sin completar ni enviar los formularios de alta. El envío real, indicador y bloqueo de duplicados se verificaron en las consultas anteriores. `paginas-chat-oidc-real.json`, `FUN10-formulario-administrador.png`. |
| FUN11 | Misma conversación exacta al minimizar/restaurar, navegar a un parque, recargar y volver al listado. Las referencias se conservaron y permitieron el seguimiento de FUN20 sobre el nodo correcto. |
| FUN13 | Vencimiento OIDC real elimina el historial y rechaza GET/POST 401. Tras cierre real de la conversación no vacía, ingreso como cuenta sin roles y luego Administrador, se volvió a la misma pestaña original: historial vacío al abrir y después de recargar. No reaparecen respuestas ni referencias de la cuenta anterior. `oidc-casos-reales.json`, `FUN13-sesion-vencida.png`, `aceptacion-real.json`. |
| FUN14 | Pregunta global real sobre 125 paneles, 123 con muestras y 193 muestras. Potencia promedio observada 30.203,1742 W, con advertencias de cobertura/antigüedad y dos paneles sin muestras. Contraste SQL independiente `FUN14-contraste.txt`. |
| FUN15 | Dos Panel 3 homónimos con ID y ruta de parque/nodo; pide aclaración sin seleccionar silenciosamente. |
| FUN16 | Norte: 0 en falla, 2 antiguos y 1 sin medición. Sur: Panel 3 EnFalla, 100 W, última lectura 21:16:36 UTC / 18:16:36 -03, antigua por superar 60 segundos. |
| FUN17 | Consulta desde monitoreo del panel de Norte: mañana local 06–12, equivalente UTC 09–15; tres muestras 0, 100 y 50 W; cobertura incompleta y huecos informados. |
| FUN18 | Al cambiar de Panel 3 Norte a Sur identifica ambos IDs y pide aclaración. Tras confirmar Sur consulta ese panel y devuelve SinDatos para la mañana. Al salir al listado y comenzar conversación nueva pide identificar panel, sin conservar el contexto de página anterior. |
| FUN19 | Nodo Comparación A: 6.110,5 W frente a 18.000 W, diferencia -11.889,5 W (-66,05 %), igual tramo hoy/ayer. La diferencia incluye la medición de regresión de 321 W añadida a un panel; no se confunde con el valor inicial de la preparación. Conclusión no concluyente y sin ganador. `FUN19-FUN25-contraste.txt`. |
| FUN20 | Después de la segunda medición de regresión, el turno previo sobre app1 observó 6.147,33 W hoy frente a 18.000 W ayer. Se añadieron 120 lecturas nuevas solo en Volumen req009. Desde app2, “De esos nodos…” respondió Comparación A con 60 paneles vigentes EnFalla y potencia vigente del nodo 600 W. SQL independiente confirma A: 60/60 EnFalla a 10 W y B: 0/60 EnFalla a 20 W. `FUN20-mediciones.txt`, `FUN20-contraste.txt`. |
| FUN21 | Contenido y fecha del mensaje de comparación permanecen idénticos después de agregar las mediciones y la pregunta de seguimiento; se comparó el texto visible completo, no solo una captura. |
| FUN22 | Panel Cero comprobado, hoy 06–08: 0 W real frente a ayer sin muestras; no inventa porcentaje ni potencia para el período vacío. |
| FUN23 | Lectura antigua con fecha/umbral explícitos, cobertura observada y comparación parcial no concluyente, sin ganador ni afirmación global injustificada. |
| FUN24 | Falla de Panel 3 Sur: fecha y hecho observados, causa no determinable y origen simulado explícitos. |
| FUN25 | Solicitud de alta, modificación y eliminación rechazada; catálogo sigue siendo seis consultas. SQL confirma que no se creó Intruso y Panel 3 Sur sigue existiendo con potencia nominal 500 W. |
| FUN26 | Solo app1 activa, DNS de Google desviado temporalmente a loopback. FalloProveedor real del adaptador en 15 ms, aviso distinto de ausencia de datos y mensaje único. Durante el fallo pasaron consulta, monitoreo y registro SignalR real en la base aislada. Restauración sin extra_hosts, ambas instancias saludables y reintento manual con respuesta. Posterior resumen global distingue potencia vigente no disponible de cero medido. `FUN26-*.txt`, `regresion-fallo-proveedor.json`. La interrupción de conectividad es inyectada, no un incidente atribuido a Google. |
| FUN27 | HTTP 429 inyectado en el adaptador real desde el arnés auxiliar HTTPS 43320. Espera Retry-After de 17 segundos respetada; intento temprano informa espera, reintento posterior exitoso sin duplicar. No se consume cuota real. |
| FUN28 | Corte deliberado de streams MCP reales en el arnés 43320: error de conexión, distinto de SinDatos; nueva conexión al reintentar responde sin duplicar mensaje. Identidad/modelo sintéticos. |
| FUN33 | Intento de asumir rol, llamar EliminarPanel y ejecutar HTML rechazado por el modelo real. La entrada aparece como texto; no hay img/script ejecutables en el log del chat. |
| FUN36 | Turno confirmado real exclusivamente en app1, luego app2 activa y app1 detenida. Misma sesión OIDC y texto confirmado conservados por recarga; seguimiento real en app2 con nuevas mediciones y referencias correctas. `FUN36-app1.txt`, `FUN36-app2.txt`. |
| FUN37 | Primera detención descartada como aceptación: el modelo ya había terminado. Repetición con retardo CONNECT de 45 segundos inyectado únicamente en app1, sin descifrar TLS. Indicador Consultando visible, CONNECT registrado y caída abrupta real antes de respuesta; recarga en app2 conserva seis mensajes confirmados y la pregunta pendiente única, informa interrupción y responde tras reintento manual real de Gemini. Se retiraron proxy/env temporales y se restauró app1. `FUN37-turno.json`, `FUN37-connect-retardado.txt`, `FUN37-interrupcion-controlada.txt`, `FUN37-restauracion.txt`, captura recortada `FUN37-recuperacion.png`. |
| FUN38 | Regresión existente del componente (`aceptacion-regresion-chat.txt`) y del despliegue (`regresion-despliegue.json`): autenticación/rechazos, consultas, monitoreo y registro SignalR real de una medición simulada solo en la base de prueba. Sesiones sintéticas identificadas. |

## Reproducción auxiliar

`--preparar-escenarios` agrega datos simulados y rechaza repetir la preparación para no duplicar muestras; no elimina los anteriores. Requiere después de la mañana local. `--actualizar-escenarios` registra nuevas lecturas solo en los nodos de Volumen req009. Ambos validan el destino y rechazan la base habitual.

`--verificar-guardas` y `--verificar-guardas-mcp` ejecutan los rechazos sin enviar preguntas a Gemini. `--verificar-regresion` reutiliza las comprobaciones existentes con el asistente habilitado. `--web-aceptacion` inicia el arnés sintético de fallos en HTTPS loopback 43320 con certificado local existente; no usar `--reiniciar` contra este despliegue.

La página `/_aceptacion/req009/permisos`, servida únicamente por nginx aislado, permite comprobar GET/POST con la sesión OIDC del navegador sin revelar su contenido. No agrega endpoints de autenticación ni omite permisos. El override `docker-compose.fallo-proveedor.yml` reserva una inyección reversible del destino DNS de Google a loopback solo para app1 de aceptación; nunca se aplica al entorno habitual.

Todos los casos de esta ejecución quedaron aprobados y sus casillas se marcaron después de comprobar las evidencias. Las inyecciones están identificadas por separado de los accesos OIDC y conversaciones reales.

## OIDC real: avance de los casos de acceso

La sesión real de Operador venció sin alterar duración ni cookies: GET sesión y POST mensajes devolvieron 401 desde la página diagnóstica, y el chat eliminó su historial y deshabilitó el envío. Después de un nuevo ingreso OIDC de Operador, la conversación comenzó vacía. Al terminar las consultas se cerró sesión realmente: sin chat y GET/POST 401. El usuario ingresó después con una cuenta sin roles reconocidos; se observó chat ausente y GET/POST 403. Luego ingresó como Administrador. En la pestaña de la conversación original, el chat permaneció vacío al abrirlo y después de recargar. Se verificaron también los tres formularios exclusivos de Administrador sin guardar registros. Evidencias sin identidad: `oidc-casos-reales.json`, `FUN13-sesion-vencida.png`, `FUN08-sin-roles.png` y `paginas-chat-oidc-real.json`.

## Cierre de aceptación

Se aprobaron y marcaron las 24 tareas que estaban pendientes: el plan queda con 197 casillas completas y ninguna pendiente. FUN08 se cerró con la cuenta OIDC real sin roles; FUN10 con los tres formularios exclusivos de Administrador; FUN13 con la misma pestaña de la conversación anterior, vacía después del nuevo ingreso y de recargar. No se usan tickets sintéticos para dar por realizados esos ingresos externos.

El usuario completó los ingresos reales como Administrador, Operador y cuenta sin roles. No se inspeccionaron ni guardaron los campos de credenciales, identidad o cookies. Las capturas finales excluyen la barra de identidad.

Cierre del código: `cierre-aceptacion-build.txt` (cero errores, 12 advertencias NU1903 preexistentes), `cierre-aceptacion-domain.txt` (91/91) y `cierre-aceptacion-arquitectura.txt` (17/17). Ambas instancias de aceptación están saludables con la imagen final, sin HTTPS_PROXY temporal ni extra_hosts de Google. Se retiró el contenedor auxiliar de retardo. Los servicios habituales siguen con req008 saludables. La inspección de archivos públicos encontró cero coincidencias de la clave real; configuración/evidencias privadas continúan excluidas de Git. Diagramas, tests estructurales y appsettings.Local.json habitual sin modificaciones.
