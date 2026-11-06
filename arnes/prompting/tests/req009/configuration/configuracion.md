# req009 — Configuración y uso

El asistente se habilita explícitamente en el servidor. Sin configuración suficiente, SolarUY arranca normalmente y el chat informa que está deshabilitado. No se agregan tablas ni migraciones y no se modifica `SolarUY/appsettings.Local.json`.

## Fuentes privadas

Usar variables de entorno `Asistente__Habilitado=true`, `Asistente__Modelo=gemini-3.5-flash-lite` y `Asistente__ClaveApi`, asignando la clave exclusivamente desde una fuente privada. No escribir su valor en comandos guardados, documentación, capturas o archivos públicos.

También se admite un archivo JSON adicional indicado por `AsistenteArchivoPrivado`. Solo se consume su sección `Asistente`; sus propiedades corresponden a las opciones de la tabla siguiente. Las variables de entorno prevalecen. Los archivos `*.private.json` se excluyen de la publicación del servidor y los nombres privados documentados se excluyen de Git y Docker. `appsettings.Local.json` también se excluye de la publicación.

En el despliegue req008, ambas instancias leen `/run/private/asistente.private.json`, montado desde el directorio privado existente. Crear ese archivo fuera de Git con la sección `Asistente` y reiniciar ambas instancias después de configurarlo. No copiar la clave al Dockerfile, a imágenes, a `SolarUY.Client` ni al navegador. Conservar el origen HTTPS, las cookies y las claves compartidas existentes de Data Protection.

| Opción en Asistente | Valor inicial |
| --- | --- |
| Habilitado | false |
| Modelo | gemini-3.5-flash-lite |
| ClaveApi | Ausente; obligatoria para habilitar |
| ResultadosPagina / MaximoPagina | 50 / 200 |
| MaximoIds / MaximoDias | 100 / 31 por invocación |
| VigenciaSegundos / HuecoSegundos | 60 / 60 |
| MaximoPregunta / MaximoMensajes / MaximoContexto | 2000 / 30 / 32000 caracteres de contexto |
| MaximoRondas / MaximoInvocaciones / TiempoTurnoSegundos | 8 / 12 / 60 |

Los límites superiores anteriores protegen la ejecución. Se pueden reducir; también se pueden ajustar los umbrales de cobertura/vigencia. Una configuración inválida deshabilita el asistente. Estos valores no son cuotas del proveedor.

## Cuenta y tratamiento de datos

Antes de completar la aceptación, verificar en la cuenta real que el proyecto permite `gemini-3.5-flash-lite` por API en el nivel gratuito y efectuar una consulta desde el chat. No activar facturación ni sustituir nuevamente el modelo si no está habilitado. La documentación del modelo acredita capacidades, no la disponibilidad ni la cuota de una cuenta concreta. El usuario confirmó «Nivel gratuito» para el mismo proyecto. El 2026-11-05 autorizó expresamente cambiar desde 3.8 Flash por los HTTP 503 de alta demanda; se actualizaron requirements, specs, tasks, el valor predeterminado, la configuración privada y los diagnósticos. Flash-Lite devolvió HTTP 200 y texto generado en las tres variantes: sin herramientas, con una mínima y con las seis declaraciones MCP. Esta prueba no ejecutó herramientas y no sustituye la aceptación conversacional. El chat aislado está habilitado y se verificó el acceso OIDC real de Administrador y Operador. Ver [cambio de modelo y validación](cambio-modelo-20261105.md) y [resultados actualizados](../../tests/req009/resultados.md).

Los [términos de Gemini API](https://ai.google.dev/gemini-api/terms) indican que el contenido del nivel gratuito puede utilizarse para mejorar productos y modelos y revisarse por personas. No enviar información sensible, confidencial ni personal. Revisar los requisitos de edad, región y uso aplicables a la cuenta antes de habilitarlo. El asistente envía texto, contexto operativo autorizado y herramientas del catálogo, sin identidad del usuario, credenciales, endpoints de sensores, archivos del proveedor ni sesiones conversacionales persistentes. Limitar el uso de este taller a los datos simulados apropiados para ese tratamiento.

Referencias oficiales: [Gemini 3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite), [precios y nivel gratuito](https://ai.google.dev/gemini-api/docs/pricing), [cuotas por proyecto](https://ai.google.dev/gemini-api/docs/rate-limits), [llamadas de funciones](https://ai.google.dev/gemini-api/docs/generate-content/function-calling), [transporte MCP en memoria](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md#in-memory-transport). SDK oficial MCP fijado en 2.3.1.

## Uso

Administrador y Operador disponen del panel inferior derecho. La flecha despliega/minimiza. “Nueva conversación” borra mensajes y referencias y cancela el turno pendiente. La conversación y el estado minimizado se conservan por pestaña en `sessionStorage`; solo se restauran después de validar la sesión actual. Al cerrar/vencer/cambiar la sesión se descartan. No se utiliza `localStorage`, una tabla de conversación ni un singleton conversacional.

Desde el detalle de un parque/nodo o el monitoreo de un panel, la pregunta incluye únicamente su tipo e ID. Al salir de esas rutas se descarta ese contexto. El servidor vuelve a validar las referencias; los homónimos y referentes ambiguos requieren aclaración. Un recorte de contexto se informa sin borrar el historial visible.

Los mensajes anteriores conservan su texto y fechas. Una pregunta sobre “ahora” vuelve a consultar MCP. Los errores conservan la conversación y ofrecen reintento manual del mismo mensaje sin duplicarlo; se respeta `Retry-After`, incluso tras recargar. Si cae una instancia durante el turno se informa la interrupción: se puede recargar/reintentar en la disponible, sin garantizar recuperación de una respuesta en curso.

## Interpretación de resultados

- Potencia en W; no se calcula energía en kWh sumando muestras. Promedio por panel = suma de potencias / cantidad de muestras. Promedio observado de nodo/parque = suma de promedios de sus paneles.
- Diferencia = promedio observado actual − referencia. Porcentaje = diferencia / referencia × 100; referencia cero produce porcentaje ausente. Cobertura insuficiente produce comparación no concluyente.
- Fechas en America/Montevideo, intervalos UTC `[desde, hasta)`. Fechas ISO sin offset también se interpretan en Montevideo. “Hoy contra ayer” usa igual tramo transcurrido salvo solicitud explícita de días completos. “Durante la mañana” usa 06:00–12:00, limitado por el instante de la pregunta.
- Estado actual = últimas muestras hasta el instante único del turno, con desempate por ID. La potencia actual suma únicamente lecturas vigentes; se separan antiguos y ausentes. Los estados observados conservan sus fechas; no existe un estado global inventado.
- Cobertura = cantidad, primera/última fecha y mayor hueco incluyendo bordes. Paneles faltantes o huecos superiores al umbral implican resultado parcial. Es cobertura observada, no garantía de monitoreo continuo. El simulador envía cada 20 segundos mientras su pantalla está activa.
- Los agregados procesan todas las muestras antes de paginar la salida. Un resumen Global no tiene límite de 100 paneles. Para más de 31 días, `ResumirMediciones` compone en SolarUY los siguientes tramos contiguos con `ComponerConAnterior=true`, dentro del mismo turno y ámbito. El presupuesto de ejecución puede impedir completar todos los tramos y debe informarse lo pendiente.
- Cada resumen/comparación obtiene una vista consistente de MySQL. Herramientas o tramos distintos pueden observar nuevas escrituras. Las consultas no determinan causas de fallas.

## Verificación y métricas

Los logs del adaptador registran código, duración, cantidad de mensajes y correlación; nunca cuerpos, respuestas operativas ni credenciales. Las evidencias depuradas y resultados de controles están en `artifacts/req009/`, excluido de Git.

Ver [reproducción y límites de la aceptación](../../tests/req009/configuracion.md). Las comprobaciones con modelo/identidad sintéticos complementan la aceptación real; no la sustituyen. Las casillas externas pendientes permanecen abiertas en `tasks.md`.
