 # req003 — Especificaciones técnicas: Listar Parque Solar

Fuente: [requirements.md](requirements.md). Aplican [AGENTS.md](../../../../AGENTS.md), las siete reglas compartidas y los diagramas de `.agents/Images`.

El alcance comprende consultar todos los parques, abrir sus detalles y llegar al listado después de iniciar sesión. Aunque la descripción menciona al Administrador, el criterio de seguridad habilita ambas consultas a **cualquier usuario autenticado**, incluidos Operador e identidades sin roles reconocidos. No se incorporan filtros, paginación, edición, eliminación ni configuración de topología.

Se amplían los módulos existentes del servidor .NET 10; las capas son carpetas y namespaces, no proyectos nuevos. Se mantiene el renderizado estático de Blazor en servidor (SSR) utilizado por las páginas protegidas actuales, dentro de la arquitectura existente. Este alcance no requiere componentes nuevos en `SolarUY.Client`, endpoints REST ni MediatR. Los diagramas permanecen intactos.

## 1. Domain

- Reutilizar `SolarUY.Domain.ParquesSolares.ParqueSolar` y `NombreParqueSolar`, sin modificar sus invariantes ni agregar comportamiento para una consulta.
- Los datos de ambas vistas proceden de `ID` (`Guid`), `Nombre.Valor`, `Ubicacion` y `CapacidadTotalEstimadaMw` (`decimal`). Ubicación sigue siendo un único texto de localidad / dirección.
- La capacidad se obtiene de la propiedad del dominio. Actualmente devuelve `0m` porque la topología está pendiente; se mostrará **0 MW** para los parques existentes bajo este modelo. No persistir otra capacidad ni calcularla o fijarla en la UI para este requerimiento.
- No incorporar nodos, paneles ni relaciones provisionales. Se conserva el cálculo derivado previsto por el modelo para cuando se implemente la topología.

No se requiere modificar comportamiento de Domain ni agregar pruebas unitarias en esta capa para req003. Si durante la implementación resultara necesario cambiarlo, acompañar el cambio con las pruebas xUnit correspondientes.

## 2. Application

Ampliar los contratos existentes en `SolarUY.Application.ParquesSolares` y su implementación `Servicios.ParqueSolarService`:

| Contrato | Operación propuesta | Responsabilidad |
| --- | --- | --- |
| `IParqueSolarRepository` | `ListarAsync(CancellationToken)` → `IReadOnlyList<ParqueSolar>` | Leer todos los parques con orden estable por nombre y, en caso de empate, ID. |
| `IParqueSolarRepository` | `ObtenerPorIdAsync(Guid id, CancellationToken)` → `ParqueSolar?` | Leer un parque por su ID; devolver `null` si no existe. |
| `IParqueSolarService` | `ListarAsync(CancellationToken)` → `ResultadoListadoParquesSolaresDto` | Autorizar la consulta y entregar la colección de DTO. |
| `IParqueSolarService` | `ObtenerPorIdAsync(Guid id, CancellationToken)` → `ResultadoDetalleParqueSolarDto` | Autorizar y entregar el DTO o informar que el parque no existe. |

Las operaciones son asíncronas mediante `Task<T>` y aceptan `CancellationToken cancellationToken = default`. Se mantienen las dependencias por constructor en el servicio: `IParqueSolarRepository` e `IUsuarioActualService`.

### Autorización y resultados

- Comprobar `IUsuarioActualService.EstaAutenticado` **antes de consultar el repositorio** en ambas operaciones. No exigir pertenencia a un rol para leer; la autorización de `CrearAsync` sigue exigiendo Administrador.
- Reutilizar `ParqueSolarDto(Guid ID, string Nombre, string Ubicacion, decimal CapacidadTotalEstimadaMw)` para listado y detalle. Application transforma entidades en DTO y obtiene la capacidad desde Domain.
- Definir `CodigoConsultaParqueSolar` con `Exitoso`, `AutenticacionRequerida` y `NoEncontrado`. `ResultadoListadoParquesSolaresDto` contiene código y colección de `ParqueSolarDto`; `ResultadoDetalleParqueSolarDto` contiene código y un `ParqueSolarDto?`.
- Una lista vacía es un resultado exitoso. En resultados rechazados no se incluyen datos. Un ID válido sin coincidencia, incluido `Guid.Empty`, produce `NoEncontrado`; la consulta de detalle no crea registros.
- Los fallos técnicos se propagan a la frontera de Presentation para su manejo controlado. No convertir un fallo de persistencia en lista vacía o parque inexistente.

Application depende únicamente de Domain y tipos de la biblioteca base; no utiliza EF Core, `HttpContext`, Blazor ni clases concretas de Infrastructure.

## 3. Infrastructure

### Consultas de persistencia

Ampliar `ParqueSolarRepository`, que implementa `IParqueSolarRepository`, utilizando `SolarUyDbContext.ParquesSolares` y el proveedor MySQL existente:

- Consultas EF Core asíncronas con `AsNoTracking` y propagación del token de cancelación.
- Listado ordenado por `Nombre.Valor` y después por `ID`, según la intercalación configurada en MySQL. Materializar la colección antes de devolverla; no exponer `IQueryable` fuera del repositorio.
- Detalle filtrado por ID y retorno nullable cuando no haya coincidencia. La lectura debe materializar también el objeto de valor `Nombre`, mediante su mapeo existente.
- Sin escrituras, cambios de esquema ni migraciones. La capacidad continúa excluida del mapeo y se obtiene del dominio después de materializar las entidades.

Se reutilizan los registros de inyección de dependencias de `Program.cs`, la conexión existente y `UsuarioActualService`. Se conserva `appsettings.Local.json` y no se introducen credenciales en el documento ni en el cliente.

### Destino después del inicio de sesión

En la composición del servidor, ajustar el inicio externo existente de `POST /cuenta/autenticar` para establecer `AuthenticationProperties.RedirectUri` en **`/parques-solares`**. Tras completar correctamente el callback OIDC, ese será el destino de todos los usuarios autenticados.

Para cumplir literalmente el criterio de redirección, `ReturnUrl` deja de seleccionar otro destino después del ingreso, incluso cuando sea una ruta local válida. La página de inicio de sesión debe dejar de enviar ese campo. El destino constante evita incorporar URLs proporcionadas por el navegador. `RetornoLocal` no requiere cambios para estas consultas.

Conservar el flujo OIDC y sus validaciones, cookie, antifalsificación del POST y limitación de intentos. Los errores o la cancelación del ingreso mantienen los mensajes actuales y no habilitan el listado. El cierre de sesión conserva su retorno actual a `/`.

## 4. Presentation

### Páginas y navegación

Crear en `SolarUY/Presentation/Pages`, bajo el namespace `SolarUY.Presentation.Pages`:

| Componente y ruta | Comportamiento |
| --- | --- |
| `ListadoParquesSolares.razor` — `/parques-solares` | Invocar `IParqueSolarService.ListarAsync` al cargar y mostrar una tabla con ID, nombre, ubicación (localidad / dirección) y capacidad total estimada en MW. Toda la fila de cada parque es clickeable: hacer clic en cualquier punto de ella abre el detalle de ese parque. La navegación también debe permitir seleccionar el parque con Tab y abrir su detalle con Enter, con foco visible y un nombre accesible que identifique el parque. |
| `DetalleParqueSolar.razor` — `/parques-solares/{Id:guid}` | Invocar `IParqueSolarService.ObtenerPorIdAsync` con el parámetro de ruta y mostrar los mismos cuatro datos, junto con un enlace para volver al listado. |

Ambas páginas llevan `[Authorize]`, sin restricción de roles. Se conserva `AuthorizeRouteView` y la protección de las peticiones SSR en el servidor. El acceso anónimo directo requiere iniciar sesión y no entrega datos; después del ingreso se llega al listado, también si el acceso original fue un detalle.

Agregar en el `AuthorizeView` general del menú un enlace «Parques solares», visible para cualquier sesión autenticada. Mantener el enlace de alta exclusivo del Administrador. El formulario de `IniciarSesion.razor` conserva el POST y antifalsificación, sin campo ni parámetro `ReturnUrl` utilizado para elegir el destino.

### Estados y mensajes

- Listado sin registros: «No hay parques solares registrados», sin tratarlo como error.
- Detalle inexistente: «No se encontró el parque solar» y enlace al listado, sin datos de otro parque. Un segmento que no sea GUID utiliza la página de ruta no encontrada existente.
- Autenticación requerida: dirigir al inicio de sesión sin mostrar datos protegidos.
- Error técnico: mensaje genérico «No se pudo cargar la información. Intentá nuevamente», con posibilidad de recargar. Registrar únicamente un diagnóstico depurado, sin excepciones completas, SQL ni secretos.

Las vistas muestran DTO y estados de presentación; no consultan Infrastructure ni calculan capacidad. Formatear el decimal con la cultura de presentación y la unidad explícita `MW`, de forma coherente entre listado y detalle.

### Verificación para la implementación

| Criterio | Comprobación esperada |
| --- | --- |
| CA1: lista y detalles | Con varios parques identificados, verificar los cuatro datos, sus valores y unidades en ambas vistas; comprobar que hacer clic en distintas celdas y espacios de cada fila abre el detalle correcto, que también se puede acceder con Tab y Enter, y que se puede volver al listado. |
| CA2: todos los autenticados | Verificar acceso con Administrador, Operador e identidad autenticada sin roles reconocidos; comprobar que el alta continúa restringida. Acceder anónimamente a ambas rutas no debe entregar datos. |
| CA3: destino del ingreso | Completar un inicio OIDC real con cada tipo de usuario y comprobar `/parques-solares` como destino. Repetir desde un detalle protegido y con `ReturnUrl` local o externo: siempre finalizar en el listado. |
| Estados de consulta | Verificar lista vacía, ID inexistente, GUID malformado y error de lectura controlado; distinguir ausencia de datos de fallos técnicos. |

La aceptación que necesite crear datos o provocar fallos debe usar un destino aislado identificado, preservando la base habitual del usuario. Los auxiliares, si se necesitan, van en `arnes/prompting/tests/req003/`; las evidencias depuradas, en `artifacts/req003/`, excluidas de Git. Una inspección o simulación no sustituye el inicio de sesión externo real; las comprobaciones externas sin acceso disponible quedan pendientes.

Durante la implementación, al finalizar cada capa modificada y antes de avanzar a la siguiente, ejecutar desde la raíz:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Corregir el código ante fallos, sin alterar las pruebas estructurales. No generar pruebas unitarias de Application, Infrastructure ni Presentation. La entrega de esta fase es únicamente este `specs.md`; la planificación de tareas y la implementación corresponden a fases posteriores.
