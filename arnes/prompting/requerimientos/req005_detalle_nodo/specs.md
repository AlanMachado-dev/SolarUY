# req005 — Especificaciones técnicas: Detalle de Nodo

## Objetivo y alcance

Permitir que un usuario autenticado con rol `Administrador` consulte el detalle de un nodo desde el detalle de su parque solar y pueda regresar al parque de origen.

Fuente: [requirements.md](requirements.md). El diseño respeta `AGENTS.md`, las siete reglas compartidas y los diagramas de `.agents/Images`, sin modificarlos. Domain, Application, Infrastructure y Presentation continúan como carpetas y namespaces del servidor `SolarUY`.

Se conserva la topología existente: cada nodo tiene exactamente un inversor y cada inversor un sensor. La mención en plural de equipos en el requerimiento no implica cambiar estas cardinalidades. Se reutilizan los DTO, la autenticación y el renderizado estático del servidor (SSR) actuales, con invocación directa a Application y sin MediatR. El botón de paneles será únicamente una reserva visual; no se incorpora alta de paneles, edición de equipos, telemetría ni conexión al endpoint del sensor.

## 1. Domain

- Reutilizar `Nodo`, `Inversor`, `Sensor` y `ParqueSolar`, con sus relaciones encapsuladas e invariantes actuales. No se requieren nuevas entidades, propiedades ni métodos de negocio para esta consulta.
- Obtener los datos del inversor y del sensor mediante `Nodo.Inversor.Sensor`; el parque de regreso se obtiene de `Nodo.Parque`.
- Mantener `Sensor.Endpoint` opcional. El texto que indica su ausencia es una decisión de presentación y no modifica el dato persistido.
- No cambiar el comportamiento del dominio; por lo tanto, esta entrega no requiere nuevas pruebas unitarias de Domain.

## 2. Application

Extender los contratos existentes de `SolarUY.Application.Nodos` y el servicio `SolarUY.Application.Nodos.Servicios.NodoService`, sin dependencias de Blazor, EF Core ni Infrastructure.

| Contrato | Diseño |
| --- | --- |
| `INodoService` | Agregar `ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)`, con resultado `ResultadoDetalleNodoDto`. Conservar el alta existente. |
| `INodoRepository` | Agregar `ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)`, que devuelve `Nodo?` con parque, inversor y sensor cargados. |
| `ResultadoDetalleNodoDto` | Nuevo record con `CodigoConsultaNodo Codigo` y `NodoDto? Nodo`. Solo un resultado exitoso contiene datos del nodo. |
| `CodigoConsultaNodo` | Nuevo enum con `Exitoso`, `AutenticacionRequerida`, `AccesoDenegado` y `NoEncontrado`. |
| `NodoDto`, `InversorDto`, `SensorDto` | Reutilizar los contratos actuales sin ampliar sus campos. `NodoDto.ParqueId` permite construir el enlace de regreso; las identidades se usan internamente. |

### Flujo de consulta

1. Comprobar autenticación con `IUsuarioActualService`; de faltar, devolver `AutenticacionRequerida` sin consultar persistencia.
2. Comprobar el rol exacto `Administrador`; de faltar, devolver `AccesoDenegado` sin consultar persistencia ni revelar si existe el nodo.
3. Si el ID es `Guid.Empty`, devolver `NoEncontrado`. Para otro ID, consultar exclusivamente el nodo solicitado mediante `INodoRepository`.
4. Si no existe, devolver `NoEncontrado`. Si existe, reutilizar el mapeo de `NodoService` a `NodoDto`, incluyendo el parque real, las coordenadas y los equipos asociados.
5. Devolver `Exitoso` con el DTO completo. No crear ni actualizar entidades durante la consulta.

Las dependencias se mantienen inyectadas por constructor. Los fallos técnicos se propagan al manejo de errores de Presentation; no deben traducirse en nodo inexistente ni en datos vacíos. La autorización en Application complementa la protección de la página y no depende de la visibilidad de los enlaces.

## 3. Infrastructure

- Implementar la nueva consulta en `SolarUY.Infrastructure.Persistencia.NodoRepository`, reutilizando `SolarUyDbContext` inyectado por constructor.
- Consultar `Nodos` por su ID mediante `AsNoTracking()`, cargando explícitamente `Parque` e `Inversor.Sensor`, y devolver un único nodo o `null`. No cargar todos los nodos del parque ni depender de carga diferida.
- Mantener los mapeos y relaciones actuales. No se requieren migraciones, cambios de esquema ni escrituras. La consulta no modifica el seguimiento ni los datos utilizados por el alta.
- Reutilizar los registros existentes de `INodoService` e `INodoRepository` en `SolarUY/Program.cs`, además de la autenticación y los roles actuales.
- Conservar `SolarUY/appsettings.Local.json` y la base habitual del usuario. Las comprobaciones que necesiten preparar datos o provocar fallos deben usar un destino aislado identificado.

## 4. Presentation

### Página de detalle

Crear `SolarUY/Presentation/Pages/DetalleNodo.razor`, namespace `SolarUY.Presentation.Pages`, con ruta `/nodos/{Id:guid}` y `[Authorize(Roles = "Administrador")]`. Mantener SSR, sin agregar una API ni componentes de cliente para este caso de uso.

La página invoca `INodoService.ObtenerPorIdAsync` al recibir el parámetro de ruta y muestra únicamente un resultado exitoso, en tres secciones semánticas:

| Sección | Datos visibles |
| --- | --- |
| Nodo | Nombre, latitud y longitud. |
| Inversor | Nombre y número de serie. |
| Sensor | Nombre, número de serie y endpoint. Si no existe endpoint, mostrar **«Sin endpoint asociado»**. |

Formatear las coordenadas con la cultura de la interfaz, sin cálculos de negocio. Renderizar los valores como texto codificado por Razor; el endpoint se presenta como dato, sin efectuar solicitudes. No mostrar IDs como contenido al usuario.

Con el detalle cargado, ofrecer:

- **«Volver al parque»**: enlace con apariencia de botón a `/parques-solares/{ParqueId:guid}`, usando el `ParqueId` devuelto por Application y sin depender del historial del navegador.
- **«+ Agregar Panel Solar»**: botón `type="button"` deshabilitado, sin manejador, navegación ni escritura, acompañado de una indicación breve de disponibilidad futura.

Presentation solo invoca el servicio y presenta DTO y estados de interfaz; no accede a repositorios, no construye entidades ni decide reglas de negocio.

### Acceso desde el parque solar

- Actualizar `DetalleParqueSolar.razor` y, si hace falta, su CSS para que toda la superficie de cada tarjeta existente de nodo permita navegar a `/nodos/{nodo.ID:guid}` para Administrador.
- Utilizar un enlace real con nombre accesible asociado al nombre del nodo y superficie extendida sobre toda la tarjeta, por ejemplo con `stretched-link` dentro de un contenedor posicionado. Evitar controles interactivos anidados y soluciones basadas únicamente en `onclick`.
- Permitir activación mediante clic en el nombre, datos o espacio libre, y mediante teclado con Tab y Enter; mostrar un foco visible que identifique la tarjeta.
- Mostrar el enlace solo para Administrador mediante la autorización de presentación existente. Conservar para los demás usuarios autenticados la visualización actual del parque y sus nodos, sin habilitar esta navegación.
- Conservar los datos, el orden, los estados vacíos y las acciones actuales del detalle del parque, incluidos el alta de nodos y el regreso al listado.

### Estados y errores

- **Sin autenticación o sin rol:** reutilizar el flujo existente de inicio de sesión y acceso denegado. Proteger también el acceso directo por URL y no renderizar datos del detalle ante un rechazo de Application.
- **Nodo inexistente:** mostrar **«No se encontró el nodo»**, sin secciones de detalle ni acciones que requieran conocer su parque. Ofrecer regreso al listado de parques. Una ruta cuyo ID no tenga formato GUID queda bajo el manejo de rutas no encontradas existente.
- **Fallo técnico:** mostrar **«No se pudo cargar la información. Intentá nuevamente»** y una acción para recargar el mismo detalle. Registrar únicamente un diagnóstico depurado, sin excepciones completas, SQL, endpoints ni credenciales.
- Limpiar el resultado anterior al consultar otro ID para evitar mostrar datos de otro nodo.

### Verificación prevista de la implementación

| Criterio | Resultado esperado |
| --- | --- |
| CA1: datos | Un nodo persistido muestra nombre y coordenadas correctos, junto con nombre y serie de su inversor y sensor; probar endpoint informado y ausente. |
| CA2: acceso desde el parque | Clic en nombre, datos y espacios de distintas tarjetas abre el nodo seleccionado. Tab y Enter permiten el mismo acceso y el foco es visible. |
| CA3: regreso | «Volver al parque» abre el parque real del nodo, incluso entrando directamente al detalle por URL. Verificar con nodos de distintos parques. |
| CA4: paneles | Se muestra «+ Agregar Panel Solar» deshabilitado y su presencia no provoca acciones ni cambios de datos. |
| CA5: seguridad | Administrador obtiene el detalle. Anónimo y usuario sin ese rol no lo obtienen por enlace ni URL directa; el servicio rechaza ambos casos antes de consultar persistencia. |
| Estados y regresión | GUID inexistente o vacío, ruta mal formada y fallo técnico controlado muestran sus estados correspondientes. Se conserva la consulta de parques, el alta de nodos y la ausencia de escrituras al consultar. |

Al implementar, ejecutar desde la raíz los controles obligatorios al finalizar cada capa modificada y antes de avanzar a la siguiente:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Corregir los fallos en el código sin alterar los tests estructurales. No generar pruebas unitarias de Application, Infrastructure ni Presentation. Verificar la interfaz en escritorio y móvil; los auxiliares funcionales, si se necesitan, se guardan en `arnes/prompting/tests/req005/` y las evidencias depuradas en `artifacts/req005/`, excluidas de Git. Registrar lo ejecutado realmente; una inspección o simulación no constituye aceptación externa real.

Esta fase entrega únicamente `specs.md`. La planificación de tareas y la implementación quedan para las fases siguientes.
