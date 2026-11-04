# mod005 — Tareas de implementación

Fuentes de alcance [requirements.md](requirements.md) y [specs.md](specs.md). Marcar cada casilla después de verificarla.

## 1. Domain

- [x] Confirmar que `Panel` y `Medicion` ya representan la relación entre panel y medición, la potencia en W, el estado y la fecha UTC. Reutilizar las invariantes actuales.
- [x] Ejecutar los controles de salida para establecer la línea base antes de modificar Application.

## 2. Application

- [x] Incorporar al DTO de cada fila del detalle del nodo una lectura opcional con generación, estado y fecha UTC. Conservar el valor de 0 W cuando existe una lectura.
- [x] Definir en `IMedicionRepository` el contrato para obtener en lote la última medición de cada panel solicitado. Aplicar la firma y su implementación en el mismo cambio compilable (primera tarea de Infrastructure).
- [x] Inyectar `IMedicionRepository` por constructor en el servicio del detalle del nodo y solicitar las últimas mediciones una sola vez para los identificadores de sus paneles.
- [x] Asociar cada lectura con su fila por ID de panel y representar explícitamente las lecturas ausentes.
- [x] Verificar que `MonitoreoService` conserva la consulta inicial individual y publica las lecturas después de guardarlas.
- [x] Ejecutar los controles de salida con el contrato y su implementación disponibles.

## 3. Infrastructure

- [x] Implementar el contrato en `MedicionRepository` mediante una consulta EF Core de solo lectura. Restringirla a los paneles solicitados y recuperar como máximo una medición por panel, ordenando por fecha UTC descendente y luego por ID descendente, como en `ObtenerUltimaAsync`.
- [x] Resolver una solicitud vacía con un resultado vacío y reutilizar la tabla y el mapeo existentes.
- [x] Ejecutar los controles de salida antes de avanzar a Presentation.

## 4. Presentation

- [x] En `DetalleNodo.razor`, mostrar la generación en W, el estado y la fecha a partir del DTO de cada fila. Mostrar «Sin mediciones» cuando la lectura esté ausente.
- [x] En `LecturaEnVivo.razor`, presentar «Generación actual» con un medidor radial de Radzen Blazor y un texto accesible con el valor exacto en W. Conservar el estado y la fecha. Mostrar «No disponible» cuando falte la lectura.
- [x] Usar la potencia nominal válida como referencia de escala del medidor y extender el rango visual cuando la lectura la supere.
- [x] Vincular el medidor a la lectura vigente usando la suscripción y el filtro de orden existentes.
- [x] En `SensorMonitoreo.razor`, ajustar el intervalo periódico a 20 segundos y conservar el arranque, la cancelación y el envío por SignalR.
- [x] Actualizar el README general en coherencia con `Diagrama_WebAssembly.png`. Explicar cómo `InversorSimulado` genera potencia y estado en WebAssembly, la consulta cada 20 segundos, el envío por SignalR, la asociación al panel y la actualización de la vista tras guardar.
- [x] Ejecutar los controles de salida al cerrar Presentation.

## Controles de salida por capa

Desde la raíz, ejecutar los tres comandos en cada cierre de capa antes de continuar.

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Registrar los resultados y distinguir advertencias preexistentes de fallos nuevos. Ante un fallo de ArchUnitNET, corregir el código y volver a ejecutar los controles con las pruebas estructurales existentes.

## Aceptación funcional

- [ ] Abrir un nodo con varios paneles. Confirmar que cada fila muestra su lectura persistida más reciente, que 0 W se muestra como una medición y que un panel sin lecturas muestra «Sin mediciones».
- [x] Verificar que, si dos mediciones del mismo panel comparten fecha UTC, el detalle del nodo y la consulta inicial del monitoreo seleccionan la misma lectura según el desempate por ID.
- [ ] Abrir el monitoreo individual y comprobar la lectura inicial, el texto en W, el estado y la fecha. Comprobar el caso sin lectura y una lectura superior a la potencia nominal.
- [ ] Comprobar en una sesión activa el intervalo periódico de 20 segundos y que una lectura nueva actualiza el medidor después de persistirse.
- [ ] Verificar el acceso al monitoreo con los roles Administrador y Operador y comprobar que una sesión sin rol autorizado no pueda acceder.

Usar un destino aislado identificado para las pruebas que escriban datos. Si se necesitan pruebas funcionales auxiliares, ubicarlas en `arnes/prompting/tests/mod005/`. Guardar evidencias depuradas en `artifacts/`.
