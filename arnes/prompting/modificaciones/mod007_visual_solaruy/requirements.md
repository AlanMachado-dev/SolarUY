# mod007 · Actualización visual de SolarUY

## Objetivo

Dar a las pantallas operativas una identidad visual común, con la navegación, los formularios y los componentes de Blazor que ya usa SolarUY.

## Criterios de aceptación

1. El inicio, la navegación, el acceso, el listado, las altas, los detalles y el monitoreo adoptan el aspecto de la plantilla de Blazor con su barra lateral azul y violeta, iconos de navegación y controles Bootstrap. La navegación y los controles siguen disponibles en escritorio y en pantallas estrechas.
2. Las cifras mostradas usan coma decimal y omiten ceros finales. La potencia en W muestra hasta dos decimales, la energía en kWh hasta tres, y la capacidad en MW y las coordenadas hasta seis. Una magnitud energética positiva inferior al mínimo visible muestra el mínimo visible precedido por «<».
3. Las fechas de instalación, última medición, mantenimiento disponible y corte de energía se muestran como día/mes/año y hora de Uruguay, sin el texto «UTC». El cambio de fecha al cruzar la medianoche se refleja en pantalla.
4. El monitoreo coloca la generación actual en W como dato principal junto al medidor radial y distingue una lectura de 0 W de la ausencia de una medición. La sección de energía estimada diaria en kWh se presenta por separado.
5. El rediseño conserva las rutas, las acciones y sus permisos, así como el flujo de mediciones en tiempo real.
6. El aviso de reconexión permanece oculto al cargar una página y muestra solo el mensaje que corresponde al estado de la conexión.
