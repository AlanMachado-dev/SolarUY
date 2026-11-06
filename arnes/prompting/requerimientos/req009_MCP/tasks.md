# req009 — Plan de implementación: Consultas en lenguaje natural mediante MCP

Seguir [requirements.md](requirements.md) y [specs.md](specs.md). Interpretar las rutas y ejecutar los comandos desde la raíz del repositorio. Todas las casillas representan trabajo pendiente de implementación o verificación; marcarlas únicamente después de comprobar su resultado.

Implementar en orden Domain → Application → Infrastructure → Presentation. Al cerrar cada capa, completar los tres controles indicados y corregir los fallos antes de avanzar. Ejecutar también ArchUnitNET durante los cambios de código o dependencias, conforme a las reglas del proyecto. No modificar los tests estructurales para acomodar la implementación. Crear pruebas unitarias únicamente para comportamiento de Domain; las comprobaciones funcionales auxiliares se ubican en `arnes/prompting/tests/req009/`.

Conservar `SolarUY/appsettings.Local.json`, la base habitual, los diagramas de `.agents/Images` y los cambios ajenos. Preparar datos y provocar fallos únicamente en un destino aislado identificado. Guardar evidencias depuradas en `artifacts/req009/`, excluido de Git, sin secretos, tokens ni valores de cookies.

## 0. Preparación y referencia inicial

- [x] PRE01. Leer las reglas compartidas y revisar los tres diagramas protegidos antes de implementar.
- [x] PRE02. Registrar el estado inicial de Git para distinguir los cambios existentes de los propios de req009.
- [x] PRE03. Identificar los puntos de integración de consultas, mediciones, OIDC, layout y balanceo de req008 sin leer ni registrar secretos innecesarios.
- [x] PRE04. Comprobar la disponibilidad del SDK .NET 10 y las herramientas del entorno balanceado existente.
- [x] PRE05. Ejecutar `dotnet build SolarUY.slnx` y registrar el resultado inicial.
- [x] PRE06. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado inicial.
- [x] PRE07. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado inicial de ArchUnitNET.
- [x] PRE08. Registrar advertencias y fallos preexistentes por separado; resolver los impedimentos a los controles antes de iniciar las capas.

## 1. Domain

### Resúmenes y agregación

Implementar cada comportamiento junto con su prueba xUnit correspondiente en `tests/SolarUY.Domain.Tests/`.

- [x] DOM01. Crear el objeto de valor inmutable `ResumenMediciones` con cantidad de muestras y magnitudes de potencia en W.
- [x] DOM02. Implementar mínimo, máximo y promedio aritmético con acumulación de precisión suficiente y sin redondeo prematuro.
- [x] DOM03. Representar un conjunto sin muestras con magnitudes ausentes y verificar que se distinga de un cero medido.
- [x] DOM04. Implementar la composición de acumulados de un mismo panel sin promediar directamente los promedios de lotes con distinta cantidad de muestras.
- [x] DOM05. Implementar la potencia promedio observada de nodo/parque como suma de promedios por panel, independiente de la cantidad de muestras de cada panel.
- [x] DOM06. Representar explícitamente la agregación parcial cuando falten paneles o la cobertura informada sea incompleta.
- [x] DOM07. Implementar la comparación de períodos con diferencia absoluta y relativa; representar como no definido el porcentaje con referencia cero.
- [x] DOM08. Evitar conclusiones de superioridad o inferioridad cuando la comparación sea parcial por falta de cobertura.
- [x] DOM09. Implementar la suma de potencia actual observada usando únicamente últimas lecturas vigentes, conservando conteos separados de paneles sin medición y con medición antigua.
- [x] DOM10. Conservar los estados observados y sus conteos sin inventar un nuevo estado global ni convertir ausencia de lecturas en `Operativo`.

### Verificación del comportamiento

- [x] DOM11. Ejecutar los casos unitarios de muestras vacías, cero real, una muestra y múltiples muestras con valores conocidos.
- [x] DOM12. Ejecutar los casos unitarios de composición de lotes y paneles con distinta cantidad de muestras para detectar promedios sesgados.
- [x] DOM13. Ejecutar los casos unitarios de agregación completa/parcial y lecturas vigentes, antiguas y ausentes.
- [x] DOM14. Ejecutar los casos unitarios de comparaciones con aumento, disminución, igualdad, referencia cero y cobertura insuficiente.
- [x] DOM15. Revisar que las nuevas operaciones mantengan unidades de potencia y no calculen energía mediante suma de muestras.
- [x] DOM16. Revisar que no se hayan alterado entidades, relaciones o invariantes existentes ni agregado dependencias técnicas a Domain.

### Control de cierre de Domain

- [x] DOM17. Ejecutar `dotnet build SolarUY.slnx`.
- [x] DOM18. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] DOM19. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.
- [x] DOM20. Corregir fallos en el código y repetir los controles afectados; registrar los tres resultados satisfactorios antes de continuar.

## 2. Application

### Contratos y autorización

- [x] APP01. Definir DTO propios para pregunta, historial, referencias de elementos, contexto de página y respuesta del asistente.
- [x] APP02. Definir DTO de consulta operativa con identificadores, pertenencias, fechas, unidades, origen simulado, cobertura y paginación.
- [x] APP03. Definir códigos distinguibles para éxito, sin datos, no encontrado, acceso denegado, argumentos inválidos, límite y fallo de dependencia.
- [x] APP04. Definir el puerto del modelo conversacional con mensajes, solicitudes de funciones y resultados, sin tipos del SDK de Gemini.
- [x] APP05. Definir el puerto del cliente MCP con descubrimiento, invocación y duración limitada a un turno, sin tipos del SDK MCP.
- [x] APP06. Definir `IConsultaOperativaRepository` con operaciones de solo lectura para jerarquía, últimas lecturas, historial y datos necesarios para resúmenes.
- [x] APP07. Completar el contrato de contexto de sesión con los datos simples necesarios para comprobar vigencia y vincular el historial a la sesión autenticada.
- [x] APP08. Implementar la comprobación de autenticación, vencimiento y rol Administrador/Operador antes de procesar preguntas y ejecutar cada consulta operativa.
- [x] APP09. Aplicar las restricciones existentes del usuario antes de buscar, contar o agregar elementos, sin aceptar identidad ni roles como argumentos del modelo.

### Consultas operativas

- [x] APP10. Crear `IConsultaOperativaService` y `ConsultaOperativaService` con dependencias inyectadas por constructor.
- [x] APP11. Implementar la validación de tipo de elemento, IDs, pertenencias, filtros, orden y parámetros de paginación.
- [x] APP12. Implementar la búsqueda de parques, nodos y paneles por nombre y pertenencia devolviendo candidatos con ruta completa para homónimos.
- [x] APP13. Implementar el detalle autorizado proyectando únicamente características y relaciones necesarias, sin endpoints internos o configuración privada.
- [x] APP14. Implementar el estado actual de panel, nodo, parque y ámbito global reutilizando las reglas de Domain.
- [x] APP15. Implementar la consulta histórica por elemento, período y estado con continuación explícita y orden estable por fecha UTC e ID.
- [x] APP16. Implementar los resúmenes por panel, nodo, parque y ámbito global usando todas las muestras del alcance solicitado.
- [x] APP17. Implementar comparaciones por elemento y período con diferencias, ordenamiento y filtros calculados en SolarUY.
- [x] APP18. Configurar los valores iniciales de 50 resultados por página, máximo 200, y hasta 100 IDs explícitos, sin limitar a 100 los elementos de un ámbito global.
- [x] APP19. Aplicar el máximo configurable inicial de 31 días por llamada y permitir la continuación en subperíodos con composición correcta de acumulados; comunicar cualquier parte pendiente por límite de ejecución.
- [x] APP20. Impedir que una página de muestras o elementos se utilice como total de un resumen sin declarar su alcance parcial.

### Tiempo y cobertura

- [x] APP21. Resolver los períodos mediante reloj inyectado y `America/Montevideo`, convirtiéndolos a intervalos UTC `[desde, hasta)`.
- [x] APP22. Resolver “hoy”, “ayer” y “mañana” conforme a los límites de specs, incluyendo días en curso e intervalos todavía vacíos.
- [x] APP23. Resolver “hoy contra ayer” con igual tramo transcurrido por defecto y conservar períodos completos cuando se soliciten expresamente.
- [x] APP24. Fijar un instante de referencia por turno y usarlo como límite para seleccionar últimas lecturas con desempate por ID.
- [x] APP25. Identificar lecturas antiguas con umbral configurable inicial de 60 segundos, conservando la fecha original.
- [x] APP26. Calcular cantidad, primera/última muestra y mayor hueco del período, incluidos los bordes, para informar cobertura observada.
- [x] APP27. Marcar cobertura incompleta por paneles faltantes o huecos mayores al umbral configurable inicial de 60 segundos.
- [x] APP28. Incorporar período local, zona horaria, unidades, advertencias y carácter simulado a los resultados entregados a las herramientas.

### Orquestación de la conversación

- [x] APP29. Crear `IAsistenteService` y `AsistenteService` para coordinar preguntas sin conservar conversación entre solicitudes.
- [x] APP30. Validar la marca de sesión y el contexto de página antes de utilizar el historial recibido.
- [x] APP31. Aplicar los límites iniciales configurables de 2.000 caracteres por pregunta, 30 mensajes y 32.000 caracteres de contexto.
- [x] APP32. Excluir del contexto recibido mensajes de sistema y resultados de herramientas aportados por el navegador; tratar mensajes y referencias como datos no confiables.
- [x] APP33. Preparar las instrucciones del asistente sobre solo lectura, aclaraciones, unidades, simulación, cobertura y separación de hechos e interpretaciones.
- [x] APP34. Descubrir el catálogo MCP y ejecutar el ciclo modelo → llamadas MCP → resultados → modelo conservando la correlación entre llamadas.
- [x] APP35. Rechazar herramientas desconocidas y argumentos fuera del esquema sin invocar repositorios ni operaciones alternativas.
- [x] APP36. Revalidar referencias de mensajes anteriores y volver a consultar datos mediante MCP para preguntas sobre actualidad.
- [x] APP37. Pedir aclaración ante homónimos, referencias descartadas por recorte de contexto o ambigüedad entre conversación y página.
- [x] APP38. Aplicar cancelación, máximo de ocho rondas, doce invocaciones MCP y 60 segundos por turno; distinguir agotamiento de límites de ausencia de datos.
- [x] APP39. Rechazar solicitudes de escritura con una explicación y restringir la obtención de datos operativos del asistente al puerto MCP.
- [x] APP40. Validar que los servicios dependan solo de Domain y contratos propios, con nombres y responsabilidades acordes a las reglas, sin MediatR.

### Control de cierre de Application

- [x] APP41. Ejecutar `dotnet build SolarUY.slnx`.
- [x] APP42. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] APP43. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.
- [x] APP44. Corregir fallos en el código y repetir los controles afectados; registrar los tres resultados satisfactorios antes de continuar.

## 3. Infrastructure

### Consultas de persistencia

- [x] INF01. Implementar `ConsultaOperativaRepository` con contextos por operación, consultas sin seguimiento y cancelación.
- [x] INF02. Implementar la búsqueda filtrada de jerarquía con orden estable y paginación parametrizada.
- [x] INF03. Implementar las proyecciones de detalle y pertenencia excluyendo datos técnicos privados.
- [x] INF04. Implementar las últimas lecturas por ámbito hasta el instante de referencia, conservando paneles sin medición y desempate estable.
- [x] INF05. Implementar el historial con intervalo semiabierto, filtro de estado y cursor por fecha UTC e ID.
- [x] INF06. Implementar la lectura por lotes o acumulados suficientes para estadísticas completas, sin aplicar el límite de una página al conjunto agregado.
- [x] INF07. Obtener los datos necesarios para detectar huecos y cobertura por panel, incluidos los bordes de cada intervalo.
- [x] INF08. Garantizar una vista consistente de los datos dentro de cada resumen o comparación, sin impedir nuevas escrituras del monitoreo.
- [x] INF09. Revisar el uso del índice existente por panel, fecha e ID y evitar consultas repetidas por cada elemento cuando sea posible consultar por conjunto.
- [x] INF10. Mantener los resultados de persistencia fallida diferenciados de un conjunto exitoso sin mediciones.

### Contexto autenticado y cliente MCP

- [x] INF11. Implementar el contexto de sesión a partir de la autenticación real del servidor, incluyendo emisión y vencimiento sin exponer credenciales.
- [x] INF12. Implementar una marca no secreta de sesión verificable en ambas instancias, vinculada a identidad, emisión y vencimiento, que no funcione como credencial.
- [x] INF13. Aislar el contexto autorizado por solicitud y conservar su comprobación de vigencia durante las llamadas MCP sin capturar un circuito anterior.
- [x] INF14. Seleccionar y fijar una versión compatible del SDK oficial MCP C# en el proyecto servidor.
- [x] INF15. Implementar el adaptador del cliente MCP para negociar, descubrir herramientas e invocarlas mediante streams.
- [x] INF16. Mantener una conexión MCP por pregunta y liberar cliente y transportes al completar, cancelar o fallar el turno.
- [x] INF17. Traducir resultados y errores MCP a contratos de Application, conservando códigos, correlación y límites.
- [x] INF18. Mantener el adaptador independiente de los handlers concretos de Presentation y de conexiones compartidas entre usuarios.

### Adaptador Gemini y configuración

- [x] INF19. Implementar el adaptador HTTPS de Gemini mediante `HttpClient`, con modelo y clave obtenidos solo de configuración privada del servidor.
- [x] INF20. Convertir el catálogo descubierto mediante MCP a declaraciones de funciones compatibles con Gemini.
- [x] INF21. Traducir llamadas y resultados de funciones conservando correlación, datos de protocolo y firmas requeridas dentro del turno.
- [x] INF22. Procesar la respuesta final y distinguir respuestas bloqueadas, incompletas o inválidas de respuestas válidas sin datos.
- [x] INF23. Definir opciones de habilitación, modelo `gemini-3.5-flash-lite` (cambio autorizado el 2026-11-05), límites y tiempos sin incluir valores secretos en archivos versionados.
- [x] INF24. Cargar esas opciones desde variables de entorno o archivo privado adicional preservando `SolarUY/appsettings.Local.json`.
- [x] INF25. Configurar el envío exclusivamente de texto y herramientas autorizadas, sin búsqueda web, ejecución de código o sesiones persistentes del proveedor.
- [x] INF26. Mapear `429`, errores del proveedor, timeout y cancelación a resultados comprensibles; conservar `Retry-After` cuando exista y evitar reintentos automáticos en bucle.
- [x] INF27. Deshabilitar el asistente con un estado informativo cuando falte configuración, sin impedir el arranque o modificar la salud habitual de SolarUY.
- [x] INF28. Incorporar logs de códigos, duración, cantidades y correlación sin cuerpos conversacionales, datos operativos sensibles o credenciales.
- [x] INF29. Adaptar la configuración de ejecución para ambas instancias usando las mismas fuentes privadas, sin incorporar secretos a Docker o archivos públicos del cliente.
- [x] INF30. Revisar que esta capa no incorpore tablas de conversación, migraciones innecesarias ni referencias concretas a Presentation.

La conexión completa de cliente y servidor MCP y su registro final se realizan en Presentation, cuando existan los handlers. Hasta entonces, mantener compilable el código y no activar servicios con dependencias todavía sin implementar.

### Control de cierre de Infrastructure

- [x] INF31. Ejecutar `dotnet build SolarUY.slnx`.
- [x] INF32. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] INF33. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.
- [x] INF34. Corregir fallos en el código y repetir los controles afectados; registrar los tres resultados satisfactorios antes de continuar.

## 4. Presentation

### Servidor MCP e integración del arranque

- [x] PRES01. Crear el servidor embebido y los adaptadores de herramientas en `SolarUY/Presentation/Asistente`, dependiendo únicamente de Application para las consultas.
- [x] PRES02. Exponer `BuscarElementos` con esquema de filtros, IDs y paginación y delegación a `IConsultaOperativaService`.
- [x] PRES03. Exponer `ObtenerDetalle` con tipo e ID y delegación a `IConsultaOperativaService`.
- [x] PRES04. Exponer `ConsultarEstadoActual` con ámbito, filtro de estado y paginación y delegación a `IConsultaOperativaService`.
- [x] PRES05. Exponer `ConsultarHistorial` con elemento, período, estado y cursor y delegación a `IConsultaOperativaService`.
- [x] PRES06. Exponer `ResumirMediciones` con ámbito, período y agrupación y delegación a `IConsultaOperativaService`.
- [x] PRES07. Exponer `CompararMediciones` con períodos, agrupación, orden y filtro y delegación a `IConsultaOperativaService`.
- [x] PRES08. Definir descripciones y esquemas de herramientas en español, sin parámetros que permitan suplantar identidad o permisos.
- [x] PRES09. Vincular cada ejecución MCP al contexto autorizado del turno y comprobar que el catálogo carezca de operaciones de escritura.
- [x] PRES10. Conectar streams de cliente y servidor por turno en la raíz de composición, con negociación y descubrimiento reales del protocolo.
- [x] PRES11. Registrar servicios, repositorios y adaptadores con ámbitos adecuados desde `Program.cs` o extensiones de arranque, sin dependencias concretas entre Infrastructure y Presentation.

### Endpoints y sesión

- [x] PRES12. Exponer el endpoint de sesión para Administrador y Operador con marca, vencimiento y disponibilidad del asistente.
- [x] PRES13. Exponer `POST /api/asistente/mensajes` con autorización y delegación a `IAsistenteService`.
- [x] PRES14. Aplicar validación antiforgery al POST y devolver estados de autenticación/autorización utilizables por el chat.
- [x] PRES15. Propagar cancelación del navegador al turno y devolver errores depurados sin detalles de excepciones o configuración.
- [x] PRES16. Implementar el módulo JavaScript de solicitudes al mismo origen, con cookie vigente y token antiforgery gestionado localmente.

### Interfaz y almacenamiento de conversación

- [x] PRES17. Crear `ChatAsistente.razor` e integrarlo desde `MainLayout` como componente interactivo de servidor independiente.
- [x] PRES18. Mostrar el chat a los roles autorizados y el aviso de indisponibilidad cuando corresponda, conservando los renderizados y el sensor existentes.
- [x] PRES19. Implementar la posición inferior derecha, despliegue hacia arriba y flecha para minimizar/restaurar.
- [x] PRES20. Incorporar historial, entrada de texto, envío e indicador de respuesta en curso.
- [x] PRES21. Evitar envíos simultáneos y duplicación del mensaje al reintentar un fallo.
- [x] PRES22. Implementar persistencia por pestaña en `sessionStorage` de mensajes, referencias y estado minimizado, ligada a la marca de sesión.
- [x] PRES23. Validar la sesión antes de restaurar o mostrar historial guardado, eliminándolo si no corresponde a la sesión actual.
- [x] PRES24. Restaurar conversación y estado visual después de navegar o recargar sin depender del circuito anterior.
- [x] PRES25. Implementar “Nueva conversación” para eliminar mensajes y referencias y cancelar el turno pendiente.
- [x] PRES26. Ignorar respuestas tardías de un turno perteneciente a una conversación descartada.
- [x] PRES27. Borrar almacenamiento y vista al cerrar sesión, vencerla, cambiarla o recibir `401/403`, comprobando vencimiento también al recuperar el foco.
- [x] PRES28. Capturar tipo e ID de parque, nodo o panel desde las rutas de detalle/monitoreo y congelarlos al enviar la pregunta.
- [x] PRES29. Limpiar el contexto al abandonar esas rutas y evitar enviar HTML o la URL completa al modelo.
- [x] PRES30. Renderizar las respuestas como texto escapado, manteniendo el contenido y las referencias temporales de mensajes anteriores.
- [x] PRES31. Mostrar errores distinguibles y reintento manual sin borrar la conversación, respetando la espera indicada cuando corresponda.
- [x] PRES32. Adaptar el panel a pantallas pequeñas y verificar navegación por teclado y anuncios accesibles de estado.
- [x] PRES33. Revisar que los componentes no calculen estadísticas, cobertura, permisos efectivos o estados de negocio ni accedan a Infrastructure.
- [x] PRES34. Conservar rutas relativas, origen HTTPS público, afinidad y Data Protection compartidos para operar con ambas instancias.
- [x] PRES35. Mostrar interrupción de un turno si cae su instancia y permitir recarga/reintento desde otra sin prometer recuperación de la respuesta en curso.

### Control de cierre de Presentation

- [x] PRES36. Ejecutar `dotnet build SolarUY.slnx`.
- [x] PRES37. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] PRES38. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.
- [x] PRES39. Corregir fallos en el código y repetir los controles afectados; registrar los tres resultados satisfactorios antes de continuar.

## 5. Verificación funcional y aceptación desde el chat

Las comprobaciones siguientes cubren los criterios numerados de `requirements.md`. Las herramientas auxiliares complementan la interacción real con SolarUY; no equivalen a aceptación externa. Mantener pendientes las casillas que requieran acceso o servicios reales todavía no comprobados.

### Preparación aislada y dependencias externas

- [x] FUN01. Preparar las herramientas funcionales necesarias bajo `arnes/prompting/tests/req009/`, sin crear proyectos de pruebas unitarias para Application, Infrastructure o Presentation.
- [x] FUN02. Identificar el destino aislado y validar su selección antes de cualquier preparación de datos o provocación de fallos.
- [x] FUN03. Preparar parques, nodos y paneles simulados con homónimos, distintos estados y resultados de referencia conocidos.
- [x] FUN04. Preparar mediciones con ceros reales, paneles sin muestras, lecturas antiguas, huecos y distinta cantidad de muestras por panel/período.
- [x] FUN05. Preparar un volumen que supere una página de elementos, una página de muestras y 100 paneles dentro de un mismo ámbito.
- [x] FUN06. Comprobar con la cuenta real la disponibilidad de Gemini 3.5 Flash-Lite por API en el nivel gratuito, sin activar facturación ni sustituir nuevamente el modelo; registrar cualquier impedimento. HTTP 200 real con y sin declaraciones MCP, mismo proyecto confirmado gratuito; ver cambio-modelo-20261105.md. La verificación anterior de 3.8 Flash se conserva como historial.
- [x] FUN07. Verificar autenticación OIDC real y acceso al chat desde el origen HTTPS público con ambas instancias.

### Escenarios de aceptación

- [x] FUN08. Verificar el acceso como Administrador y Operador y el rechazo de anónimo, otro rol y sesión vencida tanto en el chat como en el endpoint (criterio 1).
- [x] FUN09. Verificar el rechazo de una herramienta con contexto ausente/vencido y de intentos de suplantación mediante roles, IDs o mensajes enviados por el cliente (criterio 1).
- [x] FUN10. Verificar disponibilidad del chat en todas las páginas autenticadas, despliegue, minimización, envío e indicador de respuesta (criterio 2).
- [x] FUN11. Minimizar, navegar y recargar comprobando conservación de mensajes y referencias durante la misma sesión (criterio 3).
- [x] FUN12. Usar “Nueva conversación” con un turno pendiente y comprobar que una respuesta tardía no reponga el contexto eliminado (criterio 3).
- [x] FUN13. Cerrar sesión, dejarla vencer e iniciar otra sesión comprobando que el historial anterior no vuelva a mostrarse (criterio 3).
- [x] FUN14. Preguntar “¿Cómo está funcionando todo hoy?” y contrastar el resumen global con los datos conocidos, incluyendo paneles sin lecturas (criterios 5–9).
- [x] FUN15. Preguntar “¿Dónde está el panel llamado Panel 3?” y comprobar aclaración ante homónimos e identificación de su pertenencia (criterios 4 y 6).
- [x] FUN16. Preguntar “¿Qué paneles están en falla en el parque Norte?” y verificar estado de la última medición, fechas y distinción de lecturas antiguas (criterios 6, 8 y 9).
- [x] FUN17. Preguntar “¿Cómo estuvo generando este panel durante la mañana?” desde su monitoreo y verificar elemento, período local, unidades y cobertura (criterios 4, 6–9).
- [x] FUN18. Cambiar de panel y luego salir de su página comprobando actualización o descarte del contexto de página y aclaración de referencias ambiguas (criterio 4).
- [x] FUN19. Preguntar “¿Qué nodos tuvieron menor potencia promedio hoy que ayer?” y comprobar intervalos equivalentes, fórmula por panel y diferencias calculadas en SolarUY (criterios 5–8).
- [x] FUN20. Continuar con “De esos nodos, ¿cuáles tienen paneles en falla ahora?” tras cambiar mediciones aisladas y comprobar nuevas llamadas MCP con las referencias correctas (criterios 5 y 10).
- [x] FUN21. Verificar que las respuestas anteriores conserven contenido y fechas después de nuevas mediciones y preguntas (criterio 10).
- [x] FUN22. Consultar un período sin mediciones y comprobar que se diferencie de un período con potencia medida igual a cero (criterio 9).
- [x] FUN23. Consultar lecturas antiguas y cobertura parcial comprobando advertencias y ausencia de conclusiones globales injustificadas (criterios 8 y 9).
- [x] FUN24. Preguntar por una causa de falla no determinable y comprobar que se distingan hechos, interpretación y carácter simulado de las lecturas (criterios 8 y 9).
- [x] FUN25. Solicitar creación, modificación y eliminación y comprobar rechazo y ausencia de operaciones de escritura en el catálogo descubierto (criterio 11).
- [x] FUN26. Provocar una interrupción aislada de Gemini y verificar error comprensible, reintento y funcionamiento de las demás operaciones de SolarUY (criterio 12).
- [x] FUN27. Verificar el tratamiento de cuota `429` y espera indicada sin agotar deliberadamente la cuota habitual; identificar como simulada cualquier inyección de este fallo (criterio 12).
- [x] FUN28. Provocar un fallo MCP aislado y comprobar que no se informe como ausencia de datos y que se pueda reintentar (criterio 12).

### Límites, privacidad y balanceo

- [x] FUN29. Comprobar paginación sin duplicados ni omisiones y validar tamaños predeterminados, máximos e IDs explícitos.
- [x] FUN30. Comparar resúmenes globales con los datos de referencia cuando haya más de 100 paneles y más de una página, para descartar truncamientos silenciosos.
- [x] FUN31. Consultar más de 31 días mediante subperíodos y verificar composición de estadísticas y huecos entre tramos; comprobar aviso explícito si el presupuesto del turno impide completar la consulta.
- [x] FUN32. Verificar límites de mensaje, historial, rondas, invocaciones y tiempo, incluyendo referencia antigua fuera del contexto disponible.
- [x] FUN33. Enviar contenido que intente cambiar instrucciones o ejecutar HTML y comprobar respeto del catálogo, permisos y renderizado seguro.
- [x] FUN34. Verificar que configuración privada, cookies, tokens, endpoints internos y cuerpos de conversación no se expongan al cliente, al modelo o a logs indebidamente.
- [x] FUN35. Deshabilitar la configuración del asistente en el entorno aislado y comprobar arranque, aviso del chat y continuidad de consultas y monitoreo habituales.
- [x] FUN36. Atender turnos sucesivos desde ambas instancias por el balanceador, conservando sesión y conversación confirmada.
- [x] FUN37. Detener una instancia de prueba durante un turno y verificar interrupción, recuperación por recarga/reintento y conservación de los mensajes ya confirmados.
- [x] FUN38. Ejecutar las comprobaciones funcionales existentes de autenticación, consultas y monitoreo que cubran los puntos modificados, usando destino aislado cuando escriban datos.
- [x] FUN39. Registrar resultados por criterio con evidencias depuradas y separar pruebas reales, comprobaciones auxiliares, fallos simulados y aceptaciones externas pendientes.

## 6. Documentación y validación final

- [x] FIN01. Crear `arnes/prompting/requerimientos/req009_MCP/configuracion.md` con fuentes privadas, habilitación, modelo, requisitos de cuenta y ejecución en ambas instancias, sin secretos.
- [x] FIN02. Documentar uso del chat, contexto de página, nueva conversación, vencimiento, errores y reintentos.
- [x] FIN03. Documentar fórmulas, unidades, zona horaria, cobertura, simulación y valores configurables, distinguiéndolos de restricciones del proveedor.
- [x] FIN04. Documentar las condiciones de uso y tratamiento de datos aplicables al nivel gratuito y las limitaciones externas comprobadas.
- [x] FIN05. Documentar la preparación y reproducción de las verificaciones aisladas junto a sus herramientas en `arnes/prompting/tests/req009/`.
- [x] FIN06. Revisar el cambio completo para confirmar alcance, nomenclatura, responsabilidad única, inyección por constructor y fronteras de las cuatro capas.
- [x] FIN07. Confirmar mediante el diff que los diagramas, tests estructurales y configuración privada habitual permanecen sin modificaciones de req009.
- [x] FIN08. Revisar que no se incluyan secretos, evidencias privadas, bases de prueba o conversaciones persistidas entre los archivos a entregar.
- [x] FIN09. Ejecutar `dotnet build SolarUY.slnx` y registrar su resultado final.
- [x] FIN10. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar su resultado final.
- [x] FIN11. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado final de ArchUnitNET.
- [x] FIN12. Corregir los fallos encontrados en el código y repetir los controles afectados sin alterar las pruebas estructurales.
- [x] FIN13. Actualizar las casillas verificadas y entregar el resultado con advertencias preexistentes, limitaciones y comprobaciones externas pendientes explícitas.




