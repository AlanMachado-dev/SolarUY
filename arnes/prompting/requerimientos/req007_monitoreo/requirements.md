# req007 — Requerimientos: Monitoreo en tiempo real de paneles

## Descripción General

Como Administrador del sistema SolarUY, quiero poder monitorear en tiempo real la generación de corriente y el estado de los paneles.

## Criterios de Aceptación

1. El sistema debe permitir el monitoreo de los siguientes datos de los paneles de un nodo:
   - Estado
   - GeneracionActualW

   Estos datos se guardarán en una tabla Medición en la base de datos junto a la fecha y hora de la medición (como se indica en el Modelo de Dominio).

2. El proyecto se estructurará de la siguiente manera:
   - En la parte de servidor de SolarUY se tendrá la pantalla de monitoreo en tiempo real, donde se accederá con un botón de "Monitoreo" en cada panel dentro de la lista de paneles de cada nodo. En esta pantalla al entrar se mostrará una medición de la ultima medición disponible de ese panel el cuál irá cambiando en tiempo real mientras vaya recibiendo los datos del sensor. También se mostrará el estado actual del panel para monitorear su salud en tiempo real.
   - En la parte de WebAssembly de SolarUY (SolarUY.Client) se tendrá el sensor, el cuál este cada 30 segundos llamará al inversor para pedirle los datos del panel. Cuando reciba estos datos se los enviará al servidor para que los guarde en la tabla Medición asociado a ese panel, así queda asociado la medición con su panel. Cuando termine de guardarse los cambios se renderizarán las nuevas mediciones. Este proceso se hará usando la tecnología de SignalR.
   - Al ser un proyecto pequeño el inversor será una simulación de Inversor usando una clase dentro del WebAssembly el cuál contendrá funciones que devuelvan mediciones coherentes. Esto será lo que devuelva el sensor al servidor.
3. Basarse en Diagrama_WebAssembly.png de .agents/Images.
4. Seguridad: Los usuarios autenticados con el rol de "Administrador" o de "Operador" están autorizados a examinar las mediciones y estado de salud de los paneles.

## Reglas de Negocio

- Dejar documentación consistente si es necesario.
