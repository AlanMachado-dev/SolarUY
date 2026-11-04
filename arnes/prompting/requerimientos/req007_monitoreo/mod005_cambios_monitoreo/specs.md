# mod005 — Especificaciones técnicas de monitoreo de paneles

Esta modificación se aplica sobre el flujo de monitoreo de req007. El detalle del nodo muestra la última medición persistida de cada panel y la pantalla de monitoreo presenta la generación actual mediante un medidor radial. El sensor consulta el inversor simulado cada 20 segundos.

## Domain

Reutilizar `Panel` y `Medicion` con sus invariantes actuales. La potencia se expresa en W y la fecha de medición en UTC. El cambio de cadencia pertenece al sensor y la escala del medidor pertenece a la presentación.

## Application

El servicio que arma el detalle del nodo debe solicitar las últimas mediciones en lote mediante `IMedicionRepository` para los identificadores de los paneles del nodo. Incorporar al DTO de cada fila su lectura asociada, con generación, estado y fecha. La ausencia de lectura se representa explícitamente y se conserva la distinción entre una potencia de 0 W y un panel sin mediciones. Mantener en `MonitoreoService` la consulta inicial de la pantalla individual y la publicación de cada lectura después de guardarla correctamente.

## Infrastructure

Implementar la consulta en lote en `MedicionRepository`, con una consulta EF Core de solo lectura restringida a los paneles solicitados. Seleccionar como máximo una medición por panel, con el mismo orden que `ObtenerUltimaAsync` (fecha UTC descendente y, en caso de empate, ID descendente). Si el nodo carece de paneles, devolver un resultado vacío. Reutilizar la tabla y el mapeo de mediciones existentes.

## Presentation

- **Detalle del nodo.** Renderizar la generación en W, el estado y la fecha de la última lectura incluida en el DTO de cada panel. Mostrar «Sin mediciones» para los paneles sin lectura persistida.
- **Monitoreo individual.** En `LecturaEnVivo.razor`, usar un medidor radial de Radzen Blazor para «Generación actual». Mostrar el valor exacto en W como texto accesible y conservar el estado y la fecha. Usar la potencia nominal como referencia de escala cuando sea válida y permitir que la escala cubra una lectura que la supere. Si falta la lectura, mostrar «No disponible». Actualizar el medidor con la misma suscripción y el mismo filtro de orden de lecturas que usa la vista actual.
- **Sensor WebAssembly.** En `SensorMonitoreo.razor`, cambiar el intervalo periódico a 20 segundos. Conservar el comportamiento de arranque y cancelación del sensor y el envío por SignalR al servidor. Mantener el acceso al monitoreo para Administrador y Operador.
- **README general.** Explicar que `InversorSimulado` genera lecturas de potencia y estado en WebAssembly. Documentar que el sensor lo consulta cada 20 segundos y envía las lecturas mediante SignalR, que el servidor las asocia al panel y que la vista se actualiza tras la persistencia. Alinear la descripción del flujo con `Diagrama_WebAssembly.png`.

La aceptación funcional debe cubrir un nodo con varios paneles, incluyendo un panel sin lecturas y otro cuya última lectura sea de 0 W. En el monitoreo individual, verificar la lectura inicial, el intervalo de 20 segundos y la actualización del medidor tras guardar una nueva medición.
