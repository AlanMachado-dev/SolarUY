# req005 — Plan de implementación: Detalle de Nodo

Fuentes: [requirements.md](requirements.md) y [specs.md](specs.md). Marcar cada tarea únicamente después de comprobar su resultado. Este documento planifica la implementación; todos los controles y aceptaciones comienzan pendientes.

Secuencia: **Preparación → Domain → Infrastructure → Application → Presentation → Aceptación y cierre**. Se prepara primero el método público de consulta en `NodoRepository`, con la firma definida en specs, y luego se incorpora a `INodoRepository`. Así, la ampliación de la interfaz encuentra una implementación real y la solución puede compilar al cerrar cada capa. Application continúa dependiendo exclusivamente de la abstracción; no se introducen implementaciones temporales ni cambios en las dependencias arquitectónicas.

No avanzar de capa hasta que compile la solución y pasen las suites completas de Domain y ArchUnitNET. Ante fallos, corregir el código y repetir los controles afectados; no modificar los tests estructurales para acomodar la implementación. Registrar resultados y distinguir advertencias preexistentes de errores nuevos.

No generar pruebas unitarias de Application, Infrastructure ni Presentation. Domain no cambia en este alcance. Los auxiliares funcionales, si se necesitan, van en `arnes/prompting/tests/req005/`; las evidencias depuradas, en `artifacts/req005/`, excluidas de Git. Cualquier preparación de datos o fallo controlado debe realizarse en un destino aislado identificado, conservando la base habitual y `SolarUY/appsettings.Local.json`.

## 0. Preparación

- [x] INI-01. Leer `AGENTS.md`, las siete reglas en orden, `requirements.md` y `specs.md`; contrastar el alcance con los dos diagramas de `.agents/Images` sin modificarlos.
- [x] INI-02. Registrar el estado inicial de Git e identificar los cambios previos que deben conservarse, especialmente los de `DetalleParqueSolar.razor` y `ListadoParquesSolares.razor`.
- [x] INI-03. Revisar los contratos y el mapeo actual de `NodoService`, la consulta del parque, las tarjetas de nodos y los flujos de autenticación y acceso denegado.
- [x] INI-04. Ejecutar `dotnet build SolarUY.slnx` y registrar el resultado inicial.
- [x] INI-05. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado inicial.
- [x] INI-06. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado inicial de ArchUnitNET; resolver fallos de código antes de implementar.

## 1. Domain

Ubicaciones de revisión: `SolarUY/Domain/Nodos/` y `SolarUY/Domain/ParquesSolares/`. Esta capa se reutiliza sin cambios.

- [x] DOM-01. Verificar que las navegaciones actuales permiten obtener parque, inversor y sensor desde un nodo, con las cardinalidades uno a uno previstas.
- [x] DOM-02. Verificar que `Sensor.Endpoint` admite ausencia y que no es necesario agregar propiedades ni métodos de negocio para consultar el detalle.
- [x] DOM-03. Confirmar que no se modificaron entidades, invariantes, cardinalidades ni diagramas y que no se incorporaron paneles ni su enum de estados.
- [x] DOM-04. Comprobar que los tres controles de preparación resultaron satisfactorios y corresponden al mismo código de Domain; si hubo cambios de código desde entonces, repetirlos antes de avanzar a Infrastructure.

## 2. Infrastructure

Ubicación: `SolarUY/Infrastructure/Persistencia/NodoRepository.cs`. La interfaz se amplía en la siguiente capa, una vez disponible la implementación real.

- [x] INF-01. Agregar el método público `Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)` a `NodoRepository`, reutilizando el contexto recibido por constructor.
- [x] INF-02. Implementar la búsqueda de un único nodo por ID sobre `Nodos` mediante `AsNoTracking()`.
- [x] INF-03. Cargar explícitamente la navegación `Parque` y la cadena `Inversor.Sensor` en esa consulta, sin cargar todos los nodos del parque ni usar carga diferida.
- [x] INF-04. Devolver `null` si no existe el nodo y propagar el token de cancelación a la operación de EF Core.
- [x] INF-05. Revisar que los fallos técnicos se propagan y que el método no escribe datos, no limpia el seguimiento del contexto ni altera el alta existente.
- [x] INF-06. Confirmar que no se necesitan migraciones, cambios de mapeos ni registros adicionales de dependencias en `SolarUY/Program.cs`.

### Control de salida de Infrastructure

- [x] INF-07. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] INF-08. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] INF-09. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Application.

## 3. Application

Ubicaciones: contratos y DTO en `SolarUY/Application/Nodos/`; servicio en `SolarUY/Application/Nodos/Servicios/NodoService.cs`.

- [x] APP-01. Crear `CodigoConsultaNodo` con `Exitoso`, `AutenticacionRequerida`, `AccesoDenegado` y `NoEncontrado`.
- [x] APP-02. Crear el record `ResultadoDetalleNodoDto` con `CodigoConsultaNodo Codigo` y `NodoDto? Nodo`.
- [x] APP-03. Incorporar a `INodoRepository` la firma `Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)`, coincidente con el método implementado en Infrastructure.
- [x] APP-04. Incorporar a `INodoService` la firma `Task<ResultadoDetalleNodoDto> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)` e implementar el método en `NodoService`.
- [x] APP-05. Rechazar la consulta sin autenticación mediante `AutenticacionRequerida`, sin datos del nodo ni acceso al repositorio.
- [x] APP-06. Rechazar al usuario sin rol exacto `Administrador` mediante `AccesoDenegado`, antes de evaluar existencia o consultar persistencia.
- [x] APP-07. Devolver `NoEncontrado` sin datos para `Guid.Empty`, después de las comprobaciones de autorización.
- [x] APP-08. Consultar el nodo mediante `INodoRepository.ObtenerPorIdAsync`, propagando el token de cancelación.
- [x] APP-09. Devolver `NoEncontrado` sin datos cuando el repositorio devuelva `null`.
- [x] APP-10. Devolver `Exitoso` con el `NodoDto` completo reutilizando el mapeo actual, incluido el `ParqueId` real y los datos de inversor y sensor.
- [x] APP-11. Verificar que todos los resultados distintos de `Exitoso` contienen `Nodo = null` y que los fallos técnicos no se convierten en nodo inexistente.
- [x] APP-12. Revisar que se conservan el alta, las dependencias inyectadas por constructor y los DTO existentes, sin referencias a EF Core, Blazor ni repositorios concretos.

### Control de salida de Application

- [x] APP-13. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] APP-14. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] APP-15. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Presentation.

## 4. Presentation

Ubicación: `SolarUY/Presentation/Pages/`. Mantener SSR y consumo exclusivo de servicios y DTO de Application.

### Página de detalle del nodo

- [x] UI-01. Crear `DetalleNodo.razor` con namespace `SolarUY.Presentation.Pages`, ruta `/nodos/{Id:guid}` y `[Authorize(Roles = "Administrador")]`.
- [x] UI-02. Definir el parámetro `Guid Id` e inyectar `INodoService` y las dependencias necesarias para navegación y diagnóstico, siguiendo los componentes existentes.
- [x] UI-03. Invocar `ObtenerPorIdAsync` desde `OnParametersSetAsync`, limpiando el resultado y el estado de error anteriores antes de cargar otro ID.
- [x] UI-04. Incorporar título de página, encabezado principal y secciones semánticas diferenciadas para Nodo, Inversor y Sensor.
- [x] UI-05. Mostrar nombre, latitud y longitud del nodo a partir del DTO, formateando las coordenadas con la cultura de la interfaz.
- [x] UI-06. Mostrar nombre y número de serie del inversor asociado.
- [x] UI-07. Mostrar nombre y número de serie del sensor asociado.
- [x] UI-08. Mostrar el endpoint como texto codificado por Razor o «Sin endpoint asociado» si está ausente, sin efectuar solicitudes al sensor.
- [x] UI-09. Agregar «Volver al parque» con apariencia de botón y destino construido a partir del `ParqueId` del resultado exitoso.
- [x] UI-10. Agregar «+ Agregar Panel Solar» como botón `type="button"` deshabilitado, sin manejador ni navegación, con una indicación breve de disponibilidad futura.
- [x] UI-11. Integrar `AutenticacionRequerida` y `AccesoDenegado` con los flujos existentes, sin renderizar datos ni acciones del detalle ante el rechazo.
- [x] UI-12. Mostrar «No se encontró el nodo» y regreso al listado para `NoEncontrado`, sin inventar un parque de regreso ni habilitar acciones del nodo.
- [x] UI-13. Mostrar «No se pudo cargar la información. Intentá nuevamente» ante un fallo técnico e incorporar una acción para recargar el mismo detalle.
- [x] UI-14. Emitir un diagnóstico depurado del fallo de carga, sin excepciones completas, SQL, endpoints, credenciales ni valores de sesión.
- [x] UI-15. Revisar que los IDs se usan únicamente como datos técnicos de navegación y que el componente no accede a repositorios ni contiene cálculos o decisiones de negocio.

### Navegación desde el detalle del parque

- [x] UI-16. Agregar en cada tarjeta de `DetalleParqueSolar.razor` un enlace real al nodo correcto, con nombre accesible basado en su nombre y visible únicamente para Administrador.
- [x] UI-17. Extender la superficie del enlace sobre toda la tarjeta mediante el marcado y CSS necesarios, sin controles interactivos anidados ni dependencia exclusiva de `onclick`.
- [x] UI-18. Incorporar un foco visible que identifique la tarjeta al navegar mediante teclado.
- [x] UI-19. Conservar la visualización actual de nodos para usuarios autenticados sin rol Administrador, sin habilitar los enlaces nuevos.
- [x] UI-20. Revisar que se mantienen los datos, orden, estados vacíos, alta de nodos y regreso al listado del parque, respetando los cambios previos del repositorio.

### Control de salida de Presentation

- [x] UI-21. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] UI-22. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] UI-23. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de la aceptación funcional.

## 5. Aceptación funcional y cierre

Las tareas siguientes requieren resultados observables sobre la implementación. Identificar por separado las comprobaciones auxiliares, las inspecciones del código y las sesiones reales. Una identidad simulada o una inspección no acreditan acceso externo real; si faltan accesos o entorno, mantener pendientes las verificaciones dependientes y registrar el motivo.

- [x] ACE-01. Identificar un destino aislado para req005 y disponer de al menos dos parques con nodos distintos, incluyendo sensores con endpoint informado y ausente; preparar datos únicamente en ese destino.
- [x] ACE-02. Disponer de acceso real como Administrador, usuario de otro rol y anónimo para validar permisos, sin guardar credenciales, tokens ni cookies en documentación o evidencias.
- [x] ACE-03. Como Administrador, comprobar los datos del nodo, inversor y sensor contra los datos persistidos, incluidos ambos casos de endpoint y coordenadas iguales a cero (CA1).
- [x] ACE-04. Hacer clic en nombre, datos y espacios libres de diferentes tarjetas y comprobar que cada una abre su nodo correspondiente (CA2).
- [x] ACE-05. Recorrer las tarjetas con Tab y activarlas con Enter; comprobar nombre accesible y foco visible (CA2).
- [x] ACE-06. Abrir directamente por URL nodos de distintos parques y comprobar que «Volver al parque» lleva siempre al parque real del nodo (CA3).
- [x] ACE-07. Comprobar el texto exacto «+ Agregar Panel Solar», su estado deshabilitado y la ausencia de navegación o escrituras (CA4).
- [x] ACE-08. Como anónimo, abrir la ruta directamente y comprobar el flujo de autenticación sin datos del detalle (CA5).
- [x] ACE-09. Como usuario autenticado sin rol Administrador, comprobar enlaces ausentes en el parque y acceso directo al nodo denegado, conservando la consulta actual del parque (CA5).
- [x] ACE-10. Comprobar mediante un auxiliar funcional, si es necesario, que Application rechaza al anónimo y al usuario sin rol antes de consultar persistencia, sin devolver datos. Identificar esta evidencia como auxiliar, separada de la aceptación real de permisos (CA5).
- [x] ACE-11. Acceder como Administrador a un GUID inexistente y a `Guid.Empty`; comprobar el mensaje de nodo no encontrado, el regreso al listado y la ausencia de acciones que requieran conocer el parque.
- [x] ACE-12. Probar un ID malformado en la URL y comprobar el manejo de rutas no encontradas existente.
- [x] ACE-13. Provocar un fallo de consulta únicamente en el entorno aislado; comprobar mensaje genérico, diagnóstico depurado y recarga exitosa tras resolver el fallo.
- [x] ACE-14. Navegar entre detalles de nodos y hacia un nodo inexistente; comprobar que no persisten datos ni mensajes de la consulta anterior.
- [x] ACE-15. Verificar el detalle y las tarjetas en escritorio y móvil, incluyendo nombres, números de serie y endpoints extensos, sin desbordes que impidan leer o navegar.
- [x] ACE-16. Comprobar en el destino aislado que consultar nodos no cambia datos persistidos ni genera solicitudes al endpoint del sensor.
- [x] ACE-17. Verificar la regresión de listado y detalle de parques, parque sin nodos y alta de nodos; cualquier escritura de esta comprobación debe permanecer en el destino aislado.
- [x] ACE-18. Revisar el diff final por alcance, SRP, nomenclatura, inyección por constructor y dependencias; comprobar que configuración privada, diagramas y tests estructurales se conservan.
- [x] ACE-19. Si la aceptación requiere corregir código, repetir compilación, Domain y ArchUnitNET de la capa afectada y las comprobaciones funcionales relacionadas antes de darla por validada.
- [x] ACE-20. Guardar un resumen depurado en `artifacts/req005/` con comandos ejecutados, resultados, advertencias y aceptación realmente realizada; comprobar su exclusión de Git.
- [x] ACE-21. Detener únicamente los procesos propios de verificación y retirar los recursos temporales aislados que ya no se necesiten, conservando las evidencias.
- [x] ACE-22. Actualizar los checkboxes según resultados verificables y declarar req005 completo solo cuando los controles pasen y los cinco criterios de aceptación estén acreditados; documentar cualquier tarea pendiente.

## Registro de ejecución — 28/10/2026

Implementación y aceptación completadas en el orden previsto. Domain se reutilizó sin cambios y los cambios previos que ocultan IDs se conservaron.

| Control de salida | Compilación | Domain | ArchUnitNET |
| --- | --- | --- | --- |
| Preparación / Domain | Correcta | 50/50 | 17/17 |
| Infrastructure | Correcta | 50/50 | 17/17 |
| Application | Correcta | 50/50 | 17/17 |
| Presentation | Correcta | 50/50 | 17/17 |

Los cuatro builds registraron 0 errores y los mismos 12 avisos NU1903 preexistentes. No se alteraron tests estructurales ni diagramas.

Aceptación real con sesiones OIDC de Administrador y Operador, además de anónimo: CA1–CA5 acreditados. Se verificaron clic en nombre/datos/espacio libre, Tab/Enter con foco visible, regreso desde nodos de distintos parques, botón reservado deshabilitado, estados inexistentes, ruta malformada, fallo real de consulta y recuperación. La comprobación de fallo renombró y restauró una tabla exclusivamente en el destino aislado. Una huella de todas las filas confirmó que las consultas reales no escribieron datos. Se comprobó la regresión del alta en ese mismo destino.

Auxiliares funcionales en `arnes/prompting/tests/req005/`: 56 comprobaciones con MySQL y SSR reales; sus identidades locales se registran separadamente de la aceptación OIDC. Escritorio real y móvil de 390×844 verificados, con textos extensos sin desbordes. No se requirieron correcciones del producto durante la aceptación (ACE-19).

Evidencias depuradas, comandos, advertencias y detalle de cada ACE en `artifacts/req005/resultados.md`, excluido de Git. Se conservaron configuración privada y base habitual; se detuvieron los procesos propios y se retiraron las tres bases auxiliares de req005. Sin verificaciones pendientes.