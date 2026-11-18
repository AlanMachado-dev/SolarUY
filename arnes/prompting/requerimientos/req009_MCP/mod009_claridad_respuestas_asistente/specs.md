# mod009 - Especificaciones: Claridad y presentación de las respuestas del asistente

## Alcance y decisiones

Fuente funcional: [requirements.md](requirements.md), que contiene los nueve criterios de aceptación de esta modificación. Este diseño amplía el asistente de [req009](../specs.md) sobre la implementación actual.

Mejorar el orden, lenguaje, precisión visual y coherencia temporal de las respuestas. Mantener Gemini, MCP de solo lectura, permisos, contexto de página, conversación por sesión y funcionamiento con varias instancias. Conservar los diagramas de `.agents/Images`, el sensor y su cadencia, las fórmulas de potencia y los umbrales de cobertura y vigencia.

Durante la implementación se respetará la restricción del requerimiento de no modificar `arnes/prompting/requerimientos/` ni sus subcarpetas. La edición documental solicitada se limita a este `specs.md`; no modifica `requirements.md` ni documentos vecinos.

## 1. Domain

Reutilizar `ResumenMediciones`, `PotenciaObservada`, `ComparacionPotencia` y `EstadoActualObservado`. No se requieren entidades nuevas ni cambios de comportamiento en Domain.

- Conservar los cálculos decimales existentes, incluidos los agregados antes de paginar. El promedio de un panel y la suma de promedios por panel de un nodo o parque siguen siendo magnitudes distintas.
- El formato con hasta dos decimales no interviene en sumas, promedios, diferencias, comparaciones ni clasificación de resultados.
- Conservar la distinción entre ausencia de mediciones, cero medido, lectura antigua y cobertura parcial. Simplificar la explicación no convierte una comparación parcial en concluyente ni permite inferir causas de falla.

## 2. Application

### Política de respuesta

Actualizar las instrucciones de `AsistenteService` sin eliminar las reglas de seguridad, consulta actualizada por MCP y desambiguación existentes:

1. Comenzar con el resultado solicitado, identificando el elemento por nombre y la pertenencia necesaria para distinguirlo. Usar los IDs internamente; no convertirlos en encabezados rutinarios para el usuario.
2. Ante datos parciales, calificar el resultado desde la primera oración: «Con las mediciones disponibles, el promedio fue…». Si falta información para responder, comenzar por la aclaración o limitación correspondiente.
3. Continuar con el período y una aclaración breve sobre cantidad de mediciones, simulación y limitaciones relevantes. Una pregunta simple normalmente se resuelve en uno o dos párrafos, sin imponer un máximo que oculte información necesaria.
4. Para promedios utilizar `ResumirMediciones`; no solicitar ni enumerar el historial salvo que se pida el detalle o resulte necesario para explicar la consulta. Para estados y comparaciones conservar las herramientas específicas.
5. Expresar los conceptos con lenguaje cotidiano. Para un nodo o parque explicar, cuando corresponda, que el valor suma los promedios de sus paneles; no confundirlo con el promedio de todas las lecturas ni con energía.
6. Comunicar datos faltantes, antigüedad, resultados pendientes y límites mediante frases comprensibles. Traducir «hay más páginas» a una aclaración breve, sin mostrar cursores ni nombres de campos internos. No afirmar que el listado está completo si falta consultar resultados.
7. Evitar encabezados fijos como «Elemento / Origen / Cobertura», explicaciones repetidas de la cadencia del sensor y avisos de solo lectura en preguntas que no solicitan cambios. Conservar una indicación breve de simulación al informar mediciones y señalar siempre las limitaciones que afecten la conclusión.
8. Producir texto plano con párrafos y viñetas simples, sin sintaxis Markdown, HTML, barras de escape ni bloques de código. Conservar avisos de contexto recortado, ubicándolos después de la respuesta cuando no impidan interpretarla; si la referencia es ambigua, pedir aclaración primero.

### Formato de los datos

Incorporar un `FormatoRespuestaAsistenteService` pequeño en `Application/Asistente`, inyectado por constructor. Su responsabilidad es preparar textos de presentación a partir de los resultados ya calculados; no consultar persistencia, calcular estadísticas ni decidir cobertura.

Completar los resultados de las herramientas con un DTO opcional de presentación que contenga períodos y valores legibles, vinculados al elemento y al campo de origen. Mantener los campos numéricos, fechas UTC, referencias e indicadores originales para las operaciones y llamadas sucesivas. El modelo debe utilizar los textos preparados por SolarUY para comunicar cifras y límites temporales, sin recalcularlos ni convertir zonas por su cuenta.

| Dato | Representación visible |
| --- | --- |
| Potencia | Cultura `es-UY`, coma decimal y hasta dos decimales, sin ceros finales innecesarios; por ejemplo, `168,37 W`. Redondeo de presentación consistente, con punto medio alejándose de cero. |
| Potencia ausente | «No hay mediciones disponibles» o «No hay mediciones recientes», según el resultado; nunca sustituir `null` por `0 W`. |
| Potencia positiva muy pequeña | Si redondearía a cero, informar «menos de 0,01 W», para distinguirla de un cero medido. |
| Fecha y hora | Conversión mediante `America/Montevideo`; fecha `dd/MM/yyyy` y hora de 24 horas. Usar segundos en intervalos cortos y lecturas individuales; omitir fracciones de segundo. |
| Intervalo reciente | Etiqueta relativa y límites locales derivados del período resuelto, por ejemplo, «últimos 2 minutos, de 16:01:36 a 16:03:36 (hora de Uruguay)». Incluir la fecha para identificar la consulta; si cruza medianoche, mostrar ambas fechas. |
| Período largo o comparación | Fechas y horas de ambos extremos y de ambos períodos cuando corresponda; una sola indicación de horario de Uruguay, sin repetir offsets. |
| Estados y cobertura | Etiquetas comprensibles, como «en falla», «sin mediciones recientes» y «faltan registros de parte del período», a partir de los indicadores existentes. |

El formato no modifica los límites exactos del intervalo ni la precisión guardada: las fracciones de segundo permanecen en los datos de consulta aunque no se muestren. El modelo no debe deducir cantidades, cobertura o cero medido a partir de los textos redondeados.

### Resolución temporal en el servidor

Reutilizar el instante único capturado por `AsistenteService.PreguntarAsync` y transmitido a la conexión MCP. No tomar otro «ahora» al ejecutar cada herramienta ni utilizar el reloj del navegador para resolver la pregunta.

- Ampliar `ConsultaOperativaDto` con `Periodo = "Ultimos"`, `CantidadPeriodo` y `UnidadPeriodo`. Admitir cantidades positivas y finitas con unidad cerrada `Segundos`, `Minutos`, `Horas` o `Dias`; «últimos N días» representa una duración móvil de N × 24 horas. Mantener las modalidades actuales `Hoy`, `Ayer`, `Manana` y `Explicito`.
- El modelo interpreta la cantidad y la unidad expresadas por el usuario; `PeriodoConsultaService` realiza la resta temporal. Para dos minutos y referencia T, resolver exactamente `[T - 2 minutos, T)`, en UTC, y convertir después sus extremos a Uruguay.
- Rechazar cantidades ausentes o inválidas, unidades desconocidas, desbordamientos y combinaciones contradictorias de período relativo con `Desde`/`Hasta`. Respetar el límite por invocación y la composición de tramos existente. No truncar silenciosamente una duración que exceda el límite.
- Las consultas históricas deben tener inicio anterior al fin. Tratar explícitamente las ventanas calendario todavía vacías, como «esta mañana» antes de las 06:00: comunicar que el tramo no comenzó, sin ejecutar un intervalo invertido ni presentarlo como cero medido.
- Reutilizar el mismo `PeriodoConsultaDto` para leer datos, evaluar cobertura y construir su descripción visible. Un intervalo inválido se devuelve como tal y no puede utilizarse para redactar un resultado exitoso.
- En consultas relativas, las instrucciones deben exigir la modalidad `Ultimos`, evitando que el modelo fabrique timestamps explícitos. Los límites de la respuesta se copian del resultado de la herramienta. Mantener la resolución actual de los períodos de referencia de las comparaciones.
- Pedir aclaración cuando la expresión no permita determinar duración o elemento. No corregir automáticamente un intervalo intercambiando sus extremos ni reutilizar resultados de otro turno.

Para verificar el criterio 6 de `requirements.md`, comprobar primero que el intervalo resuelto por SolarUY y utilizado para consultar los datos tenga el orden y la duración solicitados. Después, contrastarlo con el período comunicado en la respuesta. Son dos comprobaciones diferentes: una consulta puede usar límites correctos y la respuesta describirlos incorrectamente. Si se detecta una discrepancia, identificar en cuál de esas etapas se produce antes de atribuir su causa.

### Salida y fallos

Conservar `RespuestaAsistenteDto`, sus códigos y referencias. Normalizar únicamente marcas de presentación reconocidas en el texto del asistente, como viñetas escapadas y delimitadores de énfasis; preservar nombres, números, signos y contenido literal. No aplicar reemplazos globales a fechas o cifras ni convertir el texto en HTML.

La política de redacción y los textos preparados ayudan a orientar al modelo; no sustituyen la validación de argumentos ni demuestran por sí solos que todas sus respuestas sean correctas. Los errores de proveedor, MCP y persistencia conservarán su clasificación y una explicación breve, sin confundirse con ausencia de mediciones. No agregar otra llamada de IA para reformular cada respuesta ni reintentos automáticos.

## 3. Infrastructure

- Mantener `GeminiConversacional` como adaptador de `IModeloConversacional`: enviar las instrucciones y esquemas actualizados, transportar los resultados y recuperar el texto. El formato y la aritmética temporal no se implementan en el adaptador del proveedor.
- Mantener `ClienteHerramientasMcp` y la conexión por turno, preservando el instante de referencia y la correlación del protocolo. Transportar los campos nuevos sin omitir referencias ni alterar las firmas del proveedor.
- Reutilizar `ConsultaOperativaRepository`: recibe el intervalo ya resuelto y conserva filtros UTC `[desde, hasta)`, agregados y paginación. No se requieren cambios de esquema, migraciones, tablas de conversación ni proveedores nuevos.
- Registrar el servicio de formato en la raíz de composición. Usar cultura y zona explícitas para que el resultado sea igual en Windows, Linux y ambas instancias.
- Conservar modelo, credenciales privadas, configuración local y base habitual. No registrar conversaciones ni respuestas completas como evidencia de funcionamiento.

## 4. Presentation

- Actualizar las descripciones y el esquema de `ServidorAsistenteMcp` para documentar la modalidad relativa. El servidor MCP delega validación, fechas y consultas en Application; no accede a repositorios concretos ni calcula duraciones.
- Mantener `ChatAsistente.razor` mostrando texto escapado. Conservar saltos de línea y separación legible entre párrafos y viñetas mediante el estilo existente; ajustar espaciado únicamente si resulta necesario. No incorporar un motor Markdown ni utilizar HTML generado por el modelo.
- El componente y `chat-asistente.js` siguen capturando preguntas, invocando el endpoint y mostrando resultados. No redondean cifras ni reinterpretan fechas o cobertura.
- Mantener historial, minimizado, navegación, nueva conversación, accesibilidad, reintento manual y descarte al vencer la sesión. Los mensajes nuevos usan la nueva presentación; los anteriores conservan su texto y referencia temporal al navegar o restaurar la pestaña.

Ejemplo orientativo para un resultado validado de tres mediciones y cobertura parcial; no es una plantilla literal ni evidencia de aceptación:

> Con las mediciones disponibles, el promedio de potencia de Panel principal fue de aproximadamente 168,37 W durante los últimos 2 minutos.
>
> Se consultó el 18/11/2026, de 16:01:36 a 16:03:36, en horario de Uruguay. El cálculo usa 3 mediciones simuladas; faltan registros de parte del período.

## Verificación prevista

| Criterios de requirements.md | Comprobación |
| --- | --- |
| 1, 2, 3 | Promedio, paneles en falla y comparación: resultado al comienzo, vocabulario claro y detalle proporcional. Pedir después las lecturas y comprobar que se muestran únicamente al solicitarlas o justificarse. |
| 4, 8 | Contrastar el promedio con datos conocidos y precisión original; comprobar coma decimal, redondeo y unidad. Incluir cero real, valor positivo que redondearía a cero y ausencia de datos. |
| 5, 6 | Con reloj controlado, resolver «últimos dos minutos» y contrastar duración exacta, límites de consulta y respuesta. Repetir atravesando medianoche y con una zona de host diferente. Incluir intervalo invertido, duración inválida y mañana todavía no iniciada. |
| 7, 8 | Cobertura completa y parcial, lecturas antiguas, homónimos y comparación no concluyente: conservar advertencias relevantes y pedir aclaración sin inventar conclusiones. |
| 8 | Provocar errores únicamente en un destino aislado: distinguirlos de «sin mediciones» y conservar el reintento manual y los permisos de Administrador y Operador. |
| 9 | Revisar el chat con párrafos, viñetas, nombres que contengan signos y contenido con apariencia de HTML/Markdown: sin marcas accidentales, pérdida de información ni ejecución de HTML. |
| Regresiones de req009 | Navegar, minimizar y restaurar; conservar mensajes anteriores y contexto. Comprobar respuesta por ambas instancias sin diferencias de cultura o zona horaria. |

Durante la implementación, ejecutar por capa `dotnet build SolarUY.slnx`, `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`. No modificar tests estructurales. Este diseño no introduce comportamiento de Domain ni prevé pruebas unitarias en otras capas.

Ubicar las comprobaciones funcionales auxiliares en `arnes/prompting/tests/mod009/` y las evidencias depuradas en `artifacts/mod009/`, excluido de Git. Las preparaciones de datos o fallos utilizarán un destino aislado identificado. Verificar finalmente desde el chat real con Gemini y MCP; las inspecciones o respuestas simuladas no acreditan esa aceptación. Esta entrega es únicamente de diseño y deja esas verificaciones para la implementación.
