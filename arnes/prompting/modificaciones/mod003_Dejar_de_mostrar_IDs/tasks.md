# mod003 — Plan de implementación: Dejar de mostrar ID

Basado en [requirements.md](requirements.md) y [specs.md](specs.md), nombre del archivo de diseño existente en esta carpeta.

Ejecutar las tareas en el orden indicado. Marcar cada casilla únicamente después de verificar su resultado. El cambio de código se limita a Presentation; las tres primeras capas requieren confirmar que sus contratos y comportamiento se conservan.

## 0. Preparación

- [x] Leer `AGENTS.md`, las siete reglas de `.agents/rules/` en su orden y los documentos de mod003 antes de implementar.
- [x] Revisar el estado de Git e identificar los cambios previos para conservar el trabajo existente.
- [x] Revisar el marcado actual de `SolarUY/Presentation/Pages/ListadoParquesSolares.razor` y `SolarUY/Presentation/Pages/DetalleParqueSolar.razor` para localizar las salidas visibles de los IDs.

## 1. Domain — Confirmación de alcance

- [x] Confirmar que ocultar los IDs en las vistas no requiere modificar entidades, identificadores, relaciones ni invariantes de `SolarUY/Domain/`.

No hay implementación ni nuevas pruebas unitarias de dominio previstas.

## 2. Application — Confirmación de contratos

- [x] Confirmar que `ParqueSolarDto`, `NodoDto`, `InversorDto` y `SensorDto` conservan sus identificadores y propiedades actuales.
- [x] Confirmar que `IParqueSolarService` y sus operaciones de listado y detalle pueden mantenerse sin cambios.

## 3. Infrastructure — Confirmación de persistencia

- [x] Confirmar que la modificación no requiere cambios en repositorios, mapeos, claves, migraciones ni configuración.

Las capas Domain, Application e Infrastructure no tienen tareas de implementación. Si fuera necesario modificar alguna, revisar el alcance y ejecutar los tres controles obligatorios al terminar esa capa, antes de continuar con la siguiente.

## 4. Presentation — Implementación

### 4.1. Listado de parques solares (CA1)

Archivo: `SolarUY/Presentation/Pages/ListadoParquesSolares.razor`.

- [x] Eliminar el encabezado `<th scope="col">ID</th>`.
- [x] Eliminar la celda `<td>@parque.ID</td>` de cada fila.
- [x] Verificar que queden tres encabezados y tres celdas por fila, en el orden Nombre, Ubicación y Capacidad total estimada (MW).
- [x] Verificar que el enlace del nombre conserve la ruta con `parque.ID`, su etiqueta accesible y el `@key` de la fila.

### 4.2. Detalle del parque solar (CA2)

Archivo: `SolarUY/Presentation/Pages/DetalleParqueSolar.razor`.

- [x] Eliminar el par `<dt>ID</dt>`/`<dd>` que muestra `parque.ID`.
- [x] Eliminar el par `<dt>ID</dt>`/`<dd>` que muestra `nodo.ID`.
- [x] Eliminar el par `<dt>ID</dt>`/`<dd>` que muestra `nodo.Inversor.ID`.
- [x] Eliminar el par `<dt>ID</dt>`/`<dd>` que muestra `nodo.Inversor.Sensor.ID`.
- [x] Verificar que se conserven los restantes campos del parque, nodo, inversor y sensor, con sus formatos actuales.
- [x] Verificar que se conserven el parámetro de ruta, los enlaces de recarga y alta de nodo, y las asociaciones `id`/`aria-labelledby` de las tarjetas.

### 4.3. Revisión del código

- [x] Revisar que los campos se hayan retirado del marcado y no se hayan ocultado mediante CSS ni trasladado a etiquetas accesibles o ayudas emergentes.
- [x] Revisar que las listas descriptivas y la tabla mantengan una estructura HTML válida y que se conserven las secciones y encabezados.
- [x] Revisar que los cambios mantengan el acceso mediante `IParqueSolarService`, sin agregar lógica de negocio, dependencias ni acceso directo a Infrastructure.
- [x] Revisar que autenticación, autorización, mensajes, estados vacíos y manejo de errores conserven su implementación.

### 4.4. Controles obligatorios al finalizar Presentation

Ejecutar desde la raíz. Corregir los fallos del código y repetir los controles afectados antes de dar por validada la capa. No modificar los tests estructurales para acomodar la implementación ni generar pruebas unitarias de Application, Infrastructure o Presentation.

- [x] Ejecutar `dotnet build SolarUY.slnx` y verificar que la solución compile sin errores.
- [x] Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y verificar que todas las pruebas de Domain pasen.
- [x] Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y verificar que todas las pruebas de ArchUnitNET pasen.
- [x] Registrar los resultados de los tres controles y distinguir advertencias preexistentes de problemas nuevos.

## 5. Validación funcional y visual

Usar un destino aislado identificado si se necesitan datos de prueba, escrituras o fallos provocados; conservar la base habitual y `SolarUY/appsettings.Local.json`. Guardar cualquier comprobación funcional auxiliar en `arnes/prompting/tests/mod003/` y las evidencias depuradas en `artifacts/`, sin secretos ni tokens.

- [x] Comprobar en la aplicación que el listado con parques no presente la columna ni los valores de ID y conserve los otros datos (CA1).
- [x] Abrir el detalle desde el nombre de un parque y comprobar que corresponda al parque seleccionado.
- [x] Comprobar el detalle de un parque con nodos, inversores y sensores: no deben aparecer las etiquetas ni los valores de sus IDs como datos (CA2).
- [x] Comprobar que el detalle conserve nombres, ubicación, capacidad, coordenadas, cantidad de paneles, números de serie y endpoint.
- [x] Comprobar que las tarjetas de distintos nodos mantengan sus asociaciones accesibles con sus títulos.
- [x] Comprobar el regreso al listado y la disponibilidad del enlace de alta de nodo según el rol, sin crear datos en la base habitual.
- [x] Comprobar los estados de listado vacío y parque sin nodos en el destino aislado.
- [x] Comprobar autenticación, parque no encontrado, mensajes de creación y errores con recarga; utilizar el destino aislado para crear registros o provocar fallos.
- [x] Comprobar ambas pantallas en anchos de escritorio y móvil, sin columnas vacías ni desajustes en los datos restantes.
- [x] Registrar el entorno y los resultados observados; dejar pendientes las comprobaciones no ejecutadas y distinguirlas de las inspecciones de código.

## 6. Cierre

- [x] Revisar el diff final y confirmar que los cambios de código se limiten a los dos componentes previstos.
- [x] Confirmar que `arnes/prompting/requerimientos/`, sus subcarpetas y los diagramas de `.agents/Images/` permanezcan intactos.
- [x] Actualizar las casillas según las verificaciones realizadas y comunicar el estado de CA1, CA2 y los controles, incluyendo cualquier pendiente real.

## Registro de ejecución — 27/10/2026

- Alcance confirmado por inspección: Domain, Application e Infrastructure sin modificaciones. El diff de la aplicación contiene únicamente seis eliminaciones en los dos componentes previstos. Contratos, rutas, claves, asociaciones accesibles y comportamiento restante conservados.
- Compilación: correcta, 0 errores. Domain: 50/50. ArchUnitNET: 17/17. No se modificaron pruebas estructurales ni se crearon pruebas unitarias de otras capas.
- La aplicación del usuario estaba abierta y bloqueaba su ejecutable y DLL. Los tres controles se completaron con `-p:UseArtifactsOutput=true -p:ArtifactsPath=C:/estudio/NET/SolarUY/artifacts/mod003/build`, sin detener esa aplicación. Un intento previo en el entorno restringido no pudo iniciar el proceso auxiliar de WebAssembly; se repitió con los procesos habilitados.
- Advertencias preexistentes: NU1903 por dos avisos del paquete Microsoft.AspNetCore.Components.WebAssembly.Server 10.0.12. Ya estaban presentes en la compilación anterior al cambio. No hubo errores nuevos de código.
- Validación funcional ejecutada: 44 comprobaciones HTTP SSR con componentes, servicios y MySQL reales sobre `solaruy_mod003_f5ab1a088ea8`, host auxiliar `http://127.0.0.1:8193`. CA1 y CA2 correctos. Se comprobaron listado vacío, parque sin nodos, parque con dos nodos/inversores/sensores, campos restantes, enlaces, asociaciones accesibles, permisos, redirección de anónimos, parque inexistente, alta real aislada y mensaje de creación.
- Errores y recarga: fallo de lectura controlado mediante interceptor en el host aislado; se observó el mensaje y el enlace de recarga en ambas páginas y la recuperación al retirar el fallo. No se simuló una aceptación externa ni se provocó una caída de MySQL.
- Validación visual ejecutada en navegador: ambas pantallas a 1366 × 900 y 390 × 844. Tres columnas sin celdas vacías; tarjetas con campos completos y secciones apiladas en móvil; sin desbordamiento horizontal. Se abrió el detalle desde el nombre y se volvió al listado. Dos tarjetas conservaron sus asociaciones accesibles. Capturas guardadas en `artifacts/mod003/`.
- Entorno auxiliar detenido tras verificar; tamaño del navegador restaurado. Base habitual, configuración privada, requerimientos y diagramas conservados.
- Alcance de autenticación comprobado: sesiones auxiliares locales, permisos y redirecciones reales del host SSR. No se ejecutó inicio/cierre de sesión con el proveedor OIDC externo; esta ejecución no acredita esa aceptación externa.
- Arnés reproducible: `arnes/prompting/tests/mod003/`. Evidencias depuradas y comandos/resultados: `artifacts/mod003/`. Las casillas reflejan las inspecciones y ejecuciones descritas; no quedan comprobaciones de mod003 pendientes dentro de este alcance.
