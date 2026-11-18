# mod008 · Tareas de implementación

## Domain

- [x] Confirmar que el cambio de permisos solo requiere reutilizar el comportamiento actual del panel y las mediciones.

## Application

- [x] Revisar que `MonitoreoService` cubra el flujo del sensor y que `ControlPanelService` mantenga la autorización de las acciones existentes.

## Infrastructure

- [x] Confirmar que el registro actual de mediciones admite el flujo sin cambios de esquema.

## Presentation

- [x] Registrar la política del sensor simulado para Administrador y Operador.
- [x] Aplicar la política en `MonitoreoPanel.razor` para cargar el panel y montar el sensor.
- [x] Aplicar la política al endpoint de transmisión.
- [x] Aplicar la política al hub de monitoreo.

## Controles del proyecto

Ejecutar desde la raíz al terminar cada capa que se modifique. Marcar las tareas al verificar el resultado.

- [x] Compilar `SolarUY.slnx`.
- [x] Ejecutar las pruebas de `SolarUY.Domain.Tests`. Pasaron las 135.
- [x] Ejecutar las pruebas de ArchUnitNET en `CodeQuality.Tests`. Pasaron las 17.

La compilación se completó con una salida aislada y las tareas de WebAssembly dentro del proceso de MSBuild. Se usó xUnit en proceso para ejecutar las suites compiladas debido a problemas de conexión entre VSTest y su proceso de pruebas.

## Aceptación funcional

Usar una base de datos aislada para las verificaciones que guardan lecturas o cambian el estado de un panel.

La verificación visual auxiliar de mod007 confirmó que el Operador puede finalizar un mantenimiento y que la vista conserva ocultos los controles exclusivos del Administrador que cubre esa comprobación. La aceptación con cuentas reales y una base aislada queda pendiente.

- [ ] Con una cuenta Operador, abrir el monitoreo de un panel que admite telemetría. Verificar el arranque automático, el guardado de lecturas en W y su actualización en pantalla con el intervalo actual.
- [ ] Provocar una pausa y una reanudación desde una sesión Administrador. Verificar el comportamiento del sensor en la sesión Operador y comprobar que deja de enviar al salir de la pantalla.
- [ ] Comprobar que el Administrador sigue usando el sensor y que los permisos de control y mantenimiento coinciden con los de [requerimientos](requirements.md).
- [ ] Comprobar que una sesión sin ninguno de los roles autorizados queda rechazada tanto por el endpoint de transmisión como por el hub.
- [ ] Abrir el mismo panel desde ambas sesiones y registrar el comportamiento de los envíos simultáneos. Cada pantalla ejecuta su propio sensor.
