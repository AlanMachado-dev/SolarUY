# req006 — Plan de implementación: Alta de panel solar

Fuentes: [requirements.md](requirements.md) y [specs.md](specs.md). Los checkboxes registran la ejecución: se marcan únicamente después de implementar y verificar cada resultado. Las evidencias depuradas de esta implementación se guardan en `artifacts/req006/`.

Secuencia: **Preparación → Domain → Application → Infrastructure → Presentation → Aceptación y cierre**. Las capas siguen siendo carpetas y namespaces del servidor. No avanzar a la siguiente hasta que compile la solución y pasen las suites completas de Domain y ArchUnitNET. Corregir los fallos en el código y repetir los controles afectados; no alterar los tests estructurales para acomodar la implementación.

Crear pruebas unitarias únicamente para el comportamiento de Domain, junto con cada cambio. Las comprobaciones funcionales auxiliares, si se necesitan, se guardan en `arnes/prompting/tests/req006/`; las evidencias depuradas, en `artifacts/req006/`, excluidas de Git. Las migraciones, escrituras y fallos controlados se verifican en una base aislada identificada, conservando la base habitual y `SolarUY/appsettings.Local.json`.

## 0. Preparación

- [x] INI-01. Leer `AGENTS.md`, las siete reglas en orden, `requirements.md` y `specs.md`; contrastar las relaciones con los dos diagramas de `.agents/Images` sin modificarlos.
- [x] INI-02. Registrar el estado inicial de Git e identificar los cambios previos que deben conservarse, incluidos req005, la presentación de IDs, `Program.cs` y las herramientas existentes.
- [x] INI-03. Revisar `Nodo`, `ParqueSolar`, sus DTO y servicios, las consultas de repositorios y el formulario SSR de alta de nodos para reutilizar sus convenciones.
- [x] INI-04. Ejecutar `dotnet build SolarUY.slnx` y registrar el resultado inicial y las advertencias preexistentes.
- [x] INI-05. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado inicial.
- [x] INI-06. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado inicial de ArchUnitNET; resolver fallos de código antes de comenzar.
- [x] INI-07. Identificar el destino MySQL aislado para req006 y la configuración de prueba que lo selecciona, sin modificar `appsettings.Local.json` ni registrar valores privados.

## 1. Domain

Ubicaciones: `SolarUY/Domain/Paneles/`, `SolarUY/Domain/Nodos/Nodo.cs`, `SolarUY/Domain/ParquesSolares/ParqueSolar.cs` y `tests/SolarUY.Domain.Tests/`. Cada tarea de comportamiento incluye sus pruebas xUnit en esta misma etapa.

### Nombre y validaciones

- [x] DOM-01. Crear `EstadoPanel` con `Operativo`, `EnFalla`, `EnMantenimiento` y `Desactivado`, asignando valores estables para su persistencia.
- [x] DOM-02. Crear `DatosPanelInvalidosException` con campo y mensaje, identificando el conflicto de nombre de forma diferenciada para que Application pueda traducirlo sin analizar texto libre.
- [x] DOM-03. Implementar `NombrePanel` con recorte de extremos, normalización Unicode Form C y clave mediante `ToUpperInvariant()`; probar equivalencia de mayúsculas y conservación de acentos y espacios internos.
- [x] DOM-04. Rechazar nombres nulos, vacíos o de espacios y valores o claves que superen 100 caracteres Unicode; probar el límite válido de 100 y el rechazo de 101, incluidos caracteres suplementarios.

### Panel y pertenencia

- [x] DOM-05. Crear `Panel` con identidad, nombre, modelo, potencia nominal, fecha de instalación, estado y referencia obligatoria a `Nodo`; proteger construcción y mutación mediante los patrones actuales.
- [x] DOM-06. Generar el `Guid` del panel y asignar siempre `Operativo` al crearlo; probar identidad no vacía y distinta entre altas, sin recibir identidad o estado editables.
- [x] DOM-07. Validar y recortar el modelo obligatorio; probar nulo, vacío, espacios y un modelo válido sin imponer catálogo ni longitud funcional adicional.
- [x] DOM-08. Validar potencia `float` finita y mayor que cero; probar valores positivos, cero, negativos, NaN e infinitos.
- [x] DOM-09. Recibir el instante UTC proporcionado por Application y truncarlo a segundos; probar un instante con fracción de segundo para demostrar que no se redondea.
- [x] DOM-10. Agregar a `Nodo` la colección privada de paneles con exposición de solo lectura; probar que el consumidor no puede modificarla directamente.
- [x] DOM-11. Implementar `Nodo.AgregarPanel` para crear e incorporar un panel con pertenencia bidireccional consistente; probar la referencia al nodo y el rechazo de una creación sin nodo.
- [x] DOM-12. Rechazar nombres con la misma clave dentro del nodo antes de incorporar el panel; probar que un intento inválido no modifica la colección ni los paneles existentes.
- [x] DOM-13. Probar que nodos distintos admiten el mismo nombre y que agregar un panel a uno no afecta la colección del otro.
- [x] DOM-14. Exponer `GeneracionActualW` y `UltimoMantenimiento` como derivados anulables de solo lectura; probar `null` en un panel sin historial, sin crear entidades ni registros ficticios de medición o mantenimiento.

### Cantidades y capacidad

- [x] DOM-15. Sustituir el cero fijo de `Nodo.CantidadPaneles` por el tamaño de su colección; probar nodo vacío y cantidad posterior a varias altas válidas y una rechazada.
- [x] DOM-16. Calcular `ParqueSolar.CapacidadTotalEstimadaMw` desde las potencias nominales de todos sus paneles, convirtiendo W a MW y conservando la salida `decimal`; probar parque vacío, nodos sin paneles y varios nodos con paneles de potencias distintas.
- [x] DOM-17. Verificar con pruebas que el cálculo usa potencia nominal y no generación medida, y conservar las pruebas existentes que esperan cero cuando no hay paneles.
- [x] DOM-18. Revisar que nuevas entidades y objetos de valor no exponen setters públicos, FKs editables ni dependencias externas; comprobar que no se agregan operaciones de edición, cambio de estado o históricos.

### Control de salida de Domain

- [x] DOM-19. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] DOM-20. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar la suite completa, incluidas las nuevas pruebas.
- [x] DOM-21. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Application; registrar resultados y advertencias.

## 2. Application

Ubicaciones: contratos en `SolarUY/Application/Paneles/`, servicio en `SolarUY/Application/Paneles/Servicios/` y ampliación de `SolarUY/Application/Nodos/`. Mantener dependencias exclusivamente hacia Domain y abstracciones de Application.

### Contratos

- [x] APP-01. Crear `CrearPanelDto` con `Nombre`, `Modelo` y `float? PotenciaNominalW`, sin campos de identidad, nodo, fecha, estado ni derivados.
- [x] APP-02. Crear `PanelDto` con ID, NodoId, nombre como texto, modelo, potencia nominal, fecha de instalación, estado y derivados anulables.
- [x] APP-03. Crear `ResultadoAltaPanelDto` y sus errores con código, mensaje y campo; incluir `AutenticacionRequerida`, `AccesoDenegado`, `NodoNoEncontrado`, `DatosInvalidos` y `NombreDuplicado`.
- [x] APP-04. Definir `IPanelRepository.GuardarAsync(Panel panel, CancellationToken cancellationToken = default)` y sus resultados `Guardado`, `ConflictoNombre` y `NodoNoEncontrado`.
- [x] APP-05. Definir `IPanelService.CrearAsync(Guid nodoId, CrearPanelDto datos, CancellationToken cancellationToken = default)` con resultado `ResultadoAltaPanelDto`.

### Servicio de alta

- [x] APP-06. Crear `PanelService` con `INodoRepository`, `IPanelRepository`, `IUsuarioActualService` y `TimeProvider` recibidos por constructor.
- [x] APP-07. Rechazar anónimos y usuarios sin rol exacto `Administrador` antes de consultar repositorios o evaluar la existencia del nodo.
- [x] APP-08. Devolver `NodoNoEncontrado` para `Guid.Empty` o para un nodo ausente al consultar `INodoRepository.ObtenerPorIdAsync`, sin crear nodos ni equipos.
- [x] APP-09. Distinguir potencia ausente de cero en el DTO y obtener el instante actual UTC desde `TimeProvider` para invocar `Nodo.AgregarPanel`.
- [x] APP-10. Traducir las validaciones de Domain a errores del campo correspondiente, distinguiendo `NombreDuplicado` de `DatosInvalidos` y conservando las reglas en Domain.
- [x] APP-11. Guardar mediante `IPanelRepository` y traducir sus resultados de conflicto de nombre o nodo eliminado; propagar la cancelación y los fallos técnicos inesperados.
- [x] APP-12. Devolver el panel creado únicamente tras `Guardado`; garantizar que los resultados rechazados no incluyen un panel ni anuncian éxito.

### Consulta de paneles del nodo

- [x] APP-13. Extender `ResultadoDetalleNodoDto` con `IReadOnlyList<PanelDto> Paneles`, garantizando una colección no nula y compatibilidad de construcción con los consumidores actuales para mantener compilable la etapa.
- [x] APP-14. Ampliar el mapeo de `NodoService.ObtenerPorIdAsync` para incluir solo los paneles de ese nodo, ordenados por nombre y luego ID, sin agregar una consulta que eluda la autorización existente.
- [x] APP-15. Garantizar listas vacías y ausencia de datos del nodo en respuestas de autenticación requerida, acceso denegado o nodo no encontrado.
- [x] APP-16. Revisar que `NodoDto` y `ParqueSolarDto` conservan sus contratos y obtienen cantidades y capacidad de Domain; documentar que la carga de paneles se completa en Infrastructure antes de la aceptación funcional.
- [x] APP-17. Revisar SRP, nomenclatura y dependencias del servicio, sin reglas de negocio duplicadas, `DbContext`, SQL, Blazor ni repositorios concretos.

### Control de salida de Application

- [x] APP-18. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa, sin implementaciones temporales para superar el control.
- [x] APP-19. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] APP-20. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Infrastructure; registrar resultados y advertencias.

## 3. Infrastructure

Ubicaciones: `SolarUY/Infrastructure/Persistencia/`, sus migraciones y el punto de composición `SolarUY/Program.cs`.

### Mapeo y consultas

- [x] INF-01. Crear `PanelConfiguration` para la tabla `Paneles`, con PK `ID`, conversión de `Guid` a texto existente y sin generación de identidad adicional en MySQL.
- [x] INF-02. Mapear la relación obligatoria con `Nodo` mediante FK `IdNodo`, colección por campo privado y eliminación en cascada, sin agregar una FK editable al dominio.
- [x] INF-03. Mapear `NombrePanel` como objeto poseído en la misma tabla, con columnas `Nombre` y `ClaveNombre` de tipo `varchar(100)` y collation `utf8mb4_0900_bin` para la clave.
- [x] INF-04. Crear el índice único compuesto `UX_Paneles_IdNodo_ClaveNombre` sobre `(IdNodo, ClaveNombre)`, verificando que los dos campos se mapean a las columnas compartidas correctas del objeto poseído y su propietario.
- [x] INF-05. Mapear modelo y potencia como obligatorios, fecha como `datetime(0)` con tratamiento UTC consistente al guardar y materializar, y estado con los valores estables del enum.
- [x] INF-06. Ignorar en EF los derivados del panel, cantidad de paneles y capacidad del parque; registrar `DbSet<Panel>` y la configuración en `SolarUyDbContext`.
- [x] INF-07. Ampliar `NodoRepository.ObtenerPorIdAsync` para cargar explícitamente `Paneles`, conservando parque, inversor y sensor y el comportamiento de solo lectura.
- [x] INF-08. Ampliar `ParqueSolarRepository.ListarAsync` para cargar `Nodos.Paneles` antes de calcular capacidades, manteniendo el orden y permisos existentes.
- [x] INF-09. Ampliar `ParqueSolarRepository.ObtenerPorIdAsync` para cargar `Nodos.Paneles`, conservando las cargas del inversor y sensor necesarias para el detalle.

### Guardado y composición

- [x] INF-10. Implementar `PanelRepository` con el contexto recibido por constructor y una operación de guardado que marque como nuevo únicamente el panel y su nombre poseído, conservando el nodo existente sin modificaciones.
- [x] INF-11. Confirmar el guardado de forma atómica y descartar o limpiar el seguimiento pendiente ante fallos, sin reinsertar parque, nodo, equipos ni paneles previos.
- [x] INF-12. Traducir exclusivamente la violación de `UX_Paneles_IdNodo_ClaveNombre` a `ConflictoNombre` y la pérdida de la FK de nodo a `NodoNoEncontrado`; propagar otros fallos técnicos.
- [x] INF-13. Registrar `IPanelRepository`, `IPanelService` y `TimeProvider.System` en `Program.cs` con las duraciones apropiadas, conservando los registros, autenticación y filtros de diagnóstico existentes.

### Migración y verificación aislada

- [x] INF-14. Generar la migración incremental y actualizar el snapshot para incorporar solo el esquema de paneles; revisar que no borra ni recrea tablas existentes ni agrega históricos o columnas derivadas.
- [x] INF-15. Aplicar la migración en el destino aislado con datos previos y comprobar que se conservan parques, nodos y equipos y que los nodos existentes comienzan sin paneles.
- [x] INF-16. Verificar el guardado y lectura en una nueva solicitud: identidad, pertenencia, nombre, potencia, fecha hasta segundos en UTC y estado `Operativo` persistidos correctamente.
- [x] INF-17. Verificar en MySQL aislado dos altas concurrentes con nombre equivalente en el mismo nodo: una sola confirmada y la otra con conflicto; repetir con nombres diferentes y comprobar que ambas se conservan.
- [x] INF-18. Verificar que el mismo nombre en nodos distintos se guarda sin conflicto y que las consultas no mezclan paneles entre nodos o parques.
- [x] INF-19. Provocar en el destino aislado la desaparición del nodo antes de guardar y un fallo técnico de escritura; comprobar resultados diferenciados y ausencia de registros parciales o cambios en el grafo previo.
- [x] INF-20. Verificar que cantidad y capacidad de los DTO se corresponden con los paneles persistidos al consultar nodos, listado de parques y detalle del parque.

### Control de salida de Infrastructure

- [x] INF-21. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] INF-22. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] INF-23. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Presentation; registrar resultados y advertencias.

## 4. Presentation

Ubicación: `SolarUY/Presentation/Pages/`. Mantener SSR y consumir exclusivamente servicios y DTO de Application.

### Formulario de alta

- [x] UI-01. Reemplazar el botón deshabilitado de `DetalleNodo.razor` por el enlace «+ Agregar Panel Solar» a `/nodos/{NodoId:guid}/paneles/nuevo` y retirar el aviso de disponibilidad futura.
- [x] UI-02. Crear `AltaPanelSolar.razor` con namespace `SolarUY.Presentation.Pages`, esa ruta y `[Authorize(Roles = "Administrador")]`, protegiendo carga y POST.
- [x] UI-03. Obtener el nodo mediante `INodoService.ObtenerPorIdAsync` y mostrar su nombre como destino; limpiar los datos anteriores al cambiar el parámetro de ruta.
- [x] UI-04. Crear el formulario SSR con nombre explícito, vinculación compatible con POST y antifalsificación existente.
- [x] UI-05. Incorporar únicamente Nombre, Modelo y Potencia nominal (W) como entradas obligatorias, con etiquetas asociadas y mensajes accesibles; tratar la conversión numérica con la cultura de la interfaz.
- [x] UI-06. Invocar `IPanelService.CrearAsync` con el nodo de la ruta y el DTO de entrada, sin vincular ID, nodo, fecha, estado ni derivados desde el formulario.
- [x] UI-07. Mostrar errores de validación junto al campo y conservar las entradas; usar «Ya existe un panel solar con ese nombre en este nodo.» para duplicados.
- [x] UI-08. Integrar los rechazos de autenticación y rol con los flujos actuales, sin mostrar datos del nodo ni habilitar el formulario ante rechazo.
- [x] UI-09. Mostrar «No se encontró el nodo» con regreso al listado para un destino inexistente, tanto al cargar como si desaparece al guardar; no conservar el formulario anterior.
- [x] UI-10. Mostrar errores técnicos generales de carga y guardado con reintento y diagnóstico depurado, sin convertirlos en un listado vacío ni anunciar éxito.
- [x] UI-11. Incorporar «Registrar panel solar» y «Cancelar»; cancelar regresa al mismo nodo sin invocar el alta.
- [x] UI-12. Tras confirmación, redirigir al detalle del nodo después del POST y recargar desde persistencia, mostrando «Panel solar registrado»; impedir que actualizar la página reenvíe el formulario.

### Lista en el detalle del nodo

- [x] UI-13. Agregar la sección «Paneles solares» y renderizar los paneles recibidos de Application, conservando el orden del DTO y sin filtrar ni calcular reglas de negocio en Razor.
- [x] UI-14. Mostrar nombre, modelo, potencia nominal en W, fecha de instalación hasta segundos con referencia UTC y estado de cada panel; formatear números según la cultura de la interfaz.
- [x] UI-15. Mostrar generación actual y último mantenimiento mediante los DTO; ante `null`, presentar «Sin mediciones» y «Sin mantenimientos» sin inventar valores.
- [x] UI-16. Mostrar «Este nodo todavía no tiene paneles solares» exclusivamente para una consulta exitosa sin paneles; no mostrar IDs como contenido.
- [x] UI-17. Conservar las secciones de nodo, inversor y sensor, el regreso al parque, los estados existentes y la protección del detalle, con presentación legible en escritorio y móvil.
- [x] UI-18. Revisar que las páginas solo capturan eventos, invocan servicios y muestran resultados, sin repositorios, entidades construidas en UI, cálculos de capacidad ni reglas de unicidad.

### Control de salida de Presentation

- [x] UI-19. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] UI-20. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] UI-21. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de la aceptación funcional; registrar resultados y advertencias.

## 5. Aceptación funcional y cierre

Verificar la aplicación real contra el destino aislado y registrar el origen de cada evidencia. Las pruebas auxiliares no acreditan por sí solas una sesión externa real. Si falta acceso o entorno para una aceptación, conservar su checkbox pendiente y documentar el motivo.

- [x] ACE-01. Disponer de datos aislados con dos parques, varios nodos y casos con cero y varios paneles; identificar qué datos se preparan para cada aceptación.
- [x] ACE-02. Disponer de sesiones reales de Administrador, usuario de otro rol y anónimo para comprobar permisos, sin guardar credenciales, tokens ni cookies en evidencias.
- [x] ACE-03. Como Administrador, abrir el alta desde el nodo, registrar un panel y cotejar los datos persistidos: tres entradas, identidad autogenerada, fecha UTC actual hasta segundos, estado `Operativo` y nodo correcto (CA1 y CA3).
- [x] ACE-04. Probar campos vacíos, nombre con más de 100 caracteres, potencia ausente, cero y negativa; comprobar errores de campo, conservación de entradas y ausencia de altas (CA1).
- [x] ACE-05. Probar nombre repetido con cambios de mayúsculas o espacios externos en el mismo nodo y el mismo nombre en otro nodo; comprobar rechazo local y aceptación en el segundo (CA1).
- [x] ACE-06. Verificar que valores manipulados de ID, pertenencia, fecha o estado en el POST no controlan los datos del alta y que una ruta a un nodo inexistente no crea un destino automáticamente (CA1 y CA2).
- [x] ACE-07. Verificar como anónimo el acceso directo y el envío al alta, y comprobar el flujo de autenticación sin creación ni exposición de datos del detalle (CA2).
- [x] ACE-08. Verificar como usuario sin rol Administrador el acceso directo y POST manipulado al alta y el detalle; comprobar acceso denegado y conservar sus permisos actuales de consulta de parques (CA2).
- [x] ACE-09. Comprobar mediante un auxiliar funcional que Application rechaza anónimo y usuario sin rol antes de consultar persistencia; registrar esta evidencia por separado de las sesiones reales (CA2).
- [x] ACE-10. Verificar «Cancelar», regreso al nodo correcto tras éxito y recarga posterior sin nuevo POST ni alta duplicada (CA3).
- [x] ACE-11. Verificar la lista vacía y la lista con varios paneles tras recargar, sin paneles ajenos; comprobar orden, todos los campos, estados y ausencia de IDs visibles (CA4).
- [x] ACE-12. Comprobar «Sin mediciones» y «Sin mantenimientos», sin ceros o fechas ficticias y sin tablas ni registros de históricos creados por el alta (CA1 y CA4).
- [x] ACE-13. Verificar que la cantidad del nodo y la capacidad del parque se actualizan en sus vistas después del alta, sin modificar las de otro parque (consistencia).
- [x] ACE-14. Probar `Guid.Empty`, GUID inexistente e ID mal formado; comprobar estados y navegación correspondientes, sin datos anteriores ni formulario para un nodo ausente.
- [x] ACE-15. Provocar un fallo técnico de carga y otro de guardado únicamente en el destino aislado; comprobar mensajes generales, reintento, recuperación y diagnósticos sin datos privados.
- [x] ACE-16. Verificar la navegación completa con teclado, etiquetas, foco visible y mensajes; comprobar escritorio y móvil, incluyendo nombres y modelos extensos sin desbordes que impidan operar.
- [x] ACE-17. Verificar regresión de listado y detalle de parques, detalle de nodos y alta de nodos; cotejar que se conservan parques, equipos y paneles previos, con escrituras solo en el destino aislado.
- [x] ACE-18. Revisar el diff final por alcance, SRP, nomenclatura y dependencias; comprobar que se conservan diagramas, tests estructurales, configuración privada y cambios previos del usuario.
- [x] ACE-19. Ante correcciones durante la aceptación, repetir los tres controles obligatorios de la capa afectada y las comprobaciones funcionales relacionadas antes de considerarla validada.
- [x] ACE-20. Guardar en `artifacts/req006/` un resumen depurado de resultados por capa, comandos, advertencias, destino aislado sin secretos y aceptaciones realmente ejecutadas; verificar exclusión de Git.
- [x] ACE-21. Detener únicamente los procesos propios de verificación y retirar recursos temporales aislados que ya no se necesiten, conservando las evidencias.
- [x] ACE-22. Actualizar los checkboxes según resultados verificables y declarar req006 completo solo cuando estén acreditados CA1–CA4, los controles y las verificaciones de consistencia; documentar cualquier pendiente.


## Registro de cierre — 29/10/2026

Implementación y verificación completadas por capas. Compilación correcta; Domain 66/66 y ArchUnitNET 17/17 en cada control de salida y en el cierre final. Persisten únicamente las advertencias NU1903 preexistentes.

Migración incremental validada en MySQL aislado, con conservación de datos, unicidad concurrente, FK, rollback y totales. Aceptación real con sesiones OIDC iniciadas manualmente por el usuario: Administrador (43 comprobaciones), Operador (13) y anónimo. Navegación, Cancelar, teclado, foco, escritorio y móvil verificados en el navegador. Los auxiliares con identidades locales se registran por separado y no se presentan como sesiones externas.

Evidencias depuradas y comandos: `artifacts/req006/resultados.md` y archivos asociados, excluidos de Git. Auxiliares reproducibles: `arnes/prompting/tests/req006/README.md`. Se detuvieron los procesos propios y se retiraron los destinos temporales de prueba. Diagramas, tests estructurales, configuración privada, base habitual y cambios previos del usuario conservados. La migración no se aplicó a la base habitual.
