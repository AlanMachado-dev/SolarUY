# mod009 — Modificaciones: Claridad y presentación de las respuestas del asistente

> Identificador vigente: **mod009 (Claridad y presentación de las respuestas del asistente)**.

## Descripción General

Como usuario de SolarUY, quiero recibir respuestas claras y orientadas a mi pregunta, para comprender la información consultada sin interpretar términos técnicos, fechas extensas ni detalles innecesarios.

## Criterios de Aceptación

1. Respuesta principal al comienzo. La primera oración debe responder directamente la pregunta cuando existan datos suficientes. Si se solicita un promedio, comenzar con el promedio; si se pregunta qué paneles están en falla, comenzar identificándolos.
2. Lenguaje natural. Utilizar español claro y consistente con SolarUY. Evitar expresiones como “promedio de las muestras individuales observadas en el tramo” cuando pueda decirse “promedio de las mediciones disponibles”. Explicar los términos técnicos únicamente cuando sean necesarios para comprender el resultado.
3. Detalle proporcional a la consulta. No enumerar mediciones individuales al responder una pregunta sobre un promedio. Mostrar esas mediciones cuando el usuario las solicite o sean necesarias para explicar el resultado. Las consultas complejas podrán incluir listas y explicaciones más extensas.
4. Valores fáciles de leer. Mostrar las potencias con hasta dos decimales y su unidad, por ejemplo, “168,37 W”. El redondeo será únicamente de presentación: los cálculos conservarán su precisión y seguirán realizándose en SolarUY.
5. Fechas y períodos comprensibles. Mostrar las fechas y horas en horario de Uruguay, evitando formatos ISO, fracciones de segundo y offsets repetidos. Para consultas recientes, utilizar expresiones como “los últimos dos minutos”, acompañadas del intervalo horario cuando ayude a identificar el período.
6. Coherencia temporal. Las expresiones relativas, como “los últimos dos minutos”, deben resolverse a partir de una única hora de referencia del servidor. El período consultado y el comunicado deben coincidir, con inicio anterior al fin y duración correspondiente a lo solicitado.
7. Limitaciones breves y relevantes. Conservar las aclaraciones sobre datos simulados, cobertura incompleta o lecturas antiguas, expresadas de manera comprensible. Por ejemplo: “El cálculo usa 3 mediciones simuladas; no hay registros de todo el período”. No convertir cada respuesta en una ficha técnica.
8. Información confiable. Diferenciar ausencia de datos, mediciones de cero y errores de consulta. No inventar resultados, causas de falla ni conclusiones que la cobertura disponible no permita sostener.
9. Presentación compatible con el chat. Las respuestas deben mostrarse sin barras de escape ni marcas de formato visibles. La información debe poder leerse cómodamente mediante párrafos breves y listas cuando correspondan.


## Reglas de Negocio

- No modificar nada en la carpeta de arnes/prompting/requerimientos ni carpetas dentro de la misma.
