# mod003 — Especificaciones: Dejar de mostrar ID

## Alcance

Aplicar los dos criterios de `requirements.md` mediante cambios visuales en el listado y el detalle de parques solares. Conservar los demás datos y comportamientos existentes. No modificar `arnes/prompting/requerimientos/` ni sus subcarpetas, ni los diagramas de `.agents/Images/`.

## 1. Domain

Sin cambios. Las entidades mantienen sus identificadores, relaciones e invariantes. Esta modificación no incorpora reglas de negocio ni requiere nuevas pruebas unitarias de dominio.

## 2. Application

Sin cambios. Conservar los servicios y los contratos actuales, incluidos los identificadores de `ParqueSolarDto`, `NodoDto`, `InversorDto` y `SensorDto`. Las consultas de listado y detalle mantienen su funcionamiento.

## 3. Infrastructure

Sin cambios. Conservar repositorios, mapeos y claves de persistencia. No se requieren migraciones ni modificaciones de datos o configuración.

## 4. Presentation

Modificar únicamente el marcado de los componentes existentes en `SolarUY/Presentation/Pages/`, manteniendo la comunicación con Application mediante `IParqueSolarService`.

### Listado de parques solares

En `ListadoParquesSolares.razor`:

- Eliminar el encabezado `ID` y la celda que muestra `parque.ID` en cada fila.
- Mantener las columnas Nombre, Ubicación (localidad / dirección) y Capacidad total estimada (MW), en ese orden.
- Conservar el enlace del nombre al detalle y el uso de `parque.ID` en la ruta y en `@key`.

### Detalle de parque solar

En `DetalleParqueSolar.razor`, eliminar los pares de elementos `<dt>ID</dt>` y `<dd>` correspondientes a:

- Parque solar: `parque.ID`.
- Nodo: `nodo.ID`.
- Inversor: `nodo.Inversor.ID`.
- Sensor: `nodo.Inversor.Sensor.ID`.

Conservar los nombres, ubicación, capacidad, coordenadas, cantidad de paneles, números de serie y endpoint existentes, así como las secciones y su organización visual.

### Comportamiento y accesibilidad

- Retirar los campos del marcado renderizado; no ocultarlos mediante CSS ni trasladarlos a etiquetas accesibles o ayudas emergentes.
- Conservar los identificadores técnicos de rutas, parámetros, enlaces y atributos HTML `id`/`aria-labelledby` que permiten navegar y asociar las tarjetas de nodos con sus títulos. El alcance es la información presentada como contenido al usuario; los identificadores continúan utilizándose internamente.
- Mantener autenticación, permisos, alta de nodos, regreso al listado, mensajes de creación, estados vacíos, errores y recarga.
- Mantener la estructura semántica de tabla, listas descriptivas y encabezados, sin agregar lógica de negocio ni dependencias.

## Verificación de la implementación

| Criterio | Resultado esperado |
| --- | --- |
| CA1: listado sin ID | La tabla contiene las tres columnas indicadas y no presenta el ID de ningún parque como dato. El enlace de cada nombre abre su detalle correspondiente. |
| CA2: detalle sin IDs | Un parque con nodos, inversores y sensores no presenta los cuatro tipos de ID como datos ni sus etiquetas; conserva los demás campos y las asociaciones accesibles de cada nodo. |
| Regresión de presentación | Se mantienen los estados sin parques y sin nodos, los mensajes existentes y las acciones disponibles según el rol. |

Al completar la capa Presentation, ejecutar desde la raíz los controles obligatorios:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Verificar también las dos pantallas en la aplicación y su disposición en anchos de escritorio y móvil. No generar pruebas unitarias de Presentation ni alterar los tests estructurales. Si se requieren comprobaciones funcionales auxiliares, ubicarlas en `arnes/prompting/tests/mod003/` y guardar evidencias depuradas en `artifacts/`, sin modificar la base habitual del usuario. Registrar qué se ejecutó realmente y distinguirlo de una inspección del código.
