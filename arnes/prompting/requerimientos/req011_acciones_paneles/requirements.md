# req011 — Activar y desactivar paneles

Como Administrador, quiero activar o desactivar un panel desde su monitoreo y ver el resultado confirmado.

## Criterios de aceptación

1. El monitoreo distingue el estado de control del último estado informado por el sensor. Administrador puede activar un panel desactivado o desactivar uno activo. Operador conserva el acceso de lectura.
2. Una acción confirmada actualiza el estado del panel en MySQL y el monitoreo en vivo. Recargar o abrir otra sesión conserva el estado. Repetir la misma orden deja el estado intacto.
3. El inversor simulado devuelve estado Desactivado y 0 W mientras el panel está desactivado. Al activarlo vuelve a simular las lecturas normales. El simulador deja de generar desactivaciones aleatorias.
4. Los cambios de control quedan reflejados en el historial de mediciones. Una lectura contradictoria recibida durante un cambio de estado se rechaza.
5. Si falla el guardado, el panel de control muestra el error y conserva el último estado confirmado.
