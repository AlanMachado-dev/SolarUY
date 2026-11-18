# mod008 · Sensor simulado para el Operador

## Objetivo

Como Operador, quiero que el sensor simulado se inicie al abrir el monitoreo de un panel para generar y ver mediciones de potencia en vivo durante mi sesión.

## Criterios de aceptación

1. Al abrir el monitoreo de un panel existente, un Operador autenticado ve la sección **Sensor simulado**. El sensor se inicia sin intervención manual.
2. Mientras la pantalla está abierta y la telemetría está habilitada, el sensor envía mediciones simuladas de potencia en W con el intervalo actual de 20 segundos entre envíos confirmados. Las lecturas admitidas se guardan y actualizan el monitoreo del panel.
3. El sensor pausa los envíos cuando el panel deja de admitir telemetría, los reanuda cuando vuelve a admitirla y termina su ciclo al salir de la pantalla.
4. El sensor está disponible para Administrador y Operador. **Simular falla**, **Simular arreglo**, **Activar** y **Desactivar** son acciones exclusivas del Administrador. El Operador conserva sus permisos actuales de consulta y mantenimiento.

Esta modificación amplía el acceso al sensor definido en [req007](../../requerimientos/req007_monitoreo/requirements.md) y conserva la frecuencia definida en [mod005](../../requerimientos/req007_monitoreo/mod005_cambios_monitoreo/requirements.md).
