# req009 — Cambio autorizado a Gemini 3.5 Flash-Lite

Fecha: 2026-11-05. El usuario indicó expresamente «utiliza 3.5 Flash-Lite» y solicitó documentar los cambios.

## Motivo y decisión

La comparación controlada de `gemini-3.8-flash` devolvió HTTP 503 `UNAVAILABLE` por alta demanda en las tres variantes, incluso sin herramientas. Se adopta `gemini-3.5-flash-lite` en el mismo proyecto gratuito. Las evidencias anteriores se conservan; no se atribuye el fallo a MCP ni se garantiza disponibilidad futura del modelo nuevo.

## Cambios realizados

- `requirements.md`, `specs.md` y `tasks.md`: modelo autorizado y trazabilidad del cambio; FUN06 se verificó de nuevo con Flash-Lite.
- `SolarUY/Application/Asistente/ContratosConsulta.cs`: valor predeterminado de `AsistenteOptions.Modelo` a `gemini-3.5-flash-lite`.
- Archivo privado adicional del servidor: se modificó únicamente `Asistente.Modelo`, conservando clave y demás opciones, sin copiar valores privados a documentación o evidencias.
- Cuatro diagnósticos PowerShell de req009: utilizan Flash-Lite; las comprobaciones individuales guardan nombres de evidencia nuevos y la comparación crea un directorio fechado.
- Documentación de configuración y resultados: instrucciones actuales, diagnóstico anterior identificado como histórico y separación entre disponibilidad básica y aceptación conversacional.

Se conserva el adaptador HTTPS `GeminiConversacional`, el catálogo de seis herramientas y el ciclo modelo → MCP → consultas autorizadas. No se añaden proveedores, selección automática de modelos, reintentos automáticos, facturación, tablas ni migraciones. Las instancias habituales req008 no se actualizan; el despliegue para verificación es exclusivamente `solaruy-req009-aceptacion` en HTTPS 9449.

## Verificación real de la API

Desde app1 de aceptación, misma clave, pregunta fija «Responde solamente OK.», máximo 512 tokens, pensamiento `low`, tiempo máximo de 60 segundos y AUTO predeterminado. Un intento por variante; solo cambian las declaraciones de herramientas.

| Variante | HTTP | Duración | Texto generado |
| --- | --- | --- | --- |
| Sin herramientas | 200 | 0,86 s | Sí |
| Una herramienta mínima | 200 | 0,78 s | Sí |
| Seis herramientas MCP reales | 200 | 0,84 s | Sí |

Evidencia depurada: `artifacts/req009/comparacion-gemini-20261105T225012386Z/resumen.json`. No se ejecutaron funciones en esta comparación ni se enviaron datos operativos/identidad. La clave se transmitió solo por stdin al curl del contenedor. El mismo proyecto fue confirmado por el usuario como «Nivel gratuito»; no se modificó su facturación. Estos resultados acreditan generación básica y aceptación de las declaraciones, no el ciclo completo de herramientas.

## Controles del cambio

- `dotnet build SolarUY.slnx -m:1`: correcto, cero errores.
- Domain: 87/87 pruebas correctas.
- ArchUnitNET: 17/17 pruebas correctas, sin modificar tests estructurales.
- Análisis de sintaxis PowerShell: correcto en los cuatro diagnósticos.

Evidencias: `artifacts/req009/cambio-modelo-build.txt`, `cambio-modelo-domain.txt` y `cambio-modelo-arquitectura.txt`. Permanecen las advertencias NU1903 preexistentes de WebAssembly.Server 10.0.12.

## Despliegue y consulta desde el chat

Se reconstruyó exclusivamente `solaruy:req009-aceptacion` y se recrearon app1/app2 del proyecto aislado con `--no-deps --wait`. Ambas quedaron saludables con el archivo privado configurado para Flash-Lite y la misma clave. Se conservaron MySQL, nginx y el anillo de Data Protection. Las dos instancias habituales permanecen saludables con `solaruy:req008`.

La sesión OIDC real de Operador sobrevivió a la actualización. Se reintentó desde el chat la pregunta pendiente «Como esta funcionando todo hoy?». El modelo produjo una respuesta operativa con resumen y estado actual de los datos simulados, identificando cobertura parcial, lecturas antiguas y paneles sin muestras. El adaptador registró tres pasos exitosos, coherentes con un ciclo de herramientas y respuesta. No se guardaron cuerpos de conversación ni identidad en los logs.

Se contrastaron mediante consultas SQL de solo lectura, exclusivamente en el esquema `solaruy_req009`, los valores de esa respuesta: 4 paneles, 2 con muestras, 8 muestras, mínimo 0 W, máximo 441,24 W, promedio de muestras 122,72 W y suma de promedios por panel 225,96 W; distribución 5 muestras Operativo, 1 EnFalla y 2 Desactivado. Coinciden con los datos aislados al instante del turno.

Esta primera comprobación confirmó una respuesta real desde el chat tras el cambio. FUN14 quedó pendiente entonces: la redacción denominó «promedio ponderado» a la suma de promedios por panel, aunque aclaró la fórmula entre paréntesis. La ejecución de aceptación posterior corrigió esa etiqueta, contrastó la respuesta sobre 125 paneles y aprobó FUN14; se conserva aquí la observación histórica.

Evidencias depuradas en `artifacts/req009/`: `cambio-modelo-imagen.txt`, `cambio-modelo-despliegue.txt`, `cambio-modelo-verificacion.json`, `cambio-modelo-eventos.json`, `cambio-modelo-contraste-datos.txt` y `chat-gemini-35-flash-lite.png`. La captura está recortada al chat para excluir identidad; el temporal privado completo se eliminó.

La aceptación posterior está completa (197/197 tareas). Las correcciones, comprobaciones reales e inyecciones identificadas se registran en [aceptacion-20261105.md](../../tests/req009/aceptacion-20261105.md), [resultados.md](../../tests/req009/resultados.md) y las casillas verificadas de [tasks.md](tasks.md).
