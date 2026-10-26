# mod002 — Especificaciones técnicas: Cambios Variados

## Alcance y decisiones de diseño

Este diseño cubre los cuatro criterios de `requirements.md`: redirigir al detalle tras crear un parque, confirmar su creación en ese detalle, listar sus nodos y evitar números de serie repetidos de inversores y sensores.

Se mantiene la estructura actual de .NET 10 y Blazor, con servicios de Application, repositorios de Infrastructure y persistencia EF Core/MySQL. Se respetan los diagramas de `.agents/Images` y las reglas compartidas. No se modificará ningún archivo de `arnes/prompting/requerimientos/` ni se incorporarán funcionalidades de paneles o telemetría.

Decisiones para concretar aspectos no definidos en los requerimientos:

- **Unicidad por tipo:** una serie no puede repetirse entre inversores de ningún parque; la misma regla se aplica entre sensores de cualquier parque. Un inversor y un sensor sí pueden compartir el mismo texto, porque son conjuntos independientes.
- **Comparación:** se conserva el recorte de espacios externos existente; se distinguen mayúsculas, minúsculas y acentos, sin eliminar espacios internos. Por ejemplo, ` INV-01 ` y `INV-01` son la misma serie; `inv-01` es otra.
- **Longitud:** se propone un máximo de 255 caracteres por serie para sustituir las columnas `longtext` por columnas completamente indexables. Es una restricción nueva, necesaria para este diseño de índices, que debe revisarse junto con estas especificaciones; nunca se truncarán valores existentes.

## 1. Domain

### Números de serie

- Conservar `Inversor.NumeroSerie` y `Sensor.NumeroSerie` como propiedades encapsuladas, sus fábricas `Crear` y las relaciones `ParqueSolar → Nodo → Inversor → Sensor`.
- Mantener la obligatoriedad y el `Trim()` en las fábricas; validar el máximo de 255 caracteres sobre el valor recortado. Los rechazos usarán `DatosNodoInvalidosException` y los campos existentes `NumeroSerieInversor` o `NumeroSerieSensor`.
- La unicidad abarca entidades de distintos parques y no puede comprobarse con la colección de un parque. Domain mantiene las invariantes locales; Application coordina la comprobación global mediante un contrato de repositorio y la persistencia garantiza la exclusión concurrente. Las entidades no consultan bases de datos ni reciben servicios de infraestructura.
- No cambiar `CantidadPaneles` ni `CapacidadTotalEstimadaMw` para mostrar los nodos.

### Verificación del dominio

Ampliar las pruebas xUnit de Domain para cubrir series vacías, recorte, conservación de mayúsculas/acentos y los límites de 255 y 256 caracteres en ambos equipos. Las pruebas locales de entidades no deben simular que garantizan unicidad global.

## 2. Application

### Detalle con nodos

- Ampliar `ResultadoDetalleParqueSolarDto` con `IReadOnlyList<NodoDto> Nodos`. Mantener `ParqueSolarDto` sin colecciones para no cambiar el contrato del listado ni del alta de parques.
- En `ParqueSolarService.ObtenerPorIdAsync`, mapear los nodos del parque consultado usando los contratos existentes `NodoDto`, `InversorDto` y `SensorDto`. Entregar nombre, identificadores, coordenadas, cantidad de paneles y datos de ambos equipos, incluido el endpoint opcional del sensor.
- Entregar la colección ordenada por nombre y luego por ID desde Application; Blazor solo la renderiza. Un parque sin nodos devuelve una colección vacía. Los resultados de autenticación requerida o parque inexistente conservan sus códigos y devuelven una colección vacía.
- Mantener la autorización actual: lectura del detalle para usuarios autenticados y altas para administradores.

### Alta de nodos y conflictos de series

- Ampliar `INodoRepository` con consultas globales `ExisteNumeroSerieInversorAsync` y `ExisteNumeroSerieSensorAsync`, sin parámetro de parque, sobre las series normalizadas por Domain.
- En `NodoService.CrearAsync`, conservar los controles de autorización, existencia del parque y validaciones de Domain. Antes de guardar, consultar las dos series y devolver errores asociados a los campos correspondientes si ya existen. Si ambas están repetidas en esta comprobación, informar ambos errores.
- Ampliar `CodigoErrorAltaNodo` con `NumeroSerieInversorDuplicado` y `NumeroSerieSensorDuplicado`. Mensajes: «Ya existe un inversor con ese número de serie.» y «Ya existe un sensor con ese número de serie.». No revelar a qué parque pertenece el equipo existente.
- Ampliar `ResultadoGuardadoNodo` con `ConflictoNumeroSerieInversor` y `ConflictoNumeroSerieSensor`. Traducir estos resultados a los mismos errores de campo que la comprobación previa. Una colisión concurrente puede informar primero el índice que haya rechazado la escritura.
- Devolver éxito únicamente después de persistir el conjunto completo. Ningún conflicto se interpreta como alta satisfactoria.

El contrato de alta de parques ya devuelve el ID creado; no necesita cambios para la redirección. Se mantiene la inyección por constructor y no se agregan MediatR ni nuevos patrones de coordinación.

## 3. Infrastructure

### Lectura y consultas de existencia

- Reutilizar `ParqueSolarRepository.ObtenerPorIdAsync`, que ya carga `Nodos → Inversor → Sensor` mediante `Include`/`ThenInclude` y `AsNoTracking`. No agregar consultas por cada fila ni cargar nodos en el listado general de parques.
- Implementar las consultas de existencia de series en `NodoRepository` usando `AnyAsync` sobre todos los inversores o sensores. La comparación debe coincidir con la usada por los índices únicos.

### Garantía de unicidad

- En `InversorConfiguration` y `SensorConfiguration`, mapear `NumeroSerie` como `varchar(255)` obligatorio y crear índices únicos completos `UX_Inversores_NumeroSerie` y `UX_Sensores_NumeroSerie`. No usar índices de prefijo, índices compuestos con el parque ni una consulta previa como única protección.
- Configurar explícitamente una comparación binaria sin padding compatible con la versión de MySQL utilizada, de forma que la igualdad coincida con las decisiones de comparación anteriores. Validar esa equivalencia en la base aislada antes de aplicar la migración a datos habituales.
- Mantener la transacción de `NodoRepository.GuardarAsync`: nodo, inversor y sensor se confirman juntos; cualquier conflicto revierte todo el alta y libera el estado de seguimiento.
- Capturar únicamente las violaciones de unicidad de los dos índices identificados y traducirlas a `ResultadoGuardadoNodo`. Conservar el tratamiento de parque eliminado y propagar otros fallos técnicos. No confundir otros errores de integridad con series repetidas ni mostrar mensajes SQL al usuario.

### Migración y datos existentes

Crear una migración nueva y actualizar el snapshot, sin reescribir migraciones anteriores. Antes de cambiar columnas o crear índices, comprobar longitudes y duplicados por tipo usando el recorte y la comparación definidos. Si hay incompatibilidades, detener la aplicación de la migración antes de alterar datos y comunicar el bloqueo de forma depurada. No truncar, renombrar ni eliminar equipos automáticamente.

Probar la migración con datos representativos y destino aislado; conservar `SolarUY/appsettings.Local.json` y la base habitual del usuario.

## 4. Presentation

### Alta de parque y confirmación

- En `AltaParqueSolar.razor`, inyectar `NavigationManager` y, cuando `CrearAsync` devuelva éxito, navegar a `/parques-solares/{id}?creado=true`, usando exclusivamente el ID devuelto por Application. Sustituir la confirmación local actual por esta redirección y conservar los errores en el formulario cuando el alta falle.
- En `DetalleParqueSolar.razor`, recibir `creado` con `SupplyParameterFromQuery` y mostrar «El parque solar se creó correctamente.» en una alerta de éxito accesible (`role="status"`) solo si el detalle se cargó correctamente y el indicador está activo.
- El parámetro es una señal visual de navegación: no crea registros ni acredita permisos o existencia del parque. Mantener el flujo actual de formularios de servidor; no incorporar un modo interactivo únicamente para este aviso. Recargar el detalle no vuelve a ejecutar el alta.
- Mantener visible «+ Agregar nodo» para administradores en el detalle de destino.

### Lista de nodos

- Mostrar una sección «Nodos» en el detalle con los datos recibidos de Application: ID y nombre del nodo, latitud, longitud, cantidad de paneles; ID, nombre y serie del inversor; ID, nombre, serie y endpoint del sensor. Usar «Sin endpoint» cuando corresponda.
- Mostrar «El parque solar todavía no tiene nodos.» si la colección está vacía. Usar una tabla adaptable o una lista de fichas con encabezados claros y conservar el formato cultural de las coordenadas.
- Cada visita al detalle consulta nuevamente Application. Al volver desde la confirmación de alta de nodo, debe verse el nodo persistido; se conserva el enlace de retorno existente de `AltaNodo.razor`.
- En `AltaNodo.razor`, presentar los errores de series duplicadas junto a sus campos mediante el mecanismo actual de `ValidationMessageStore`, conservar lo ingresado y permitir corregir y reenviar. La UI no decide unicidad, no consulta Infrastructure ni recalcula datos del dominio.

### Aceptación y controles de la implementación posterior

| Criterio | Verificación esperada |
| --- | --- |
| 1. Redirección | Un alta válida termina en el detalle del ID creado, con alerta de éxito y acceso a agregar nodo para el administrador. Un alta fallida conserva el formulario y sus errores. |
| 2. Nodos visibles | Un parque sin nodos muestra el estado vacío. Tras crear uno y volver al detalle, aparece con los datos guardados; otros parques no lo muestran. Se conserva tras recargar. |
| 3. Serie de inversor única | Repetirla en el mismo parque o en otro rechaza el alta en el campo del inversor y no deja registros parciales. |
| 4. Serie de sensor única | Repetirla en el mismo parque o en otro rechaza el alta en el campo del sensor y no deja registros parciales. |

Verificar además series con espacios externos, diferencias de mayúsculas/acentos, límites de longitud, el mismo texto en tipos distintos y dos altas concurrentes que compitan por cada índice: solo una se confirma y la otra recibe un error de campo. Comprobar la migración tanto con datos compatibles como con duplicados o series demasiado largas, sin pérdida de datos.

Al finalizar cada capa de implementación, ejecutar desde la raíz y aprobar antes de avanzar:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

No crear pruebas unitarias en Application, Infrastructure ni Presentation ni modificar los tests estructurales. Las comprobaciones funcionales auxiliares, si se necesitan, van en `arnes/prompting/tests/mod002/`; las evidencias depuradas, en `artifacts/`, sin secretos. Estas verificaciones quedan previstas para la implementación: este documento no constituye una aceptación funcional ejecutada.
