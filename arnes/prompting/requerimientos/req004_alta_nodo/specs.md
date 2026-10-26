# req004 — Especificaciones técnicas: Alta de Nodo

## Objetivo y alcance

Registrar, desde el detalle de un parque solar existente, un nodo con exactamente un inversor y un sensor. La operación está reservada a usuarios autenticados con el rol `Administrador` y guarda los tres elementos conjuntamente, sin paneles asignados.

Fuente: [requirements.md](requirements.md). Se respetan `AGENTS.md`, las siete reglas compartidas y los diagramas de `.agents/Images`, sin modificarlos. Las capas continúan como carpetas y namespaces del servidor `SolarUY`, sin nuevos proyectos.

Se reutilizan el detalle de parque incorporado por req003, la autenticación existente y EF Core con MySQL. Se mantiene el formulario Blazor con renderizado estático del servidor (SSR), invocación directa de Application y sin MediatR. No se incorporan API REST, telemetría, conexión al endpoint del sensor, creación de paneles ni edición o eliminación de equipos. La gestión independiente posterior del inversor y del sensor queda fuera de esta entrega.

## 1. Domain

### Modelo y relaciones

- Incorporar `Nodo`, `Inversor` y `Sensor` en `SolarUY.Domain.Nodos`. Cada entidad genera su propio `Guid` no vacío al crearse, siguiendo el tipo de identidad ya utilizado por `ParqueSolar`.
- `Nodo` contiene nombre, latitud y longitud, y referencias obligatorias a su `ParqueSolar` y a su `Inversor`. `Inversor` contiene nombre, número de serie y una referencia obligatoria a su `Sensor`. `Sensor` contiene nombre, número de serie y endpoint opcional.
- Extender `ParqueSolar` con una colección privada de nodos, expuesta como solo lectura. Un método de dominio `AgregarNodo` incorpora un nodo completo al parque y asegura su pertenencia. Las relaciones no tienen setters públicos ni permiten reemplazar el inversor o el sensor en este caso de uso.
- La lista de IDs de nodos requerida para el parque se obtiene de su colección persistida, mediante una proyección de solo lectura `NodosIds`. No mantener una segunda lista editable ni almacenar IDs como texto o JSON. Las claves de las relaciones se representan mediante referencias a objetos en Domain y mediante claves foráneas en Infrastructure.
- Cada nodo pertenece a un único parque; un parque admite múltiples nodos sin límite funcional. Un nodo tiene exactamente un inversor y un inversor exactamente un sensor; no se comparten equipos entre nodos. Un parque recién creado puede continuar sin nodos.

### Invariantes y decisiones de validación

| Dato | Regla |
| --- | --- |
| Nombres del nodo, inversor y sensor | Obligatorios; rechazar valores nulos, vacíos o de espacios y conservar el texto recortado. |
| Números de serie del inversor y del sensor | Obligatorios, con el mismo tratamiento de espacios. El requerimiento no exige unicidad de nombres ni números de serie. |
| Latitud y longitud | Obligatorias; cero es válido. Como decisión de diseño geográfico, usar valores `float` finitos, con latitud entre −90 y 90 y longitud entre −180 y 180, incluidos los extremos. |
| Endpoint | Opcional; ausencia o espacios se normalizan a `null`. Si se informa, conservar el texto recortado. No imponer protocolo ni comprobar conectividad en esta entrega. |
| Relaciones | Rechazar la ausencia de parque, inversor o sensor y la incorporación de un nodo a otro parque. |

Las fábricas y métodos de las entidades aplican las invariantes y mantienen el grafo completo antes de persistir. `AgregarNodo` crea e incorpora el nodo a partir de sus datos y de un inversor ya construido con su sensor; no crea un parque ni persiste elementos como efecto secundario. Las validaciones conocidas se comunican mediante `DatosNodoInvalidosException`, con campo y mensaje, sin dependencias de EF, ASP.NET ni Application.

`CantidadPaneles` es una propiedad derivada de la pertenencia de paneles, sin setter ni columna editable. En este alcance no existe alta ni asociación de paneles: la colección está vacía y el resultado es **0**. No introducir una entidad `Panel` incompleta ni un contador almacenado para anticipar esa funcionalidad. La capacidad del parque conserva su valor actual de **0 MW** al agregar equipos sin paneles.

Durante la implementación, agregar pruebas unitarias de Domain para creación válida, textos obligatorios, coordenadas inválidas, límites geográficos, cero válido, endpoint opcional, relaciones obligatorias, colección protegida, IDs generados, pertenencia al parque, múltiples nodos y cantidad inicial de paneles.

## 2. Application

### Contratos

Definir contratos y DTO en `SolarUY.Application.Nodos` y el servicio concreto en `SolarUY.Application.Nodos.Servicios`, con dependencias exclusivamente hacia Domain y hacia abstracciones de Application.

| Contrato | Responsabilidad |
| --- | --- |
| `INodoService` / `NodoService` | Exponer `CrearAsync(Guid parqueId, CrearNodoDto datos, CancellationToken cancellationToken)` y coordinar únicamente el alta completa. |
| `CrearNodoDto` | Transportar `Nombre`, `Latitud`, `Longitud`, `NombreInversor`, `NumeroSerieInversor`, `NombreSensor`, `NumeroSerieSensor` y `EndpointSensor`. Coordenadas `float?` para distinguir ausencia de cero. |
| `NodoDto` | Devolver ID del nodo, ID del parque, nombre, coordenadas, cantidad de paneles e identificación y datos del inversor y del sensor, listos para mostrar. |
| `ResultadoAltaNodoDto` | Comunicar éxito con `NodoDto`, o errores con código, mensaje y campo del DTO de entrada. |
| `INodoRepository` | Exponer el guardado asíncrono del nuevo nodo con su inversor y sensor como una única operación. No exponer `DbContext`, SQL ni `IQueryable`. |

Reutilizar `IParqueSolarRepository` para obtener el parque y `IUsuarioActualService` para comprobar la identidad confiable del servidor. Las dependencias de `NodoService` se reciben por constructor. El identificador del parque proviene de la ruta y se verifica en el servidor; no forma parte del modelo editable ni se considera una prueba de autorización.

### Flujo de alta

1. Rechazar usuarios no autenticados o sin el rol exacto `Administrador`, antes de consultar datos o escribir.
2. Obtener el parque indicado; devolver `ParqueNoEncontrado` si no existe, sin crearlo automáticamente.
3. Comprobar la presencia de las coordenadas en el DTO. Crear el sensor y el inversor mediante Domain e invocar `ParqueSolar.AgregarNodo` con los datos del nodo. Traducir las validaciones a `DatosInvalidos` y al campo correspondiente; las reglas geográficas y de texto residen en Domain.
4. Solicitar a `INodoRepository` la persistencia del nuevo grafo. Si el parque dejó de existir antes de guardar, devolver `ParqueNoEncontrado` sin dejar equipos huérfanos.
5. Devolver éxito únicamente después de la confirmación, incluyendo las identidades generadas y `CantidadPaneles = 0` obtenido del dominio.

Distinguir `AutenticacionRequerida`, `AccesoDenegado`, `ParqueNoEncontrado` y `DatosInvalidos`. Los fallos técnicos inesperados se propagan al manejo de errores de Presentation sin convertirlos en validaciones. No se agregan búsquedas de duplicados ni restricciones de unicidad funcional no solicitadas.

## 3. Infrastructure

- Implementar `NodoRepository` en `SolarUY.Infrastructure.Persistencia`, con contexto recibido por constructor y duración por solicitud, siguiendo la persistencia existente.
- Incorporar configuraciones EF de `Nodo`, `Inversor` y `Sensor` y registrarlas en `SolarUyDbContext`. Mapear las claves `Guid` con la misma conversión usada por `ParqueSolar` y sin generación adicional de IDs en la base.
- Persistir tablas `Nodos`, `Inversores` y `Sensores`. `Nodos.IdParque` es una FK obligatoria; `Inversores.IdNodo` y `Sensores.IdInversor` son FKs obligatorias con índices únicos para las relaciones uno a uno. Nombres, series y coordenadas son obligatorios; endpoint admite `NULL`. No imponer longitudes funcionales adicionales a los textos.
- No duplicar las relaciones mediante FKs circulares: el ID del inversor del nodo y el ID del sensor del inversor se obtienen de las navegaciones correspondientes. Ignorar en el mapeo `NodosIds` y `CantidadPaneles`, por ser derivados.
- Mapear la colección encapsulada del parque mediante su campo privado. La consulta que entregue un parque con su colección de nodos debe cargarla de manera explícita; no presentar una colección sin cargar como si estuviera vacía. El listado existente de parques no necesita cargar esta topología para mostrar sus datos actuales.
- Guardar nodo, inversor y sensor con una única confirmación transaccional. Asociar el parque existente sin reinsertarlo y añadir solamente el grafo nuevo; no reemplazar toda la colección ni actualizar otros nodos. Altas concurrentes en un mismo parque deben conservar todos los nodos confirmados.
- Las FKs impiden huérfanos y los índices únicos impiden varios inversores por nodo o varios sensores por inversor. La existencia obligatoria del inversor y del sensor se garantiza además mediante el modelo de dominio y el guardado conjunto; una FK por sí sola no garantiza que todo principal tenga un dependiente.
- Ante fallos, revertir la operación completa y descartar o limpiar las entidades pendientes del contexto. Traducir específicamente la pérdida concurrente del parque; otros errores conservan su naturaleza técnica.
- Generar una migración incremental que conserve los parques existentes y sus datos. Registrar `INodoRepository` e `INodoService` en el punto de composición `SolarUY/Program.cs`. Reutilizar autenticación, roles y antifalsificación existentes.

La implementación y las verificaciones con escrituras o fallos utilizarán una base aislada identificada. Conservar `SolarUY/appsettings.Local.json` y la base habitual del usuario; no aplicar migraciones de prueba sobre ella.

## 4. Presentation

### Acceso y formulario

- Agregar en `DetalleParqueSolar.razor` el botón **«+ Agregar nodo»**, visible para Administrador cuando el parque existe. Enlazar a `/parques-solares/{ParqueId:guid}/nodos/nuevo`.
- Crear `SolarUY/Presentation/Pages/AltaNodo.razor`, namespace `SolarUY.Presentation.Pages`, con esa ruta y `[Authorize(Roles = "Administrador")]`. Proteger tanto la carga como el POST en el servidor, además del control en Application. Ocultar el botón no sustituye la autorización.
- Mostrar el parque de destino y un único formulario SSR con nombre explícito, vinculación compatible con POST y antifalsificación. Separar visual y semánticamente tres secciones: **Nodo** (nombre, latitud, longitud), **Inversor** (nombre, número de serie) y **Sensor** (nombre, número de serie, endpoint opcional).
- Identificar campos obligatorios con etiquetas y mensajes accesibles. No solicitar IDs, pertenencia al parque, cantidad de paneles ni relaciones entre equipos; no incluirlos como campos ocultos que determinen el destino.
- Invocar `INodoService.CrearAsync` usando el parque de la ruta. Presentation solo captura datos, comunica errores y muestra DTO; no construye entidades ni calcula cantidades o valida reglas de negocio. Los errores de conversión numérica se muestran como errores de entrada, sin sustituir las validaciones del servidor.
- Ofrecer **«Registrar nodo»** y **«Cancelar»**, con regreso al detalle del parque. Tras confirmar, mostrar **«Nodo registrado»**, los datos devueltos de las tres secciones y la cantidad inicial de paneles, junto con un enlace al detalle. No agregar pantallas de gestión de equipos.

### Estados y aceptación prevista

Conservar las entradas ante validaciones y mostrar cada error junto a su campo. Ante parque inexistente, mostrar «No se encontró el parque solar» sin permitir el alta. Reutilizar el inicio de sesión y acceso denegado existentes. Ante un fallo técnico, mostrar «No se pudo registrar el nodo. Intentá nuevamente.» y registrar solo un diagnóstico depurado, sin excepciones completas, SQL, endpoint ni credenciales. No anunciar éxito si el guardado no fue confirmado.

| Criterio | Verificación durante la implementación |
| --- | --- |
| CA1: nodo | Nombre y coordenadas obligatorios; cero válido; IDs autogenerados y parque correcto; cantidad inicial de paneles igual a cero. |
| CA2: inversor | Nombre y serie obligatorios; exactamente un inversor asociado al nodo, con ID generado y sin IDs editables. |
| CA3: sensor | Nombre y serie obligatorios; endpoint vacío o informado; exactamente un sensor asociado al inversor e ID generado. |
| CA4: seguridad | Administrador puede cargar y enviar; anónimo y usuario de otro rol no pueden crear mediante acceso directo ni POST manipulado. |
| CA5: navegación | Botón con el texto requerido en el detalle correcto y formulario único con tres secciones diferenciadas. |
| Reglas y persistencia | Crear varios nodos en un parque, recargar desde una nueva solicitud y comprobar pertenencia e IDs; verificar cero paneles, ausencia de efectos en otros parques, altas concurrentes y reversión completa ante un fallo controlado. |

Al implementar, verificar cada capa antes de avanzar a la siguiente mediante los controles obligatorios desde la raíz:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Corregir el código ante fallos sin alterar los tests estructurales. Crear pruebas unitarias únicamente para Domain. Los auxiliares funcionales, si se requieren, van en `arnes/prompting/tests/req004/`; las evidencias depuradas en `artifacts/req004/`, excluidas de Git. Una inspección o simulación no constituye aceptación externa real.

La entrega de esta fase es únicamente `specs.md`; la planificación de tareas y la implementación se realizan posteriormente.
