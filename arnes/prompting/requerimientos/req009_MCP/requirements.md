# req009 — Requerimientos: Consultas en lenguaje natural mediante MCP

> Identificador: **req009 (Consultas en lenguaje natural mediante MCP)**. La documentación está en requerimientos/req009_MCP y las herramientas en tests/req009. Ver [estructura actual](../../README.md).

## Descripción General

Como usuario autorizado de SolarUY, quiero consultar información del sistema mediante lenguaje natural desde un chat integrado en la aplicación.

El asistente utilizará Gemini como modelo en la nube y accederá a las consultas autorizadas de SolarUY mediante Model Context Protocol (MCP).

Debe permitir preguntas generales, como “¿cómo está funcionando todo hoy?”, y consultas específicas que requieran combinar información de parques, nodos, paneles y mediciones.

## Alcance

- Incorporar un chat flotante en la interfaz de SolarUY.

- Utilizar Gemini 3.5 Flash-Lite (`gemini-3.5-flash-lite`) mediante el nivel gratuito de su API. Cambio autorizado por el usuario el 2026-11-05 después de observar indisponibilidad HTTP 503 de Gemini 3.8 Flash; conservar las evidencias anteriores y comprobar el nuevo modelo con la cuenta real.

- Exponer mediante MCP capacidades de búsqueda, detalle, estado actual, historial y resúmenes de mediciones.

- Realizar la demostración y aceptación desde el chat de SolarUY.

- Entender por “estado del sistema” el funcionamiento de los parques, nodos y paneles, según los datos disponibles.

La consulta del estado técnico de servidores, balanceador y base de datos queda fuera de esta entrega.

## Criterios de Aceptación

1. Acceso y permisos. El chat estará disponible para usuarios autenticados con rol Administrador u Operador. Cada consulta deberá respetar los permisos del usuario. La autorización se comprobará en el servidor, incluyendo las consultas ejecutadas mediante MCP.

2. Chat flotante. El chat estará disponible en todas las páginas de la aplicación después de iniciar sesión, ubicado en la esquina inferior derecha. Permitirá desplegarse hacia arriba y minimizarse mediante una flecha en su cabecera. Incluirá historial de mensajes, campo de escritura, botón de envío e indicador de respuesta en curso.

3. Continuidad de la conversación. Minimizar el chat o navegar entre páginas conservará la conversación durante la sesión. El usuario podrá eliminar su contexto conversacional mediante un botón “Nueva conversación”. Al cerrar o finalizar la sesión, se descartará la conversación. No se guardará permanentemente en la base de datos.

4. Contexto de la página. Al enviar una pregunta, el asistente recibirá el contexto del parque, nodo o panel mostrado en la página, cuando corresponda. Esto permitirá interpretar preguntas como “¿cómo estuvo funcionando hoy?”. La respuesta identificará el elemento consultado. Si el contexto o la conversación permiten varias interpretaciones, el asistente solicitará una aclaración.

5. Consulta mediante MCP. El asistente obtendrá la información operativa de SolarUY utilizando herramientas expuestas mediante MCP. Deberá poder combinar varias consultas para resolver una pregunta y utilizar referencias de mensajes anteriores, como “de esos nodos, ¿cuáles tienen paneles en falla?”.

6. Capacidades de consulta. El conjunto de herramientas deberá permitir:

    - Buscar parques, nodos y paneles por nombre y pertenencia dentro de la estructura.

    - Consultar sus características y relaciones.

    - Consultar estados y últimas mediciones, incluyendo la fecha de cada lectura.

    - Consultar mediciones históricas por elemento y período.

    - Obtener resúmenes y comparaciones por período, panel, nodo o parque.

    Las consultas deberán admitir filtros y límites de resultados adecuados. El acceso al historial y los resúmenes necesarios deberá incorporarse si todavía no está disponible.

7. Interpretación temporal. Las expresiones “hoy”, “ayer” y los períodos sin zona horaria explícita se interpretarán utilizando America/Montevideo. Las respuestas indicarán el período analizado o la fecha y hora de las mediciones utilizadas, según corresponda.

8. Precisión de las respuestas. Las respuestas se basarán en los resultados obtenidos de SolarUY e identificarán las unidades de las magnitudes informadas. Los cálculos, agrupaciones y estadísticas se realizarán en SolarUY. El asistente distinguirá los datos observados de sus interpretaciones y no presentará como comprobada una causa de falla que los datos no permitan determinar.

9. Información incompleta. El asistente indicará cuando no existan datos, las lecturas sean antiguas o el período tenga cobertura incompleta. No interpretará la ausencia de mediciones como generación cero ni como funcionamiento normal. Conocerá el carácter simulado de las mediciones actuales.

10. Actualización bajo consulta. Cada nueva pregunta que requiera información actual obtendrá datos actualizados mediante MCP. Las respuestas anteriores conservarán su contenido y sus referencias temporales, sin actualizarse automáticamente.

11. Solo lectura. Las capacidades expuestas al asistente permitirán exclusivamente consultar información. Ante solicitudes de creación, modificación o eliminación, el asistente explicará esa limitación. Esta restricción deberá estar garantizada por las operaciones disponibles en el servidor.

12. Errores y cuota gratuita. Si Gemini o MCP no están disponibles, o se alcanza la cuota gratuita, el chat mostrará un mensaje comprensible y permitirá reintentar. No presentará el error como ausencia de datos. Las demás funcionalidades de SolarUY deberán continuar funcionando.

## Escenarios mínimos de aceptación

Verificar con datos conocidos:

- “¿Cómo está funcionando todo hoy?”

- “¿Dónde está el panel llamado Panel 3?”, incluyendo nombres repetidos.

- “¿Qué paneles están en falla en el parque Norte?”

- “¿Cómo estuvo generando este panel durante la mañana?”, utilizando el contexto de la página.

- “¿Qué nodos tuvieron menor potencia promedio hoy que ayer?”

- “De esos nodos, ¿cuáles tienen paneles en falla ahora?”

- Una consulta sobre un período sin mediciones.

- Una pregunta sobre una causa de falla que no pueda determinarse.

- Una solicitud de modificación, que deberá rechazarse.

- Una interrupción del servicio de IA o agotamiento de cuota.

## Restricciones

- Respetar los diagramas, las reglas de arquitectura y los permisos existentes.

- Mantener el funcionamiento con el balanceador y las múltiples instancias de SolarUY.

- Mantener las credenciales de Gemini exclusivamente en el servidor y en configuración privada.

- Enviar al modelo únicamente el contexto y los datos autorizados necesarios para responder.

- Conservar appsettings.Local.json y la base habitual del usuario.

- Utilizar datos simulados y un destino aislado para las pruebas que requieran preparar información.

- No incluir secretos, tokens ni valores de cookies en documentación, mensajes o evidencias.

- Documentar brevemente la configuración, el uso y las limitaciones del asistente.
