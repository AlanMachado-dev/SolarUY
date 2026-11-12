# Especificaciones — req011

## Domain

- El estado persistido de `Panel` gobierna las transiciones Operativo ↔ Desactivado. Las órdenes repetidas son idempotentes. Los estados persistidos EnFalla y EnMantenimiento quedan fuera de estas acciones.
- Una medición de un panel desactivado debe indicar Desactivado y 0 W. Durante la operación normal, las lecturas pueden informar fallas o mantenimiento.

## Application

- `ControlPanelService` autoriza el cambio para Administrador y publica la lectura confirmada a través del canal de monitoreo existente.
- `LecturaPanelDto` incluye el estado de control separado de la telemetría. El sensor obtiene el estado de control persistido a través del hub.

## Infrastructure

- Reutilizar `Paneles.Estado` y `Medicion`. Guardar la transición y una medición de 0 W en la misma transacción. El registro a 0 W evita imputar energía durante la pausa.
- Serializar los cambios y la validación de lecturas del mismo panel mediante el bloqueo de su fila MySQL. La fecha de cada cambio se calcula bajo el bloqueo y se ordena después de las mediciones ya guardadas para ese panel.

## Presentation

- Mostrar el botón correspondiente y el resultado de la orden en el monitoreo individual. Habilitar las acciones solo para Administrador y deshabilitar el botón durante el envío.
- En cada ciclo, el sensor consulta el estado de control y lo aplica al inversor simulado antes de tomar y enviar la lectura. Las sesiones vuelven a consultar el estado persistido después de reconectar.
