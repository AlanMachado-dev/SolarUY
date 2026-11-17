# req012 — Especificaciones de mantenimiento de panel solar

Alcance definido en [requirements.md](requirements.md), sobre el monitoreo individual y las acciones de [req011](../req011_acciones_paneles/specs.md).

## Domain

- Incorporar `Mantenimiento` como entidad de `Panel`, con navegación al panel e historial de cero a muchos registros, según el [modelo de dominio](../../../../.agents/Images/Modelo_de_Dominio.png). El dominio genera un `Guid` único y valida `Tipo` y `Responsable` como textos obligatorios y `FechaProgramada` como fecha válida. `Observaciones` es opcional. `FechaRealizada` queda vacía hasta finalizar y registra la fecha y hora del servidor al confirmar el cierre. `Panel.UltimoMantenimiento` se obtiene de la fecha realizada más reciente, si existe.
- Admitir un mantenimiento sin finalizar por panel. Pasa a estar en curso al llegar a la fecha programada y permanece abierto hasta su finalización. Si al confirmar la programación la fecha ya llegó, el mantenimiento empieza en ese momento. El panel valida las transiciones mediante sus propios métodos.

| Acción | Estado de control resultante |
| --- | --- |
| Programar para una fecha futura | Conserva el estado actual hasta el inicio. |
| Comenzar el mantenimiento | `EnMantenimiento`, salvo si el panel está `EnFalla` o `Desactivado`, en cuyo caso conserva ese estado. |
| Simular falla o desactivar | `EnFalla` o `Desactivado`, según la orden confirmada más reciente. El mantenimiento conserva su ciclo. |
| Simular arreglo desde `EnFalla` o activar desde `Desactivado` | `EnMantenimiento` si hay un mantenimiento en curso, u `Operativo` en los demás casos. |
| Finalizar un mantenimiento en curso | `Operativo` si estaba `EnMantenimiento`. Si estaba `EnFalla` o `Desactivado`, conserva ese estado hasta arreglarlo o activarlo. |

- Una orden repetida deja el estado intacto. Un arreglo o una activación enviados desde una vista desactualizada solo pueden levantar el estado que les corresponde. Mientras el estado efectivo sea distinto de `Operativo`, la potencia actual es 0 W y se rechazan las lecturas del sensor.
- Agregar pruebas de Domain para datos obligatorios, inicio y cierre, programación duplicada, órdenes repetidas, alternancia entre falla y desactivación y recuperación con mantenimiento pendiente, en curso o finalizado.

## Application

- Ampliar `ControlPanelService` y su contrato de persistencia con órdenes explícitas para los cambios de estado y el ciclo de mantenimiento. Validar en servidor que el usuario esté autenticado. Autorizar a Administrador para simular falla o arreglo y para desactivar o activar. Autorizar a Administrador y Operador para programar y finalizar. Dirigir la ejecución de los vencimientos a un caso de uso interno.
- Usar `TimeProvider` como reloj del servidor y operar en UTC. Devolver al monitoreo el estado de control confirmado, el estado del mantenimiento, las acciones disponibles y el resultado de cada orden. Incluir el ID del mantenimiento en la orden de cierre para que un reintento no cierre otro registro. Publicar los cambios confirmados mediante `INotificacionMedicion`, incluso si solo cambia el mantenimiento.
- Extender `MonitoreoService` para que el sensor consulte si puede transmitir y obtenga la revisión de control vigente. Exigir esa revisión al registrar una muestra. Devolver un resultado de pausa o revisión desactualizada cuando corresponda, de modo que el sensor vuelva a consultar antes de enviar. Los datos operativos del sensor informan potencia y el servidor fija el estado de esas lecturas.

## Infrastructure

- Mapear `Mantenimiento` con EF Core y MySQL, con clave única, relación obligatoria a `Panel`, fechas en UTC e índice para consultar programaciones vencidas. Guardar cuándo se procesó el inicio para distinguir un registro pendiente de uno en curso, incluso si una falla o desactivación oculta el mantenimiento. Agregar la migración y proyectar el último mantenimiento realizado para el detalle existente.
- Procesar vencimientos en segundo plano desde el arranque y periódicamente, también para paneles sin sesiones abiertas. Reutilizar el bloqueo de la fila del panel para serializar órdenes, vencimientos y guardado de muestras. Volver a comprobar bajo ese bloqueo si el mantenimiento ya debe comenzar antes de decidir una orden o admitir telemetría. Así, un reinicio o varias instancias pueden retomar el trabajo sin duplicar el inicio ni aceptar lecturas posteriores al vencimiento durante la pausa.
- Guardar juntos el mantenimiento, el estado del panel, su revisión de control y, cuando cambie el estado efectivo, un registro de control de 0 W en `Medicion`. Este registro marca el corte o la reanudación para la estimación de energía. Si el mantenimiento estaba programado antes de vencer, registrar el corte desde la fecha programada, respetando el orden de las mediciones. Reutilizar la sincronización entre instancias también para paneles con sensores conectados. Ordenar las actualizaciones por revisión de control y, dentro de ella, por fecha e ID de medición. Así también llegan las programaciones que todavía no cambian la potencia.

## Presentation

- En `LecturaEnVivo.razor`, renderizar las acciones informadas por Application. «Programar Mantenimiento» abre un modal con Tipo, Fecha Programada y Responsable. Mostrar la fecha en hora de Uruguay y convertirla a UTC al enviar. Durante la espera, mostrar la fecha confirmada. Cuando el mantenimiento está en curso, el mismo botón pasa a «Finalizar Mantenimiento» y abre un modal con Observaciones. La finalización sigue disponible si una falla o una desactivación cambia el estado visible. El ID y la fecha realizada se generan en servidor.
- Mostrar «Simular Falla» o «Simular Arreglo» y «Desactivar Panel» o «Activar Panel» según el estado confirmado. Tras desactivar un panel en falla, mostrar «Simular Falla» y «Activar Panel». Durante la pausa, mostrar el estado efectivo y 0 W. Deshabilitar el envío mientras haya una orden en curso y mostrar la confirmación o el error sin anticipar cambios de estado.
- En `MonitoreoHub`, asociar el sensor conectado al panel monitoreado para enviarle por SignalR la confirmación de pausa. Al recibirla, `SensorMonitoreo.razor` detiene la conexión de telemetría. Exponer una consulta de control autenticada para Administrador y usarla periódicamente durante la pausa para reconectar cuando el servidor habilite las lecturas. Consultar el estado al cargar o reconectar y antes de enviar. Si el servidor rechaza una lectura por un cambio de control, volver a consultar en ese ciclo. `InversorSimulado` solo elige al azar la potencia de las lecturas operativas.
