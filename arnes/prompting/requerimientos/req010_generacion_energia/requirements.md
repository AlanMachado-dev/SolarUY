# req010 - Mostrar la energía generada por parque, nodo y panel

## Descripción general

Como Administrador u Operador, quiero consultar la energía estimada de hoy en el detalle de un parque solar, de un nodo y en el monitoreo de un panel, para conocer cuánto generó cada componente a partir de las mediciones guardadas.

## Criterios de aceptación

1. Al abrir o recargar cualquiera de los tres detalles, el sistema muestra «Energía estimada de hoy» en kWh, para el día calendario de Uruguay y con la hora de consulta. Las mediciones de origen se registran en W y son simuladas en el sistema actual.
2. El valor del panel corresponde a sus propias mediciones. Para el mismo corte de consulta, el nodo suma las estimaciones de sus paneles y el parque suma las de todos los paneles de sus nodos.
3. Cuando hay tramos medidos que permiten estimar 0 kWh, se muestra cero. Si no hay ningún tramo válido, se muestra «Sin datos suficientes». Si solo se pudo estimar parte de la generación, se muestra el subtotal con el aviso «Cobertura parcial».
4. Un parque o nodo sin paneles muestra «Sin paneles». Si un panel carece de datos suficientes pero otros sí aportan al agregado, el resultado se indica como parcial.
5. El indicador está disponible para Administrador y Operador. El detalle general del parque conserva su acceso actual para los demás usuarios autenticados y omite el indicador para esos usuarios.

## Alcance

El indicador usa las mediciones persistidas. Se consulta al abrir o recargar el detalle. El monitoreo individual sigue mostrando la potencia actual en W en su medidor en vivo.
