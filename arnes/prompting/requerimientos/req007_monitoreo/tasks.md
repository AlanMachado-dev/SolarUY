# REQ007 — Tareas de monitoreo en tiempo real

Seguir [requirements.md](requirements.md) y [specs.md](specs.md). Marcar cada casilla después de verificarla. Ejecutar los tres controles desde la raíz al cerrar cada capa y avanzar solo cuando pasen. Corregir los fallos en el código de implementación y registrar las advertencias preexistentes por separado.

## Domain

- [x] Crear `Medicion` con identidad, referencia obligatoria a `Panel`, fecha UTC, `GeneracionActualW` y `EstadoPanel`. Admitir cero o más mediciones por panel.
- [x] Encapsular la creación de la entidad y rechazar panel ausente, fechas fuera de UTC, estados fuera del enumerado y potencias negativas o no finitas.
- [x] Agregar pruebas xUnit para la creación válida, incluida potencia cero, y para cada entrada inválida.
- [x] Ejecutar los tres controles de cierre de capa.

## Application

- [x] Definir los DTO de ingreso y consulta con los datos requeridos y la identidad necesaria para ordenar las lecturas.
- [x] Definir `IMedicionRepository` para guardar y consultar la última lectura y el puerto para notificar lecturas confirmadas.
- [x] Implementar la consulta en `MonitoreoService`. Distinguir panel inexistente, panel sin historial y panel con mediciones. Para el panel sin historial, devolver su estado registrado y generación no disponible.
- [x] Implementar el registro en `MonitoreoService`. Resolver el panel, asignar la fecha UTC del servidor y crear la medición mediante Domain.
- [x] Notificar solo tras confirmar la persistencia y devolver un error al sensor si falla la escritura.
- [x] Definir el orden por fecha UTC con la identidad como desempate estable para la consulta y la reconciliación de actualizaciones.
- [x] Ejecutar los tres controles de cierre de capa.

## Infrastructure

- [x] Mapear `Medicion` en EF Core con clave foránea obligatoria al panel e índice que facilite la consulta ordenada de su última medición.
- [x] Generar la migración de la tabla `Medicion` y comprobar en una base aislada que los paneles existentes comienzan con historial vacío.
- [x] Implementar `MedicionRepository` y registrar las dependencias en el arranque. Consultar la última lectura con el orden acordado y usar ámbitos de persistencia breves por operación.
- [x] Ejecutar los tres controles de cierre de capa.

## Presentation

- [x] Agregar el botón **Monitoreo** por panel en `DetalleNodo.razor` y crear la página interactiva de servidor que muestra estado, potencia en W y fecha, con una indicación cuando aún no hay mediciones.
- [x] Autorizar en servidor la página y la suscripción para Administrador y Operador.
- [x] Configurar el hub SignalR para que solo Administrador pueda enviar lecturas y delegar el registro a Application, que comprueba la existencia del panel.
- [x] Implementar y registrar el adaptador de notificación con suscripciones por panel. Suscribir la vista antes de la consulta inicial, reconciliar ambas fuentes con el orden de Application y liberar la suscripción al salir o cambiar de panel.
- [x] Crear `InversorSimulado` en `SolarUY.Client` con estados válidos y potencias coherentes con la potencia nominal.
- [x] Habilitar el sensor WebAssembly para el panel abierto en una sesión de Administrador. Consultar el inversor y enviar por SignalR cada 30 segundos sin solapar envíos. Iniciar al activarse, cancelar al salir o cambiar de panel y reanudar al recuperar la conexión.
- [x] Ejecutar los tres controles de cierre de capa.

## Controles de cierre de capa

~~~powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
~~~

## Verificación funcional

Usar una base aislada identificada. Guardar las evidencias depuradas en `artifacts/` y, si se necesitan pruebas auxiliares, ubicarlas en `arnes/prompting/tests/req007/`.

- [ ] Abrir el monitoreo desde el detalle de un nodo con cada rol autorizado y comprobar la última medición persistida y la vista de un panel sin historial.
- [ ] Observar el ciclo de 30 segundos del sensor en una sesión de Administrador. Verificar que cada lectura quede asociada al panel correcto y que ambas vistas autorizadas se actualicen tras guardarla.
- [ ] Comprobar que las lecturas de otro panel se excluyen y que una actualización atrasada no reemplaza una lectura más reciente.
- [ ] Interrumpir la conexión y comprobar que el sensor retoma el envío al reconectar y deja de enviar para el panel anterior al salir o cambiar de panel.
- [ ] Provocar un fallo de escritura en el entorno aislado y comprobar que el sensor recibe un error y las vistas conservan la última lectura confirmada.
- [ ] Comprobar que Operador no puede publicar y que una sesión sin autorización no puede abrir el monitoreo, suscribirse ni publicar.
