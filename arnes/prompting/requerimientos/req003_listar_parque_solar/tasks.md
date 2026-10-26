# req003 — Plan de implementación: Listar Parque Solar

Fuentes: [requirements.md](requirements.md) y [specs.md](specs.md). El diseño disponible se llama `specs.md`; la referencia a `spec.md` se interpreta como ese documento.

Las tareas están agrupadas por capa. Marcar cada checkbox únicamente después de verificar su resultado. Este plan no acredita implementación ni aceptación funcional.

## Secuencia y controles

Ejecutar en este orden: **Preparación → Domain → Application A → Infrastructure A → Application B → Infrastructure B → Presentation → Aceptación y cierre**.

La división en etapas permite conservar la compilación: primero se definen los resultados de Application; después se incorporan los métodos públicos de lectura al repositorio concreto, todavía sin ampliar su interfaz; finalmente se amplía la interfaz y se implementan los casos de uso. No utilizar implementaciones ficticias, métodos que lancen `NotImplementedException` ni miembros predeterminados de interfaz para superar los controles.

Al cerrar cada etapa, ejecutar los tres controles indicados desde la raíz. Ante un fallo, corregir el código y repetirlos antes de continuar. No modificar `CodeQuality.Tests` para acomodar la implementación. Registrar los resultados y distinguir advertencias preexistentes de errores nuevos. No generar pruebas unitarias de Application, Infrastructure ni Presentation.

## 0. Preparación

- [x] PRE-01. Leer `AGENTS.md`, las siete reglas en su orden, `requirements.md` y `specs.md`; confirmar el alcance y respetar los diagramas de `.agents/Images` sin modificarlos.
- [x] PRE-02. Revisar el estado de Git e identificar cambios previos para conservarlos durante la implementación.
- [x] PRE-03. Ejecutar `dotnet build SolarUY.slnx` y registrar el estado inicial.
- [x] PRE-04. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el estado inicial.
- [x] PRE-05. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el estado inicial de ArchUnitNET.

## 1. Domain

**Dependencia:** preparación verificada. No se prevén cambios de código en esta capa.

- [x] DOM-01. Verificar que `ParqueSolar` expone `ID`, `Nombre.Valor`, `Ubicacion` y `CapacidadTotalEstimadaMw` para alimentar las consultas sin modificar sus invariantes.
- [x] DOM-02. Confirmar que la capacidad actual se obtiene del dominio como `0m`, sin incorporar topología ni un dato de capacidad editable o persistido.
- [x] DOM-03. Confirmar que req003 no requiere cambios de comportamiento en Domain. Si resultaran necesarios, incorporar simultáneamente las pruebas xUnit correspondientes antes de cerrar esta etapa.
- [x] DOM-04. Ejecutar `dotnet build SolarUY.slnx` y verificar compilación exitosa.
- [x] DOM-05. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar resultado satisfactorio.
- [x] DOM-06. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar ArchUnitNET antes de continuar.

## 2. Application

### Etapa A — Contratos de resultado

**Dependencia:** Domain verificado. Ubicación: `SolarUY/Application/ParquesSolares/`.

- [x] APP-A01. Crear `CodigoConsultaParqueSolar` con los valores `Exitoso`, `AutenticacionRequerida` y `NoEncontrado`.
- [x] APP-A02. Crear `ResultadoListadoParquesSolaresDto` con código de resultado y colección de `ParqueSolarDto`; representar un rechazo con colección vacía.
- [x] APP-A03. Crear `ResultadoDetalleParqueSolarDto` con código de resultado y `ParqueSolarDto?`; representar un rechazo o ausencia con parque nulo.
- [x] APP-A04. Verificar que los nuevos contratos reutilizan `ParqueSolarDto` y utilizan únicamente tipos permitidos en Application.
- [x] APP-A05. Ejecutar `dotnet build SolarUY.slnx` y verificar compilación exitosa.
- [x] APP-A06. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar resultado satisfactorio.
- [x] APP-A07. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar ArchUnitNET antes de pasar a Infrastructure A.

### Etapa B — Contratos de consulta y casos de uso

**Dependencia:** Infrastructure A verificada. Mantener las dependencias existentes por constructor de `ParqueSolarService`.

- [x] APP-B01. Ampliar `IParqueSolarRepository` con `Task<IReadOnlyList<ParqueSolar>> ListarAsync(CancellationToken cancellationToken = default)`, coincidiendo con el método concreto preparado en Infrastructure A.
- [x] APP-B02. Ampliar `IParqueSolarRepository` con `Task<ParqueSolar?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)`, coincidiendo con el método concreto preparado.
- [x] APP-B03. Ampliar `IParqueSolarService` con `Task<ResultadoListadoParquesSolaresDto> ListarAsync(CancellationToken cancellationToken = default)` e implementar el método en `ParqueSolarService`: rechazar usuarios anónimos antes de consultar el repositorio, leer parques y mapear los cuatro datos al DTO existente.
- [x] APP-B04. Ampliar `IParqueSolarService` con `Task<ResultadoDetalleParqueSolarDto> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)` e implementar el método en `ParqueSolarService`: comprobar autenticación antes de consultar y devolver el DTO o `NoEncontrado`.
- [x] APP-B05. Revisar que las consultas no exigen rol, que los rechazos no contienen datos y que un listado vacío es exitoso.
- [x] APP-B06. Revisar que un ID sin coincidencia, incluido `Guid.Empty`, produce `NoEncontrado` y nunca crea un parque.
- [x] APP-B07. Verificar que ambos casos de uso propagan la cancelación y los fallos técnicos, sin traducir errores de persistencia a ausencia de datos.
- [x] APP-B08. Verificar que `CrearAsync` conserva su comprobación de Administrador y su comportamiento previo.
- [x] APP-B09. Revisar nombres, responsabilidad de los métodos y dependencias: sin EF Core, `HttpContext`, Blazor ni implementaciones concretas de Infrastructure en Application.
- [x] APP-B10. Ejecutar `dotnet build SolarUY.slnx` y verificar compilación exitosa.
- [x] APP-B11. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar resultado satisfactorio.
- [x] APP-B12. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar ArchUnitNET antes de pasar a Infrastructure B.

## 3. Infrastructure

### Etapa A — Lecturas del repositorio

**Dependencia:** Application A verificada. Ubicación: `SolarUY/Infrastructure/Persistencia/ParqueSolarRepository.cs`. Agregar los métodos públicos con sus firmas definitivas; Application B los incorporará a la interfaz existente.

- [x] INF-A01. Implementar `ListarAsync` como `Task<IReadOnlyList<ParqueSolar>>`, usando `AsNoTracking`, orden por `Nombre.Valor` y desempate por `ID`.
- [x] INF-A02. Materializar el listado mediante una operación EF Core asíncrona, con `CancellationToken cancellationToken = default`, sin exponer `IQueryable`.
- [x] INF-A03. Implementar `ObtenerPorIdAsync` como `Task<ParqueSolar?>`, filtrando por ID con `AsNoTracking`, operación asíncrona y token de cancelación.
- [x] INF-A04. Revisar que ambas consultas materializan `Nombre` mediante el mapeo existente y que el detalle devuelve `null` cuando no encuentra el ID.
- [x] INF-A05. Verificar que las consultas no ejecutan escrituras, no cambian el esquema y mantienen la capacidad excluida del mapeo EF Core.
- [x] INF-A06. Ejecutar `dotnet build SolarUY.slnx` y verificar compilación exitosa.
- [x] INF-A07. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar resultado satisfactorio.
- [x] INF-A08. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar ArchUnitNET antes de retomar Application B.

### Etapa B — Integración y destino del ingreso

**Dependencia:** Application B verificada. La composición se mantiene en `SolarUY/Program.cs`.

- [x] INF-B01. Verificar que los registros existentes de `IParqueSolarRepository`, `IParqueSolarService` e `IUsuarioActualService` resuelven las implementaciones ampliadas sin crear registros duplicados.
- [x] INF-B02. Establecer `AuthenticationProperties.RedirectUri = "/parques-solares"` en `POST /cuenta/autenticar`, sin seleccionar el destino desde `ReturnUrl` enviado por el navegador.
- [x] INF-B03. Revisar que el cambio conserva antifalsificación, límite de intentos, validaciones OIDC, cookie y mensajes de error o cancelación del inicio.
- [x] INF-B04. Verificar que el cierre de sesión conserva su retorno a `/` y que `RetornoLocal` no requiere modificaciones.
- [x] INF-B05. Comprobar que no se modificaron `appsettings.Local.json`, credenciales, esquema MySQL ni configuración habitual del usuario.
- [x] INF-B06. Ejecutar `dotnet build SolarUY.slnx` y verificar compilación exitosa.
- [x] INF-B07. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar resultado satisfactorio.
- [x] INF-B08. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar ArchUnitNET antes de pasar a Presentation.

## 4. Presentation

**Dependencia:** Infrastructure B verificada. Crear las páginas en `SolarUY/Presentation/Pages/`, con namespace `SolarUY.Presentation.Pages` y renderizado SSR.

### Listado

- [x] PRE-L01. Crear `ListadoParquesSolares.razor` con ruta `/parques-solares`, título y encabezado descriptivos, `[Authorize]` sin roles e inyección de `IParqueSolarService`.
- [x] PRE-L02. Cargar el resultado de `ListarAsync` durante la inicialización de la página.
- [x] PRE-L03. Renderizar una tabla con encabezados para ID, nombre, ubicación (localidad / dirección) y capacidad total estimada en MW, utilizando los DTO recibidos.
- [x] PRE-L04. Hacer que **toda la superficie de cada fila**, incluidas sus celdas y espacios, abra el detalle del parque correspondiente; mantener la navegación compatible con SSR.
- [x] PRE-L05. Permitir acceder a cada parque con Tab y abrir su detalle con Enter, con foco visible y nombre accesible que identifique el parque.
- [x] PRE-L06. Mostrar «No hay parques solares registrados» cuando el resultado exitoso tenga una colección vacía.

### Detalle

- [x] PRE-D01. Crear `DetalleParqueSolar.razor` con ruta `/parques-solares/{Id:guid}`, `[Authorize]` sin roles e inyección de `IParqueSolarService`.
- [x] PRE-D02. Cargar el detalle mediante `ObtenerPorIdAsync` al recibir el parámetro `Id`.
- [x] PRE-D03. Mostrar ID, nombre, ubicación y capacidad del DTO con título y encabezado descriptivos.
- [x] PRE-D04. Agregar un enlace para volver a `/parques-solares`.
- [x] PRE-D05. Mostrar «No se encontró el parque solar» y el enlace al listado ante `NoEncontrado`, sin conservar datos de otro parque.
- [x] PRE-D06. Verificar que un segmento de ruta que no sea GUID utiliza la página de ruta no encontrada existente y no entra al caso de uso de detalle.

### Estados comunes, menú e inicio de sesión

- [x] PRE-C01. Manejar `AutenticacionRequerida` en ambas páginas dirigiendo al inicio de sesión, sin renderizar datos protegidos.
- [x] PRE-C02. Manejar fallos técnicos de lectura en ambas páginas con «No se pudo cargar la información. Intentá nuevamente» y una opción para recargar.
- [x] PRE-C03. Registrar únicamente diagnósticos depurados de los fallos, sin SQL, secretos ni excepciones completas.
- [x] PRE-C04. Aplicar el mismo formato cultural al decimal y mostrar la unidad `MW` en ambas vistas, sin calcular ni fijar la capacidad desde Presentation.
- [x] PRE-C05. Agregar «Parques solares» al `AuthorizeView` general de `SolarUY/Components/Layout/NavMenu.razor`, visible para cualquier sesión autenticada.
- [x] PRE-C06. Verificar que el menú conserva «Registrar parque solar» exclusivo de Administrador.
- [x] PRE-C07. Retirar el campo oculto y la propiedad de consulta `ReturnUrl` de `IniciarSesion.razor`, conservando POST, antifalsificación y mensajes actuales.
- [x] PRE-C08. Revisar la protección SSR y `AuthorizeRouteView`: las peticiones anónimas directas a listado y detalle deben requerir ingreso sin entregar datos.
- [x] PRE-C09. Revisar que las páginas consumen solamente contratos y DTO de Application, sin acceso a Infrastructure, lógica de negocio, nuevos endpoints REST ni componentes WebAssembly.
- [x] PRE-C10. Ejecutar `dotnet build SolarUY.slnx` y verificar compilación exitosa.
- [x] PRE-C11. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar resultado satisfactorio.
- [x] PRE-C12. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar ArchUnitNET antes de la aceptación funcional.

## 5. Aceptación funcional y cierre

Las siguientes comprobaciones complementan los controles anteriores. Si faltan acceso al proveedor externo, identidades o un entorno aislado, dejar pendientes las verificaciones dependientes y registrar el motivo. No sustituir un ingreso OIDC real por una simulación.

### Preparación de la aceptación

- [x] ACE-01. Identificar un destino de pruebas aislado para preparar parques o provocar fallos, sin modificar la base habitual ni `appsettings.Local.json`.
- [x] ACE-02. Confirmar disponibilidad de identidades de prueba con Administrador, Operador y sin roles reconocidos, sin registrar credenciales ni tokens en evidencias.
- [x] ACE-03. Preparar en el destino aislado varios parques con datos identificables y un escenario sin registros; si se crean auxiliares, ubicarlos en `arnes/prompting/tests/req003/`.

### CA1 — Datos y navegación

- [x] ACE-04. Verificar en navegador que el listado muestra los cuatro datos de todos los parques preparados y respeta el orden por nombre e ID.
- [x] ACE-05. Hacer clic en distintas celdas y espacios de cada fila y comprobar que toda su superficie abre el detalle del parque correcto.
- [x] ACE-06. Recorrer los parques con Tab, comprobar el foco visible y abrir cada detalle con Enter.
- [x] ACE-07. Comparar los cuatro datos de cada detalle con el listado y verificar la vuelta al listado mediante su enlace.
- [x] ACE-08. Comprobar que listado y detalle muestran la capacidad actual de Domain como `0 MW` y utilizan formato coherente.

### CA2 — Seguridad y regresión del alta

- [x] ACE-09. Acceder anónimamente al listado y a un detalle conocido y verificar que no se entregan datos y se requiere iniciar sesión.
- [x] ACE-10. Comprobar acceso al listado, al detalle directo y al enlace del menú con Administrador.
- [x] ACE-11. Comprobar acceso al listado, al detalle directo y al enlace del menú con Operador.
- [x] ACE-12. Comprobar acceso al listado, al detalle directo y al enlace del menú con una identidad autenticada sin roles reconocidos.
- [x] ACE-13. Revisar que las consultas del servicio rechazan usuarios anónimos antes de invocar al repositorio; registrar esta comprobación como revisión de código, separada de la aceptación externa.
- [x] ACE-14. Comprobar que Operador e identidad sin roles no ven el enlace de alta y que el acceso directo a `/parques-solares/nuevo` sigue siendo rechazado.
- [x] ACE-15. Registrar un parque como Administrador en el destino aislado y verificar su aparición en listado y detalle, sin cambios en el comportamiento previo del alta.

### CA3 — Destino después del ingreso

- [x] ACE-16. Completar un inicio OIDC real con Administrador y verificar que el destino final es `/parques-solares`.
- [x] ACE-17. Completar un inicio OIDC real con Operador y verificar el mismo destino.
- [x] ACE-18. Completar un inicio OIDC real con una identidad sin roles reconocidos y verificar el mismo destino.
- [x] ACE-19. Iniciar desde un detalle protegido sin sesión y comprobar que, después del ingreso real, se llega al listado.
- [x] ACE-20. Comprobar que un `ReturnUrl` local distinto y uno externo, recibidos por URL o enviados al POST con antifalsificación válida, no alteran el destino final del ingreso.
- [x] ACE-21. Cancelar un inicio en el proveedor y verificar que se conserva el mensaje controlado y no se habilitan datos protegidos.
- [x] ACE-22. Cerrar sesión y verificar el retorno a `/` y que las consultas protegidas requieren nuevamente ingreso.

### Estados de consulta y revisión final

- [x] ACE-23. Consultar el escenario sin parques y verificar el mensaje de lista vacía como resultado exitoso.
- [x] ACE-24. Consultar un GUID inexistente y `Guid.Empty`; comprobar el mensaje de parque no encontrado, sin creación de registros ni datos de otro parque.
- [x] ACE-25. Consultar una ruta con GUID malformado y comprobar la página de ruta no encontrada existente.
- [x] ACE-26. Provocar un fallo de lectura únicamente en el destino aislado y comprobar el mensaje técnico, la opción de recarga y la ausencia de detalles sensibles; no debe mostrarse como lista vacía ni parque inexistente.
- [x] ACE-27. Guardar evidencias depuradas y resultados de controles en `artifacts/req003/`, verificando que permanece excluido de Git.
- [x] ACE-28. Revisar el diff final: alcance de req003, nomenclatura, responsabilidades y dependencias correctas; diagramas, pruebas estructurales y configuración privada preservados.
- [x] ACE-29. Si la aceptación o la revisión exigieron cambios de código, repetir compilación, pruebas de Domain y ArchUnitNET de la etapa afectada, y las verificaciones funcionales relacionadas.
- [x] ACE-30. Actualizar los checkboxes según resultados verificables y registrar las limitaciones pendientes; declarar completado req003 únicamente cuando estén satisfechos los controles y los tres criterios de aceptación.

## Registro final de ejecución — req003 completado y aceptado

Fecha: 2026-10-25. **100 de 100 subtareas verificadas; ninguna pendiente.** Los tres criterios de aceptación están satisfechos con controles de capa y sesiones OIDC reales de Administrador, Operador y sin roles reconocidos. No se afirma que esos perfiles correspondan a tres cuentas distintas mantenidas simultáneamente; se verificaron los tres contextos de autorización en sesiones independientes.

### Implementación y controles

Se siguió la secuencia Preparación → Domain → Application A → Infrastructure A → Application B → Infrastructure B → Presentation. Antes de avanzar de cada etapa pasaron `dotnet build SolarUY.slnx`, las 21 pruebas de Domain y las 17 de ArchUnitNET. No se modificaron Domain, los tests estructurales, los diagramas, las migraciones ni `appsettings.Local.json`.

Advertencias preexistentes: NU1903 de Microsoft.AspNetCore.Components.WebAssembly.Server 10.0.12, GHSA-g27p-qf6j-c96c y GHSA-qjpx-jqm5-h2c4. Sin errores ni advertencias nuevas de código. La aceptación no exigió cambios posteriores de código de producción; ACE-29 no requirió otra repetición de los controles de capa.

### Aceptación real

- **CA1:** listado completo con ID, nombre, ubicación y capacidad 0 MW, cotejado con MySQL y cada detalle; orden por nombre, navegación mediante las cuatro celdas y un espacio de cada fila inicial (quince clics), Tab y Enter, nombre accesible, foco visible de 3 px y enlaces de vuelta. El parque registrado por Administrador apareció en listado y detalle.
- **CA2:** acceso a listado, detalle directo y menú con Administrador, Operador y sin roles reconocidos, en sesiones OIDC reales. Las peticiones anónimas exigieron ingreso sin datos. Operador y la identidad sin roles no vieron el enlace de alta y su acceso directo a `/parques-solares/nuevo` fue rechazado. Administrador conservó el alta. La revisión de autorización del servicio de ACE-13 quedó documentada separadamente de la aceptación externa.
- **CA3:** los tres perfiles terminaron en `/parques-solares` tras el ingreso real. También se verificaron el inicio desde un detalle protegido y ReturnUrl local y externo recibidos por URL: todos finalizaron en el listado. ACE-20 se acredita en la variante de parámetros por URL; no se afirma una manipulación manual del POST.
- Se verificaron lista vacía, GUID inexistente y vacío sin escrituras ni datos anteriores, GUID malformado con la página 404 existente, fallos reales de lectura en el destino aislado con mensaje genérico y recarga, y recuperación al restaurar el escenario. Los diagnósticos se depuraron, sin SQL ni excepciones completas.
- El cierre real retornó a `/` y las consultas volvieron a exigir ingreso. El usuario canceló una solicitud vigente con Decline en el proveedor; SolarUY mostró `/cuenta/iniciar-sesion?error=cancelado` y el mensaje controlado. Listado y detalle siguieron protegidos.

### Cierre de las cuatro subtareas de Operador

El usuario completó el ingreso real con Operador y se observó la sesión con ese rol y destino `/parques-solares`. Se abrieron directamente los tres detalles de prueba y se compararon sus cuatro datos con el listado; el menú volvió al listado. El enlace de alta estuvo ausente y el acceso directo al formulario terminó en acceso denegado, sin formulario disponible. Se cerró después la sesión con retorno al inicio. Quedan acreditados ACE-02, ACE-11, ACE-14 y ACE-17.

Esta fase usó la base aislada `solaruy_req003_aceptacion_53c70d1ee3c9` con tres parques, en una instancia MySQL propia ligada a `127.0.0.1:13306`. La fase anterior con Administrador y sin roles utilizó `solaruy_req003_aceptacion_446787cc9185`. La base habitual no se alteró. Los auxiliares están en `arnes/prompting/tests/req003/`; las identidades locales del auxiliar no se usaron para acreditar sesiones OIDC reales.

### Evidencias y preservación

Resumen final: `artifacts/req003/aceptacion-final.md`. Resultados por etapa y registro detallado: `artifacts/req003/resultados.md`. Evidencias de Operador: `operador-aceptacion.json`, `operador-listado.jpg` y `operador-alta-denegada.jpg`; evidencias anteriores de Administrador, sin roles, retorno externo y cancelación se conservan en la misma carpeta, excluida de Git. Las capturas excluyen identidades personales y los JSON no contienen credenciales, tokens ni valores de cookies.

Se verificó por hash la conservación de configuración privada, diagramas y pruebas estructurales. Al cerrar cada entorno se detuvieron solo los procesos propios, verificando PID y hora de inicio, y se retiraron sus directorios temporales MySQL. Las evidencias permanecen. Estado de la última limpieza: `artifacts/req003/cierre-operador.json`.