# req006 — Especificaciones técnicas: Alta de panel solar

## Objetivo y alcance

Permitir que un usuario autenticado con rol `Administrador` registre un panel solar desde el detalle de un nodo existente y consulte allí los paneles pertenecientes a ese nodo.

Fuente: [requirements.md](requirements.md). Se respetan `AGENTS.md`, las siete reglas compartidas y los diagramas de `.agents/Images`, sin modificarlos. Las cuatro capas siguen siendo carpetas y namespaces del servidor `SolarUY`. Se reutilizan Blazor con renderizado estático del servidor (SSR), los servicios de Application, la autenticación existente y EF Core con MySQL, sin MediatR ni nuevas API o proyectos.

Las referencias a «datos del nodo» en CA1 se interpretan como datos del **panel**. El nodo de destino se obtiene de la ruta. No se incorpora edición, eliminación, cambio de estado, captura de mediciones ni gestión de mantenimientos.

## 1. Domain

### Modelo y relaciones

- Incorporar `Panel`, `EstadoPanel`, `NombrePanel` y `DatosPanelInvalidosException` en `SolarUY.Domain.Paneles`. `Panel` representa la entidad del diagrama; no crear otra entidad denominada `PanelSolar` para el mismo concepto.
- `Panel` contiene `ID` de tipo `Guid`, `Nombre` de tipo `NombrePanel`, `Modelo` de tipo `string`, `PotenciaNominalW` de tipo `float`, `FechaInstalacion` de tipo `DateTime`, `Estado` de tipo `EstadoPanel` y una referencia obligatoria a `Nodo`. Mantener constructores de materialización privados y propiedades sin setters públicos.
- Extender `Nodo` con una colección privada de paneles, expuesta como `IReadOnlyCollection<Panel>`. `Nodo.AgregarPanel` valida, crea e incorpora el panel, preservando la relación bidireccional y evitando incorporaciones parciales ante errores. No crea nodos ni persiste datos.
- Cada panel pertenece exactamente a un nodo; cada nodo admite cero o más paneles. Representar la pertenencia mediante objetos en Domain; `IdNodo` será la FK de persistencia y `NodoId` un dato de los DTO, sin una relación editable paralela.
- Sustituir el valor fijo de `Nodo.CantidadPaneles` por el tamaño de su colección. Sustituir el valor fijo de `ParqueSolar.CapacidadTotalEstimadaMw` por la suma de las potencias nominales de todos sus paneles dividida entre `1_000_000`, conservando su salida `decimal`. Es una capacidad nominal estimada, independiente del estado operativo y de la generación medida; un parque sin paneles devuelve cero. Estos cálculos pertenecen al dominio.

### Invariantes y decisiones de diseño

| Dato | Regla |
| --- | --- |
| ID | Generado por Domain con `Guid.NewGuid()`, no vacío y protegido por la PK. No se recibe del formulario. |
| Nombre | Obligatorio. Como decisión consistente con los nombres de parques existentes, `NombrePanel` recorta extremos, normaliza Unicode a Form C y obtiene una clave con `ToUpperInvariant()`. Límite técnico de 100 caracteres Unicode tanto para el valor como para la clave. |
| Unicidad | Comparar la clave del nombre dentro del mismo nodo. Mayúsculas y espacios externos no distinguen nombres; los acentos se conservan. El mismo nombre puede existir en nodos diferentes. Domain rechaza duplicados en la colección cargada; la persistencia garantiza también la concurrencia. |
| Modelo | Obligatorio; rechazar nulo, vacío o espacios y conservar el texto recortado, sin imponer un catálogo de modelos. |
| PotenciaNominalW | Obligatoria. Como decisión de validez física, debe ser finita y mayor que cero; rechazar cero, negativos, NaN e infinitos. No imponer una potencia máxima funcional. |
| FechaInstalacion | Fecha y hora actual del servidor al crear el panel, en UTC y truncada a segundos, sin redondeo. Application obtiene el instante con `TimeProvider` y Domain aplica la precisión; nunca se toma del cliente. |
| Estado | Enum con `Operativo`, `EnFalla`, `EnMantenimiento` y `Desactivado`. Toda alta asigna `Operativo`; no exponer una operación de cambio de estado en este alcance. |
| Nodo | Obligatorio y existente. La asociación se establece al crear el panel y no se reemplaza desde el alta. |

`DatosPanelInvalidosException` comunica campo y mensaje para las invariantes; el conflicto de nombre se identifica de manera diferenciada para mapearlo a `NombreDuplicado`. Domain no depende de EF, ASP.NET ni Application.

### Historiales y valores derivados

Respetar las relaciones conceptuales `Panel` → cero o más `Medicion` y `Panel` → cero o más `Mantenimiento` del diagrama. Ambos historiales comienzan vacíos. Dado que el requerimiento declara que esas funcionalidades todavía no existen, su implementación y persistencia se difieren a sus respectivos casos de uso; req006 no necesita introducir clases incompletas, registros ficticios ni tablas de históricos.

`GeneracionActualW` y `UltimoMantenimiento` se exponen como datos derivados de solo lectura, de tipos `float?` y `DateTime?`. En este alcance devuelven `null`: no existe última medición ni mantenimiento realizado. No almacenarlos como columnas editables ni confundir ausencia de medición con generación cero. Cuando se incorporen los historiales, su fuente será la última medición y la última fecha de mantenimiento realizado, respectivamente.

Agregar pruebas unitarias de Domain para creación válida, identidad y estado inicial, precisión de fecha, textos obligatorios, normalización y límites del nombre, potencias inválidas, duplicados en un nodo y nombres iguales en nodos distintos, pertenencia, colección protegida, ausencia de incorporación parcial, derivados sin historial, cantidad de paneles y capacidad de parques con varios nodos.

## 2. Application

Definir los nuevos contratos en `SolarUY.Application.Paneles` y el servicio en `SolarUY.Application.Paneles.Servicios`, con dependencias recibidas por constructor y sin referencias a Infrastructure, EF Core o Blazor.

| Contrato | Diseño |
| --- | --- |
| `IPanelService` / `PanelService` | `CrearAsync(Guid nodoId, CrearPanelDto datos, CancellationToken cancellationToken = default)`. Coordina exclusivamente el alta en un nodo existente. |
| `CrearPanelDto` | `Nombre`, `Modelo` y `float? PotenciaNominalW`, distinguiendo ausencia de cero. No incluye ID, nodo, fecha, estado ni derivados. |
| `PanelDto` | ID, NodoId, nombre como texto, modelo, potencia nominal, fecha de instalación, estado y derivados anulables, listos para mostrar. |
| `ResultadoAltaPanelDto` | Panel creado solo en caso de éxito, o errores con código, mensaje y campo. Códigos: `AutenticacionRequerida`, `AccesoDenegado`, `NodoNoEncontrado`, `DatosInvalidos` y `NombreDuplicado`. |
| `IPanelRepository` | `GuardarAsync(Panel panel, CancellationToken cancellationToken = default)`, con resultado `Guardado`, `ConflictoNombre` o `NodoNoEncontrado`. No expone detalles del proveedor ni `IQueryable`. |

### Flujo de alta

1. Comprobar autenticación y rol exacto `Administrador` mediante `IUsuarioActualService`, antes de consultar o escribir. No aceptar permisos enviados por el formulario.
2. Resolver el nodo con `INodoRepository.ObtenerPorIdAsync`, ampliado para cargar sus paneles. `Guid.Empty` o nodo ausente producen `NodoNoEncontrado`; no crear automáticamente el nodo ni sus equipos.
3. Comprobar presencia de la potencia en el DTO, obtener el instante actual con `TimeProvider` e invocar `Nodo.AgregarPanel`. Domain valida los valores y la unicidad en el nodo. Traducir sus errores al campo correspondiente del DTO.
4. Guardar únicamente el nuevo panel mediante `IPanelRepository`. Traducir una colisión concurrente a `NombreDuplicado` y la desaparición concurrente del nodo a `NodoNoEncontrado`.
5. Devolver éxito y `PanelDto` únicamente después de confirmar la escritura. Los fallos técnicos inesperados se propagan al manejo de errores de Presentation.

### Consulta del detalle y datos existentes

Extender `ResultadoDetalleNodoDto` con `IReadOnlyList<PanelDto> Paneles`. `NodoService.ObtenerPorIdAsync` reutiliza su autorización y consulta existentes; en éxito mapea únicamente los paneles del nodo y los ordena por nombre y luego ID. En rechazo o inexistencia devuelve una lista vacía sin datos del nodo. No agregar una consulta independiente que pueda omitir esa autorización.

Conservar los contratos existentes de `NodoDto` y `ParqueSolarDto`; sus cantidades y capacidad se obtienen de Domain una vez cargadas las relaciones necesarias. El listado y detalle de parques mantienen los permisos actuales, sin ampliar el acceso al detalle de nodos o al alta de paneles.

## 3. Infrastructure

- Implementar `PanelRepository` y `PanelConfiguration` en `SolarUY.Infrastructure.Persistencia`. Registrar `DbSet<Panel>` y su configuración en `SolarUyDbContext`, y `IPanelRepository`, `IPanelService` y `TimeProvider.System` en el punto de composición `SolarUY/Program.cs`.
- Crear una tabla `Paneles` con PK `ID` y FK obligatoria `IdNodo` a `Nodos`. Usar la conversión `Guid` ↔ texto ya establecida por el proyecto y sin generación adicional de IDs en MySQL. Mantener la política existente de eliminación en cascada para dependientes, sin incorporar un caso de uso de eliminación.
- Mapear `NombrePanel` como objeto poseído en la misma tabla: valor `Nombre` y `ClaveNombre`, ambos `varchar(100)`, con collation binaria `utf8mb4_0900_bin` para la clave. Crear el índice único compuesto `UX_Paneles_IdNodo_ClaveNombre` sobre `(IdNodo, ClaveNombre)`; no hacer globalmente único el nombre del panel.
- Persistir modelo como texto obligatorio, potencia nominal como valor obligatorio, fecha como `datetime(0)` interpretado siempre en UTC y estado como enum con valores estables. Ignorar los derivados del panel, la cantidad de paneles y la capacidad del parque. Mapear la colección encapsulada mediante su campo privado.
- Ampliar `NodoRepository.ObtenerPorIdAsync` para incluir `Paneles` además de parque, inversor y sensor. Ampliar las consultas de listado y detalle de `ParqueSolarRepository` para cargar `Nodos.Paneles` cuando se calculan cantidades o capacidad; conservar las cargas de equipos requeridas por el detalle. No interpretar relaciones sin cargar como colecciones vacías ni calcular totales en la UI.
- Guardar solo el panel nuevo, asociando el nodo existente como no modificado. No reinsertar ni actualizar el grafo de parque, nodo, inversor, sensor o paneles previos. Una única confirmación debe ser atómica; ante fallos, descartar o limpiar el seguimiento pendiente.
- Reconocer específicamente la violación del índice de nombre y de la FK de nodo y devolver los resultados del contrato. No convertir cualquier error de integridad en duplicado. El índice compuesto debe resolver dos altas concurrentes del mismo nombre con un solo éxito, conservando altas diferentes.
- Generar una migración incremental y su snapshot, conservando los datos existentes. Los nodos previos comienzan con cero paneles. No agregar columnas para generación actual, último mantenimiento ni tablas de históricos en esta entrega.

Conservar `SolarUY/appsettings.Local.json` y la base habitual del usuario. Aplicar y verificar migraciones, escrituras, concurrencia y fallos controlados en una base aislada identificada, sin exponer conexiones ni credenciales en evidencias.

## 4. Presentation

### Acceso y formulario

- En `DetalleNodo.razor`, sustituir el botón deshabilitado **«+ Agregar Panel Solar»** por un enlace a `/nodos/{NodoId:guid}/paneles/nuevo`, conservando el texto y retirando el aviso de disponibilidad futura.
- Crear `SolarUY/Presentation/Pages/AltaPanelSolar.razor`, namespace `SolarUY.Presentation.Pages`, con esa ruta y `[Authorize(Roles = "Administrador")]`. Proteger carga y POST; reutilizar SSR, formulario con nombre explícito, vinculación de POST y antifalsificación existentes.
- Resolver el nodo de destino con `INodoService.ObtenerPorIdAsync` y mostrar su nombre. Presentar solo **Nombre**, **Modelo** y **Potencia nominal (W)** como entradas obligatorias, con etiquetas, mensajes accesibles y conversión numérica acorde con la cultura de la interfaz.
- No solicitar ni vincular campos de identidad, pertenencia, fecha, estado o derivados. El nodo se toma de la ruta y se vuelve a verificar en el caso de uso de alta. Presentation solo captura entradas, invoca servicios y muestra sus resultados; no aplica reglas de negocio ni construye entidades.
- Ofrecer **«Registrar panel solar»** y **«Cancelar»**. Tras éxito, redirigir al detalle del nodo con una confirmación breve **«Panel solar registrado»**, mostrando la lista recargada desde persistencia. Cancelar vuelve al mismo nodo sin escribir. Usar la redirección después del POST para evitar reenviar el alta al recargar.

### Lista de paneles en el nodo

Agregar al detalle una sección **«Paneles solares»** con nombre, modelo, potencia nominal en W, fecha de instalación hasta segundos, estado, generación actual en W y último mantenimiento. Mostrar fechas de instalación con referencia explícita a UTC y valores numéricos según la cultura de la interfaz. Para derivados ausentes, presentar **«Sin mediciones»** y **«Sin mantenimientos»**; no inventar cero ni fechas.

Mostrar **«Este nodo todavía no tiene paneles solares»** para una consulta exitosa sin paneles. No mostrar IDs como contenido al usuario. Mantener las secciones de nodo, inversor y sensor, el regreso al parque y la navegación existente. La cantidad de paneles y la capacidad del parque deben reflejar el alta al recargar sus vistas.

### Estados, seguridad y errores

- Reutilizar inicio de sesión y acceso denegado. Administrador es el único rol autorizado para carga, envío y consulta del detalle; ocultar un enlace no sustituye la autorización del servidor.
- Ante nombre duplicado o datos inválidos, conservar las entradas y mostrar errores junto al campo. Para duplicados usar **«Ya existe un panel solar con ese nombre en este nodo.»**.
- Ante nodo ausente, mostrar **«No se encontró el nodo»**, sin formulario ni información anterior, con regreso al listado de parques. Un GUID mal formado usa el manejo existente de rutas no encontradas.
- Ante fallo técnico de carga o guardado, mostrar un mensaje general y permitir reintentar, sin afirmar que se guardó o que no existen paneles. Registrar solo un código de diagnóstico depurado, sin SQL, excepciones completas, cookies ni credenciales. Limpiar datos anteriores al cambiar de nodo.

### Verificación prevista de la implementación

| Criterio | Resultado esperado |
| --- | --- |
| CA1: datos | Alta con las tres entradas; ID autogenerado, nodo correcto, fecha del servidor con segundos y estado `Operativo`. Derivados ausentes sin valores ficticios. |
| CA1: validación y unicidad | Rechazar textos vacíos, nombre fuera de límite y potencia ausente o inválida. Rechazar nombres equivalentes en el mismo nodo, incluso con concurrencia; permitirlos en nodos distintos. |
| CA2: seguridad | Anónimo y usuario sin rol no pueden cargar ni enviar por URL directa o POST manipulado; Application rechaza antes del acceso a datos. |
| CA3: navegación | El botón del nodo abre el formulario correcto; cancelar no escribe; éxito regresa al mismo nodo con confirmación y evita reenvío al recargar. |
| CA4: listado | Mostrar estado vacío o los paneles propios, ordenados y persistentes al recargar; un panel de otro nodo no aparece. Verificar escritorio, móvil y navegación por teclado. |
| Consistencia y regresión | Cantidad y capacidad correctas con varios nodos y parques; conservar equipos y paneles previos. Verificar migración con datos existentes, conflicto concurrente, nodo eliminado antes del guardado y fallo sin escritura parcial, en destino aislado. |

Al implementar, ejecutar desde la raíz al finalizar cada capa modificada, antes de avanzar a la siguiente:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Corregir fallos en el código sin alterar los tests estructurales. Crear pruebas unitarias únicamente de Domain. Ubicar auxiliares funcionales, si se necesitan, en `arnes/prompting/tests/req006/` y evidencias depuradas en `artifacts/req006/`, excluidas de Git. Registrar lo ejecutado realmente; una inspección o simulación no constituye aceptación externa real.

Esta fase entrega únicamente `specs.md`; la planificación de tareas y la implementación corresponden a las fases siguientes.
