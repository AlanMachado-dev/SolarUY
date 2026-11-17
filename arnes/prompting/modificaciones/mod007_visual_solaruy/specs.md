# mod007 · Especificaciones

## Domain

Las entidades, la precisión de las magnitudes y los instantes almacenados siguen utilizando sus representaciones actuales.

## Application

Los contratos y servicios existentes suministran potencia, estado y fecha de la lectura. El indicador diario utiliza el corte local entregado por el servicio de energía. El cálculo de energía y la aceptación de lecturas siguen su flujo actual.

## Infrastructure

La modificación se entrega sin migraciones ni cambios de configuración. La verificación funcional usa datos aislados.

## Presentation

- Recuperar la barra lateral, los iconos y los estilos base de la plantilla de Blazor. Usar los componentes Bootstrap para botones, tablas, formularios y tarjetas con ajustes de tamaño y espaciado. Adaptar el menú a pantallas estrechas y reemplazar el contenido de ejemplo en el inicio por los accesos de SolarUY.
- Centralizar el formato de salida con cultura es-UY. Usar hasta dos decimales en W, tres en kWh y seis en MW y grados. Aplicar el indicador «<» a las magnitudes energéticas y eléctricas positivas que quedan debajo de la resolución correspondiente. El formato de entrada de los formularios conserva sus reglas actuales.
- Convertir los instantes UTC a America/Montevideo al mostrarlos. Formatear directamente el corte de energía que ya llega en hora local.
- Colocar primero la lectura en vivo. Mostrar en la misma tarjeta el valor actual y el medidor Radzen. Presentar estados y fecha de medición junto con las acciones permitidas. Situar la energía estimada y el estado del sensor simulado en secciones separadas.
- Mantener los modos interactivos de servidor y WebAssembly, las suscripciones existentes y el intervalo de simulación.
- Usar un diálogo HTML cerrado en el marcado inicial. El evento de reconexión de Blazor abre o cierra el diálogo y selecciona el mensaje visible, aun cuando falten los estilos.
