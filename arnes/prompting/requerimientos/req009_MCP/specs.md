# req009 — Especificaciones: Consultas en lenguaje natural mediante MCP

## Alcance y decisiones

Incorporar un chat flotante para Administrador y Operador, con Gemini 3.5 Flash-Lite y herramientas MCP de solo lectura sobre parques, nodos, paneles y mediciones. Conservar las cuatro capas como carpetas y namespaces del servidor .NET 10, el simulador WebAssembly, MySQL, OIDC y el balanceo existentes. Respetar los tres diagramas de `.agents/Images` sin modificarlos. Quedan fuera la salud técnica de servidores, balanceador y base, las escrituras desde el asistente y el almacenamiento permanente de conversaciones.

Cambio de modelo autorizado expresamente por el usuario el 2026-11-05: `gemini-3.5-flash-lite` sustituye a `gemini-3.8-flash` tras los HTTP 503 por alta demanda, reproducidos incluso sin herramientas. Se conserva proveedor, nivel gratuito, adaptador HTTPS y arquitectura MCP. Las evidencias de 3.8 permanecen como historial y no acreditan la aceptación de 3.5 Flash-Lite. No se incorpora selección automática de otro modelo.

La persistencia actual ya registra mediciones UTC con potencia en W y estado; `IMedicionRepository` permite consultar últimas lecturas, pero no historial ni resúmenes. Ampliar las consultas sin cambiar el registro, la simulación ni las relaciones del dominio.

Flujo de una pregunta: navegador → endpoint autenticado → `AsistenteService` → adaptador Gemini → solicitudes de herramientas → cliente MCP → servidor MCP → servicios de consulta de Application → repositorios. Los resultados vuelven al modelo para redactar la respuesta. Toda obtención de información operativa para el modelo atravesará MCP.

Utilizar cliente y servidor MCP del SDK oficial C# conectados por streams en memoria, dentro de la instancia que atiende cada pregunta. Es una integración MCP real, con negociación, descubrimiento e invocación de herramientas, sin un servicio desplegable adicional. El SDK contempla este uso embebido mediante [transporte en memoria](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md#in-memory-transport). La conversación no dependerá de una sesión MCP ni de un circuito Blazor concreto.

## 1. Domain

- Mantener `ParqueSolar`, `Nodo`, `Panel`, `Medicion` y `EstadoPanel`, sus invariantes y navegaciones. No incorporar dependencias de MCP, Gemini, ASP.NET Core, EF Core o Blazor.
- Incorporar un objeto de valor `ResumenMediciones` que calcule cantidad, mínimo, máximo y promedio aritmético de potencia de las muestras recibidas. Un conjunto vacío devuelve magnitudes ausentes, nunca ceros inferidos. Acumular con precisión suficiente y redondear únicamente al presentar resultados.
- Para un nodo o parque, calcular la potencia promedio observada como la suma de los promedios de sus paneles en el período. Identificarla como tal: no es un promedio de todas las filas, que sesgaría el resultado hacia paneles con más muestras. Encapsular esta regla de agregación y la diferencia entre períodos en comportamiento de dominio, reutilizando los resúmenes por panel. Si faltan paneles o hay cobertura incompleta, devolver un resultado parcial y no declarar un ganador en una comparación concluyente.
- Distinguir el signo de la diferencia observada (`CambioObservado`) de su conclusión. Los filtros de aumento/disminución/igualdad seleccionan las diferencias calculadas en Domain, conservando `NoConcluyente` y las advertencias cuando la cobertura es parcial; no convertir una diferencia observada en un ganador ni ocultarla como si no hubiera datos.
- La potencia actual agrupada será la suma observada de las últimas lecturas vigentes de sus paneles. Informar por separado paneles sin lecturas y con lecturas antiguas. No crear un estado global ficticio: devolver conteos de los estados observados y sus fechas.
- Las magnitudes disponibles representan potencia, no energía acumulada. Expresar W o conversiones explícitas a kW/MW; no producir kWh mediante una suma de muestras ni diagnosticar causas de falla.
- Agregar pruebas unitarias de Domain para conjuntos vacíos, ceros medidos, promedios con distinta cantidad de muestras, agregaciones parciales y comparación de períodos. No modificar pruebas estructurales.

## 2. Application

### Servicios, puertos y autorización

- Crear `IAsistenteService`/`AsistenteService` para coordinar cada turno y `IConsultaOperativaService`/`ConsultaOperativaService` para resolver búsquedas, detalle, estado, historial, resúmenes y comparaciones. Inyectar dependencias por constructor, sin MediatR.
- Definir puertos para el modelo conversacional y el cliente de herramientas MCP, con DTO propios para mensajes, llamadas, resultados y errores; sin tipos de los SDK, HTTP, EF Core o UI. Los adaptadores externos implementarán esos puertos.
- Definir `IConsultaOperativaRepository`, exclusivamente de lectura, para consultas filtradas de la jerarquía y sus mediciones. Reutilizar servicios y contratos existentes cuando cubran la operación, sin exponer operaciones de alta ni `GuardarAsync` al catálogo del asistente.
- Comprobar autenticación, vigencia de la sesión y rol Administrador u Operador antes de procesar una pregunta y en cada ejecución de herramienta. Reutilizar `IUsuarioActualService` y completar el contrato de contexto de sesión necesario con datos simples emitidos por el servidor. Aplicar cualquier restricción existente sobre el elemento consultado; actualmente no se introduce una nueva propiedad por usuario o parque.
- Obtener identidad y permisos del contexto autenticado del servidor, nunca de argumentos del modelo, mensajes anteriores o identificadores del navegador. El ámbito autorizado se aplicará también antes de listar, contar o agregar datos. No reutilizar una identidad en un singleton ni confiar en un `HttpContext` capturado de un circuito anterior.
- Validar identificadores, pertenencias y contexto de página. Los nombres repetidos devolverán candidatos con ID y ruta parque/nodo/panel; pedir aclaración antes de elegir uno. “Todo” abarcará el conjunto autorizado, sin quedar restringido a la primera página de resultados.

### Catálogo MCP

Los adaptadores de herramientas serán delgados: validarán el contrato y delegarán en `IConsultaOperativaService`. Publicar únicamente este catálogo cerrado, con esquemas y descripciones en español:

| Herramienta | Entrada y resultado |
| --- | --- |
| `BuscarElementos` | Tipo, nombre, parque/nodo padre y paginación. Candidatos con ID, nombre y pertenencia completa; sin seleccionar silenciosamente un homónimo. |
| `ObtenerDetalle` | Tipo e ID. Características y relaciones autorizadas del elemento, sin configuración técnica privada. |
| `ConsultarEstadoActual` | Ámbito global, parque, nodo o panel; filtro de estado y paginación del detalle. Últimas lecturas con fecha, potencia, estado y conteos de paneles vigentes, antiguos y sin medición. |
| `ConsultarHistorial` | Elemento, intervalo, filtro de estado y cursor. Muestras ordenadas por fecha UTC e ID, con unidades y continuación explícita. |
| `ResumirMediciones` | Ámbito, período y agrupación por panel, nodo o parque. Estadísticas de potencia, estados observados y cobertura; admite resumen global sin enumerar todos los paneles al modelo. |
| `CompararMediciones` | Ámbito, dos intervalos y agrupación. Promedios por elemento, diferencias absolutas y relativas, orden y filtro por aumento/disminución calculados en SolarUY; referencia cero produce porcentaje no definido. |

Cada resultado incluirá código de resultado, instante de consulta, elementos y pertenencias, período o fecha de lecturas, unidades, origen simulado y advertencias de cobertura. Distinguir `SinDatos`, `NoEncontrado`, `AccesoDenegado`, argumentos inválidos, límite alcanzado y fallo de dependencia. Un error nunca será una colección vacía de éxito.

Valores iniciales configurables: 50 resultados por página, máximo 200; hasta 100 IDs explícitos por consulta y períodos de hasta 31 días por invocación. Los ámbitos globales podrán agregar todos los elementos autorizados en el servidor. Validar y comunicar los límites; permitir continuar por páginas o dividir períodos. Calcular agregaciones sobre todas las muestras del ámbito solicitado, sin usar una página truncada como si fuera el total.

### Tiempo, cobertura y precisión

- Resolver fechas con un reloj inyectado y `America/Montevideo`, independientemente de la zona del navegador, contenedor o modelo. Persistir y consultar en UTC usando intervalos `[desde, hasta)`, y devolver también los límites locales y la zona utilizada.
- “Hoy” abarca desde las 00:00 locales hasta el instante de consulta; “ayer”, el día calendario anterior. En comparaciones “hoy contra ayer”, utilizar por defecto igual tramo transcurrido en ambos días e identificarlo expresamente. Si se piden días completos, respetarlos y señalar que el día actual está en curso.
- “Durante la mañana” utiliza inicialmente 06:00–12:00 locales, limitado al instante actual si corresponde; comunicar esos límites. Pedir aclaración para expresiones que no permitan definir un intervalo o un elemento inequívoco.
- Usar un instante de referencia por pregunta. Para el estado actual, seleccionar la última muestra hasta ese instante, con desempate estable por ID. Cada nueva pregunta sobre actualidad vuelve a consultar MCP; las respuestas previas permanecen intactas.
- Considerar antigua una lectura de más de 60 segundos, umbral configurable. No sustituir una medición ausente por el estado inicial `Operativo` del panel ni por potencia cero.
- Para el historial, informar cantidad de muestras, primera y última fecha y mayor intervalo sin lecturas, incluidos los bordes del período efectivo. Marcar cobertura incompleta cuando falten paneles o existan huecos superiores a 60 segundos, parámetro configurable. Es una evaluación de cobertura observada; no garantiza monitoreo continuo. Indicar que el simulador actual envía cada 20 segundos mientras su pantalla está activa.
- Responder con hechos respaldados por herramientas e identificar elemento, período y unidades. Las estadísticas, diferencias, ordenamientos y agrupaciones se resuelven en SolarUY. El modelo solo interpreta la pregunta y redacta; separar sus interpretaciones de los datos observados y reconocer causas no determinables.

### Conversación y ejecución

- Enviar al modelo únicamente la pregunta, el contexto autorizado necesario y un historial acotado. Tratar mensajes previos, nombres y resultados como datos, sin aceptar instrucciones que cambien permisos o habiliten herramientas. No incluir cookies, tokens, credenciales, cadenas de conexión ni endpoints internos de sensores.
- Permitir que el modelo solicite varias herramientas en un turno. Descubrir el catálogo mediante MCP, convertir sus esquemas a llamadas de función de Gemini y devolver sus resultados manteniendo la correlación de llamadas. Revalidar cualquier referencia previa, incluidos los IDs de “esos nodos”, y consultar sus estados nuevamente si se pide “ahora”.
- Límites iniciales: pregunta de hasta 2.000 caracteres, hasta 30 mensajes y 32.000 caracteres de contexto, ocho rondas con el modelo, doce invocaciones MCP y 60 segundos por turno. Conservar el historial visible al recortar contexto y avisar cuando una referencia ya no pueda resolverse. No truncar listas ni presentar una respuesta parcial como exhaustiva.
- Mantener los datos de protocolo y firmas que Gemini requiera para las llamadas encadenadas dentro del turno, exclusivamente en el servidor. Entre turnos conservar solo mensajes y referencias simples, tratados como contexto no confiable; no aceptar mensajes de sistema ni resultados de herramientas fabricados por el navegador.
- Propagar cancelación y rechazar nombres o argumentos fuera del catálogo. Ante una solicitud de escritura, explicar la limitación. La garantía efectiva será que ninguna herramienta disponible puede escribir o ejecutar SQL/código arbitrario.

## 3. Infrastructure

### Persistencia y MCP

- Implementar `ConsultaOperativaRepository` sobre EF Core/MySQL, con contextos por operación, consultas sin seguimiento, filtros parametrizados y proyecciones de solo lectura. Aprovechar el índice existente por panel, fecha e ID. No cargar historiales completos en Blazor ni enviarlos al modelo para calcular estadísticas.
- Obtener muestras o acumulados suficientes para que Domain resuelva las reglas definidas, procesando por lotes cuando corresponda. No perder mínimos, máximos, conteos, huecos o muestras por límites de paginación. Para cada resumen o comparación utilizar una vista consistente de los datos de esa operación; informar que distintas herramientas del turno pueden observar nuevas escrituras.
- No se prevén nuevas tablas ni migraciones para conversaciones. Mantener la base habitual y `SolarUY/appsettings.Local.json` intactos.
- Implementar el cliente MCP como adaptador del puerto de Application. Crear y disponer una conexión por pregunta, conservándola durante sus llamadas sucesivas. Resolver los handlers con el mismo ámbito autenticado y pasar el contexto autorizado por mecanismos del servidor, fuera de los argumentos de las herramientas.
- Conectar cliente, transportes y servidor MCP en la raíz de composición (`Program.cs` o extensiones de arranque). Infrastructure no referenciará clases concretas de Presentation, y Presentation no importará repositorios o adaptadores concretos de Infrastructure. No compartir conexiones MCP entre usuarios ni publicar un endpoint MCP anónimo.

### Gemini, configuración y fallos

- Implementar un adaptador servidor para la API HTTPS de Gemini mediante `HttpClient`, con llamadas de función y respuestas estructuradas. El [modelo Gemini 3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite) documenta soporte de llamadas de función. Usar el identificador del modelo por configuración, con valor predeterminado `gemini-3.5-flash-lite`, sin reemplazarlo silenciosamente por otro modelo o modalidad de pago.
- Configurar habilitación del asistente, modelo, clave privada, tiempos y límites en fuentes privadas del servidor, mediante variables de entorno o un archivo privado adicional. Conservar el archivo local existente y excluir secretos de Git, imágenes Docker y archivos públicos de `SolarUY.Client`. Ambas instancias recibirán configuración equivalente.
- Antes de la aceptación, verificar con la cuenta real que ese modelo está disponible por API en el nivel gratuito. Los [límites efectivos dependen del proyecto y nivel de uso](https://ai.google.dev/gemini-api/docs/rate-limits); no fijar una cuota comercial ni dar su disponibilidad por comprobada mediante documentación. Si no está habilitado, informar el impedimento sin activar facturación o cambiar el requerimiento.
- Limitar el envío a texto y herramientas del catálogo; no habilitar búsqueda web, ejecución de código ni otros recursos del proveedor. No usar sesiones conversacionales persistentes ni almacenamiento de archivos del proveedor. Documentar brevemente las condiciones de uso y tratamiento de datos aplicables al nivel gratuito, enviando solo información autorizada necesaria.
- Convertir cuota agotada (`429`), errores del proveedor, timeout, fallo MCP y error de persistencia en resultados distinguibles y mensajes comprensibles. Respetar `Retry-After` cuando exista; ofrecer reintento manual, sin bucles automáticos. Una respuesta bloqueada o inválida del modelo tampoco equivale a falta de mediciones.
- Si falta configuración de Gemini, deshabilitar el chat con un aviso, sin impedir el arranque ni afectar altas, consultas, autenticación o monitoreo. La salud habitual de SolarUY no dependerá de Gemini.
- Registrar únicamente códigos depurados, duración, cantidades e identificador de correlación; sin cuerpos de conversaciones, respuestas operativas, secretos, cookies o tokens. Fijar versiones compatibles del SDK MCP y dependencias al implementar, respetando los controles arquitectónicos existentes.

## 4. Presentation

### Chat, sesión y contexto

- Añadir `ChatAsistente.razor` en Presentation, integrado desde `MainLayout` como componente interactivo de servidor independiente. Mantener la distribución actual de renderizado de las demás páginas y el sensor en `SolarUY.Client`.
- Ubicar el servidor MCP y los adaptadores de sus herramientas en `SolarUY/Presentation/Asistente`, como otra entrada de presentación que depende de Application. Sus handlers no consultarán EF Core ni invocarán servicios concretos de Infrastructure.
- Mostrar el chat solo con sesión autorizada, fijo abajo a la derecha y desplegable hacia arriba. Incluir cabecera con flecha de minimizar/restaurar, historial, campo, botón de envío, indicador de respuesta y “Nueva conversación”. Adaptar dimensiones a pantallas pequeñas y permitir manejo por teclado y anuncios accesibles de estado.
- Exponer un endpoint de sesión y `POST /api/asistente/mensajes`, autorizados para Administrador y Operador, en Presentation. El componente usará solicitudes del navegador al mismo origen mediante un módulo JavaScript pequeño, con cookie vigente y antiforgery en el POST. Así cada turno obtiene el contexto HTTP autenticado actual, también si el circuito Blazor quedó abierto al vencer la sesión.
- Guardar mensajes, referencias y estado minimizado en `sessionStorage`, por pestaña y sesión autenticada; nunca en `localStorage`, MySQL ni un singleton del servidor. El endpoint de sesión devolverá una marca no secreta ligada a la identidad, emisión y vencimiento de la sesión actual; esta marca no concede acceso. Validarla antes de restaurar o aceptar historial. No mostrar mensajes almacenados antes de esa validación.
- Restaurar la conversación al minimizar, navegar y recargar, incluso tras cambiar de instancia. En cada pregunta, enviar el historial acotado y la marca de sesión, tratados como datos no confiables. El servidor no conserva estado conversacional entre solicitudes y el navegador no llama directamente a Gemini o MCP.
- “Nueva conversación” cancela el turno pendiente y elimina mensajes y referencias. Ignorar respuestas tardías de conversaciones descartadas. Al cerrar sesión, al vencerla o al detectar otra sesión, borrar el almacenamiento y limpiar la vista; usar el vencimiento informado por el servidor y comprobarlo también al recuperar el foco. Una sesión nueva no recupera conversaciones anteriores.
- Capturar al enviar el tipo e ID del parque, nodo o panel de la ruta de detalle/monitoreo actual. Limpiar ese contexto al salir de esas rutas y congelarlo para el turno en curso. Application resolverá su nombre y pertenencia; no enviar todo el HTML ni la URL completa. La respuesta identificará el elemento interpretado y pedirá aclaración ante referentes ambiguos.
- Las vistas capturan eventos, invocan Application mediante el endpoint y muestran sus resultados. No calculan estadísticas, cobertura, permisos efectivos o estados de negocio. Renderizar texto escapado; si se utiliza Markdown, sanitizarlo y no ejecutar HTML generado por el modelo.
- Deshabilitar envíos duplicados durante el turno. Mostrar errores sin borrar la conversación y permitir reintentar el mismo mensaje sin duplicarlo en el historial. Ante `401/403`, cancelar, limpiar datos y ofrecer el acceso correspondiente. No actualizar automáticamente respuestas anteriores.

### Balanceo

- Acceder al chat por el origen HTTPS público existente, con rutas relativas y configuración privada equivalente en ambas instancias. Conservar afinidad, cookies y claves compartidas de Data Protection de req008.
- Cada solicitud puede resolverse desde cualquier instancia autorizada usando el historial aportado por la pestaña y una conexión MCP nueva. No incorporar Redis, afinidad adicional ni tablas de conversación. Si cae una instancia durante un turno, mostrar interrupción y permitir reintento en la disponible; no garantizar la recuperación automática de una respuesta en curso.

## Verificación y documentación de aceptación

Documentar brevemente configuración, uso, métricas y limitaciones en `arnes/prompting/tests/req009/configuracion.md`. Ubicar herramientas funcionales auxiliares en `arnes/prompting/tests/req009/` y evidencias depuradas en `artifacts/req009/`, excluido de Git. Preparar datos simulados y fallos solo sobre un destino aislado identificado, sin alterar la base o configuración habitual.

| Criterios de requirements.md | Comprobación desde el chat de SolarUY |
| --- | --- |
| 1, 11 | Acceder como Administrador y Operador; rechazar anónimo, otro rol y sesión vencida también en el endpoint y las herramientas. Solicitar una modificación y comprobar que no existe herramienta de escritura. |
| 2, 3 | Abrir, minimizar, navegar, recargar y usar “Nueva conversación”, incluido un turno pendiente. Cerrar sesión, dejarla vencer e iniciar otra; verificar descarte del historial anterior. |
| 4, 6 | Preguntar por “Panel 3” con homónimos, por paneles en falla en el parque Norte y por “este panel durante la mañana” desde su monitoreo. Comprobar aclaración, pertenencias, unidades y contexto al cambiar de página. |
| 5, 6, 7, 8 | Consultar “¿Cómo está funcionando todo hoy?” y “¿Qué nodos tuvieron menor potencia promedio hoy que ayer?”. Contrastar resultados con datos conocidos, períodos locales, cobertura y fórmulas; incluir más elementos que una página y paneles con distinta cantidad de muestras. |
| 5, 10 | Continuar con “De esos nodos, ¿cuáles tienen paneles en falla ahora?”. Cambiar mediciones en el destino aislado y comprobar nuevas llamadas MCP, referencias correctas y respuestas anteriores sin cambios. |
| 8, 9 | Consultar períodos vacíos, lecturas antiguas y cobertura parcial, y preguntar una causa no determinable. Distinguir cero medido, ausencia, antigüedad, simulación e interpretación. |
| 12 | Provocar fallo de Gemini, cuota y fallo MCP de forma aislada. Verificar mensajes diferentes de “sin datos”, reintento y continuidad de las demás funciones. |
| Restricción de balanceo | Continuar la conversación por el balanceador en ambas instancias y tras la caída de una de prueba, usando recarga/reintento si corresponde, sin perder sesión ni contexto ya confirmado. |

Durante la implementación, al terminar cada capa ejecutar desde la raíz `dotnet build SolarUY.slnx`, `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`. No avanzar con fallos ni modificar tests estructurales para acomodar el diseño. Crear pruebas unitarias solo para comportamiento nuevo de Domain.

La aceptación requiere interacción real desde el chat con Gemini, MCP y el proveedor OIDC. Las inspecciones, dobles y pruebas auxiliares complementan esa aceptación; registrar explícitamente cualquier comprobación externa pendiente.
