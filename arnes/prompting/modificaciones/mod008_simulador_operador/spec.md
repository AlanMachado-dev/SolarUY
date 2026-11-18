# mod008 · Especificación técnica

El cambio de [requerimientos](requirements.md) se concentra en la autorización del sensor web. Se conserva el flujo WebAssembly → SignalR → servidor del [diagrama del proyecto](../../../../.agents/Images/Diagrama_WebAssembly.png).

## Domain

Las reglas existentes del panel determinan cuándo admite telemetría. Se reutilizan sus estados y las reglas de creación de mediciones.

## Application

`MonitoreoService` sigue consultando el permiso de transmisión, delegando el registro de la muestra y publicando la lectura confirmada. `ControlPanelService` mantiene la autorización actual al presentar y ejecutar acciones. Falla, arreglo, activación y desactivación son exclusivas del Administrador. El mantenimiento conserva sus permisos actuales.

## Infrastructure

`MedicionRepository` sigue validando la existencia del panel, el estado de transmisión, la revisión de control y el valor de potencia antes de guardar la lectura. Se usa el esquema existente de la base de datos.

## Presentation

- Registrar una política de acceso al sensor simulado en `Program.cs` para usuarios autenticados con rol Administrador u Operador. Aplicar la misma política en la página, la API y el hub.
- En `MonitoreoPanel.razor`, usar la política para cargar los datos del panel y mostrar el sensor. Mantener la comprobación de que el panel consultado sigue siendo el de la ruta antes de montar `SensorMonitoreo`.
- En `MonitoreoEndpoints.cs`, aplicar la política a `GET /api/monitoreo/paneles/{panelId}/transmision`. Mantener la respuesta con el permiso y la revisión de control, y el 404 para paneles inexistentes.
- En `MonitoreoHub.cs`, aplicar la política a `/hubs/monitoreo`. Mantener la comprobación de que cada muestra corresponde al panel asociado a la conexión.
- Reutilizar `SensorMonitoreo.razor` y el inversor de `SolarUY.Client`. Cada pantalla abierta inicia su propia instancia con el ciclo actual de envío, pausa, reanudación y cancelación. La coordinación de instancias entre pantallas queda fuera del alcance.
