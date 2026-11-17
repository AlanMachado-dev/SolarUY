# mod007 · Tareas

## Preparación

- [x] Leer las reglas, revisar los diagramas e identificar las vistas, los formatos actuales y la configuración del monitoreo.
- [x] Registrar los criterios y el diseño en requirements.md y specs.md.

## Domain

- [x] Confirmar que el alcance visual conserva las invariantes y la precisión del dominio.

## Application

- [x] Confirmar que los contratos actuales contienen los datos necesarios para mostrar la lectura y el corte local.

## Infrastructure

- [x] Confirmar que los instantes de medición e instalación se recuperan como UTC y que la modificación prescinde de migraciones y cambios de configuración.

## Presentation

- [x] Actualizar el layout, la navegación, el inicio y los componentes visuales compartidos.
- [x] Aplicar formatos es-UY a las cifras de consulta y mostrar las horas en la zona de Uruguay.
- [x] Dar prioridad a la generación instantánea y separar la estimación diaria en el monitoreo.
- [x] Verificar el renderizado con 0 W y sin lectura, la fecha al cruzar la medianoche y la actualización al recibir otra medición.
- [ ] Verificar la presentación en navegadores de escritorio y móvil y la sesión autenticada con datos reales.

## Controles

- [x] Compilar la solución. Se utilizó una configuración temporal de MSBuild para ejecutar en el mismo proceso las tareas de WebAssembly cuyo proceso auxiliar no arrancaba en este entorno. La compilación registró NU1900 por el acceso a los datos de auditoría de NuGet.
- [x] Ejecutar la suite de Domain. Aprobadas 112 de 112.
- [x] Ejecutar la suite de ArchUnitNET. Aprobadas 17 de 17.

VSTest no consiguió conectarse con su proceso auxiliar. Las dos suites se ejecutaron con el runner xUnit en proceso sobre los ensamblados recién compilados.

## Verificación funcional

`dotnet run --project arnes/prompting/tests/mod007/VerificarMonitoreoVisual.csproj` ejecuta la prueba de renderizado con Blazor y mediciones en memoria. Comprueba la ausencia de medición, 0 W, el cambio de fecha local, la actualización por notificación y la ausencia de acciones administrativas para Operador. La prueba no usa MySQL ni el proveedor de identidad. Queda pendiente la aceptación en navegador con una sesión autenticada.

## Corrección del aviso de reconexión

- [x] Cerrar el diálogo desde el marcado inicial y mostrar solo el mensaje del estado recibido de Blazor.
- [x] Comprobar con eventos simulados la apertura, los estados de reintento y fallo, el cierre y la acción de recargar.
- [x] Compilar en una salida aislada y comprobar que el módulo JavaScript figura en el manifiesto de recursos estáticos.
- [x] Ejecutar 112 pruebas de Domain y 17 de ArchUnitNET con xUnit en proceso. VSTest volvió a agotar el tiempo de conexión con su proceso auxiliar.

## Ajuste a la plantilla de Blazor

- [x] Recuperar la paleta, los iconos y el menú adaptable de la plantilla, con los componentes visuales de Bootstrap.
- [x] Compilar en la salida aislada y comprobar que la hoja de estilos generada contiene el degradado, los iconos y el estilo del diálogo de reconexión.
- [x] Ejecutar las 112 pruebas de Domain y las 17 de ArchUnitNET con xUnit en proceso, y la comprobación funcional de renderizado del monitoreo.
