# mod009 - Tareas: Claridad y presentación de las respuestas del asistente

## Alcance y seguimiento

Plan basado en [requirements.md](requirements.md) y [specs.md](specs.md). Las referencias CA1–CA9 corresponden a los nueve criterios de aceptación del requerimiento. Las casillas marcan únicamente tareas implementadas o verificadas. Las comprobaciones auxiliares no acreditan la aceptación externa real.

El usuario autorizó expresamente actualizar los checkboxes de este `tasks.md` durante la implementación. Durante la implementación, conservar `requirements.md`, `specs.md` y los demás documentos de `arnes/prompting/requerimientos/`, conforme a la restricción del requerimiento. Registrar el avance y las comprobaciones en `artifacts/mod009/seguimiento.md`, excluido de Git; actualizar estas casillas únicamente si se autoriza explícitamente la edición del plan.

Trabajar en orden: Domain → Application → Infrastructure → Presentation → aceptación funcional. No avanzar a la siguiente capa si fallan sus controles. Las rutas de código y los comandos se interpretan desde la raíz del repositorio.

## 0. Preparación

- [x] **P01.** Revisar el estado inicial de Git y los archivos del asistente; identificar cambios previos que deban conservarse. Mantener los diagramas, el simulador, `appsettings.Local.json` y la base habitual intactos.
- [x] **P02.** Preparar el registro depurado en `artifacts/mod009/` y confirmar su exclusión de Git. Registrar únicamente casos, resultados, períodos, cifras de contraste y errores depurados; no guardar conversaciones completas, credenciales, tokens ni cookies.

## 1. Domain

- [x] **D01.** Confirmar la reutilización sin cambios de `ResumenMediciones`, `PotenciaObservada`, `ComparacionPotencia` y `EstadoActualObservado`. Identificar los campos que representan promedio por panel, suma de promedios, cantidad y cobertura para conservar sus significados al redactar. **CA4, CA7, CA8.**
- [x] **D02.** Confirmar que la propuesta mantiene precisión decimal, agregación antes de paginar, ausencia frente a cero y comparaciones parciales. No crear entidades ni pruebas unitarias nuevas: este plan no modifica comportamiento de Domain.

### Control de salida de Domain y línea inicial

- [x] **D03.** Ejecutar `dotnet build SolarUY.slnx` y registrar el resultado y las advertencias iniciales.
- [x] **D04.** Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado.
- [x] **D05.** Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado de ArchUnitNET. Resolver cualquier impedimento antes de comenzar Application, sin alterar tests estructurales.

## 2. Application

### Contratos y resolución temporal

- [x] **A01.** Ampliar `ConsultaOperativaDto` con la modalidad `Ultimos`, `CantidadPeriodo` y `UnidadPeriodo`, documentando las unidades cerradas y la exclusión de `Desde`/`Hasta` en esa modalidad. Mantener los argumentos actuales y los períodos de referencia de las comparaciones. **CA6.**
- [x] **A02.** Extender `PeriodoConsultaService` para calcular duraciones móviles desde el instante UTC del turno; admitir segundos, minutos, horas y días de 24 horas. Conservar la conversión posterior a `America/Montevideo`. **CA5, CA6.**
- [x] **A03.** Validar cantidad positiva y finita, unidad admitida, desbordamientos, combinaciones contradictorias y límite por invocación. Rechazar argumentos inválidos sin truncar duraciones ni intercambiar extremos automáticamente. **CA6, CA8.**
- [x] **A04.** Exigir inicio anterior al fin en intervalos históricos y tratar explícitamente ventanas calendario todavía no iniciadas, como la mañana antes de las 06:00, sin consultarlas como intervalos invertidos ni presentarlas como cero. **CA6, CA8.**
- [x] **A05.** Integrar la resolución en `ConsultaOperativaService` usando el instante capturado por `AsistenteService` y transmitido por MCP. Usar el mismo `PeriodoConsultaDto` para persistencia, cobertura y descripción; conservar composición de tramos y límites existentes. **CA6.**

### Formato y datos preparados para la respuesta

- [x] **A06.** Definir el DTO opcional de presentación de los resultados, vinculando cada valor legible con el elemento y el campo correspondiente. Conservar cifras originales, UTC, códigos, referencias y metadatos de paginación. **CA4, CA5, CA8.**
- [x] **A07.** Crear `FormatoRespuestaAsistenteService` con una única responsabilidad de formato e inyección por constructor. No incorporar acceso a persistencia ni cálculo de estadísticas, estados o cobertura.
- [x] **A08.** Implementar formato de potencia `es-UY`, unidad W, hasta dos decimales y redondeo de punto medio alejándose de cero, sin ceros finales innecesarios. Distinguir `null`, cero medido y positivos que redondearían a cero; para estos últimos usar «menos de 0,01 W». **CA4, CA8.**
- [x] **A09.** Preparar fechas `dd/MM/yyyy`, horas de 24 horas y límites en Uruguay sin ISO, fracciones de segundo ni offsets repetidos. Conservar segundos en intervalos cortos y ambas fechas al cruzar medianoche; mantener los límites exactos en los datos originales. **CA5, CA6.**
- [x] **A10.** Preparar etiquetas comprensibles de estado, antigüedad y cobertura desde los indicadores existentes; conservar las diferencias entre ausencia, simulación, datos parciales y errores. **CA2, CA7, CA8.**
- [x] **A11.** Incorporar los textos de presentación a las salidas de las herramientas que contienen cifras, fechas o períodos, sin sustituir datos necesarios para llamadas sucesivas ni aplicar redondeo a los cálculos. **CA4, CA5.**

### Redacción y salida

- [x] **A12.** Actualizar las instrucciones de `AsistenteService` para responder primero la pregunta, identificar el elemento por nombre y pertenencia necesaria y usar párrafos breves. Ante información parcial, calificar el resultado desde la primera oración. **CA1, CA2, CA7.**
- [x] **A13.** Indicar el uso de `ResumirMediciones` para promedios y reservar el historial para detalle solicitado o necesario. Mantener las herramientas específicas de estado y comparación y la distinción entre promedio de un panel y suma de promedios. **CA3, CA8.**
- [x] **A14.** Exigir la modalidad relativa cuando corresponda y el uso de cifras y períodos preparados por SolarUY. Evitar que el modelo calcule estadísticas, reste horas o reformatee límites por su cuenta. **CA4, CA5, CA6.**
- [x] **A15.** Sustituir encabezados y advertencias repetitivos por aclaraciones breves sobre simulación, cobertura y datos pendientes. Mantener permisos, desambiguación, consulta actualizada por MCP, límites y avisos de contexto recortado; no ocultar información que impida interpretar la respuesta. **CA1, CA2, CA7, CA8.**
- [x] **A16.** Normalizar marcas reconocidas de presentación en las respuestas del asistente, preservando nombres y contenido literal. Mantener texto plano y saltos de línea; no reemplazar globalmente cifras, fechas o signos ni modificar las preguntas del usuario. **CA9.**
- [x] **A17.** Conservar `RespuestaAsistenteDto`, sus códigos y referencias; asegurar que argumentos inválidos, fallo de dependencia, cuota y timeout no se comuniquen como ausencia de mediciones. No añadir otra llamada de IA para reformular ni reintentos automáticos. **CA8.**

### Control de salida de Application

- [x] **A18.** Ejecutar `dotnet build SolarUY.slnx`.
- [x] **A19.** Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] **A20.** Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`. Registrar los tres resultados y corregir el código antes de continuar. No crear pruebas unitarias de Application.

## 3. Infrastructure y raíz de composición

- [x] **I01.** Registrar `FormatoRespuestaAsistenteService` en la raíz de composición y resolver su inyección. Conservar las dependencias entre capas y el ámbito por operación existente.
- [x] **I02.** Verificar que `GeminiConversacional` transporte instrucciones, esquemas y textos de presentación actualizados sin incorporar formato o aritmética temporal al adaptador ni cambiar proveedor o modelo.
- [x] **I03.** Verificar que `ClienteHerramientasMcp` conserve campos nuevos, instante único y correlación de llamadas; mantener las firmas del proveedor y la conexión por turno.
- [x] **I04.** Confirmar que `ConsultaOperativaRepository` utiliza los límites resueltos con semántica UTC `[desde, hasta)` y mantiene filtros, paginación y agregados; no añadir migraciones ni modificar la conexión habitual.
- [x] **I05.** Comprobar el uso explícito de cultura `es-UY` y zona `America/Montevideo`, sin depender de la configuración del host ni añadir estado conversacional compartido.

### Control de salida de Infrastructure

- [x] **I06.** Ejecutar `dotnet build SolarUY.slnx`.
- [x] **I07.** Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] **I08.** Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`. Registrar los tres resultados y resolver fallos antes de continuar. No crear pruebas unitarias de Infrastructure.

## 4. Presentation

- [x] **V01.** Actualizar las descripciones de `ServidorAsistenteMcp` y comprobar que su esquema expone `Ultimos`, cantidad y unidad. Mantener validación y resolución temporal delegadas a Application. **CA6.**
- [x] **V02.** Verificar `ChatAsistente.razor` con texto escapado, saltos de línea y viñetas simples. Ajustar espaciado solo si es necesario, conservando lectura accesible y dimensiones adaptables. **CA9.**
- [x] **V03.** Confirmar que ni el componente ni `chat-asistente.js` calculan promedios, redondean resultados, convierten períodos o reinterpretan cobertura. Mantener la ausencia de HTML ejecutable y de un motor Markdown.
- [x] **V04.** Conservar historial, referencias, minimizado, navegación, nueva conversación y reintento manual; las respuestas anteriores mantienen su contenido y referencia temporal al restaurarlas. Preservar limpieza al vencer o cambiar la sesión.

### Control de salida de Presentation

- [x] **V05.** Ejecutar `dotnet build SolarUY.slnx`.
- [x] **V06.** Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] **V07.** Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`. Registrar los resultados y corregir fallos sin modificar tests estructurales. No crear pruebas unitarias de Presentation.

## 5. Verificación funcional y aceptación

Las comprobaciones auxiliares se ubican en `arnes/prompting/tests/mod009/`, separadas de la documentación. Reutilizar la preparación aislada existente cuando resulte adecuada y comprobar su destino antes de insertar lecturas o provocar fallos. Las verificaciones controladas complementan la aceptación real desde el chat con Gemini, MCP y una sesión autorizada.

- [x] **F01.** Preparar datos conocidos en el destino aislado: panel con lecturas suficientes, panel con cobertura parcial, panel sin lecturas, cero medido, positivo muy pequeño y lectura antigua. Añadir paneles con nombres repetidos en pertenencias diferentes.
- [x] **F02.** Preparar una comprobación funcional del servidor con reloj controlado y contraste de resultados MCP para períodos relativos. Mantener la sustitución del reloj exclusivamente en el entorno auxiliar; no añadir controles de prueba a la aplicación habitual.
- [x] **F03.** Contrastar `[T - 2 minutos, T)` con las muestras realmente incluidas, sus límites inclusivo/exclusivo y el promedio calculado por SolarUY. Repetir cerca de medianoche y con otra zona horaria del host. **CA4, CA5, CA6.**
- [x] **F04.** Verificar duración ausente, cero, negativa, unidad desconocida, desbordamiento, combinación relativa con extremos explícitos e intervalo invertido. Comprobar rechazo comprensible y ausencia de un resultado exitoso inventado. **CA6, CA8.**
- [x] **F05.** Verificar «esta mañana» antes de las 06:00 y la conservación de `Hoy`, `Ayer`, `Manana`, `Explicito`, comparación y composición de tramos dentro de los límites existentes. **CA6.**
- [x] **F06.** Desde el chat real, preguntar por el promedio de los últimos dos minutos con cobertura completa y parcial. Contrastar primero el intervalo ejecutado y después las horas y cifras comunicadas; verificar resultado al comienzo y ausencia de lecturas individuales innecesarias. **CA1, CA2, CA3, CA4, CA5, CA6, CA7.**
- [ ] **F07.** Continuar con «mostrame las mediciones utilizadas» y comprobar que los datos y la paginación corresponden al período solicitado, sin confundir un detalle parcial con el total. **CA3, CA8.**
- [x] **F08.** Consultar paneles en falla y comparar nodos o parques con diferente cantidad de muestras por panel. Confirmar nombres y pertenencias, desambiguación de homónimos y explicación correcta de agregados, sin declarar conclusiones globales con cobertura parcial. **CA1, CA2, CA7, CA8.**
- [x] **F09.** Revisar respuestas para ausencia de datos, cero, positivo que redondearía a cero y lectura antigua; verificar unidad, coma decimal y redondeo sin alterar la precisión de origen. **CA4, CA7, CA8.**
- [x] **F10.** Verificar en el chat párrafos, viñetas escapadas, delimitadores de énfasis, nombres con signos y contenido con apariencia de HTML. Comprobar legibilidad, ausencia de marcas accidentales y preservación de contenido literal, sin ejecución de HTML. **CA9.**
- [ ] **F11.** Provocar de forma aislada fallo de proveedor, cuota y fallo MCP o persistencia. Comprobar mensajes distinguibles de «sin datos», reintento manual sin duplicados y continuidad de las demás funcionalidades. **CA8.**
- [ ] **F12.** Comprobar acceso con Administrador y Operador y rechazo de sesión no autorizada. Confirmar que instrucciones del usuario o contenido de herramientas no cambian permisos y que el asistente sigue siendo de solo lectura. **CA8; regresión de req009.**
- [ ] **F13.** Minimizar, navegar, restaurar y comenzar otra conversación; verificar conservación de mensajes anteriores y descarte al vencer la sesión. Repetir consultas por ambas instancias a través del balanceador y contrastar formato y período. **Regresión de req009.**
- [x] **F14.** Registrar por caso el resultado observado, tipo de comprobación y evidencia depurada. Mantener explícitamente pendientes las verificaciones externas que no se hayan ejecutado; no presentar inspecciones o respuestas simuladas como aceptación real.

## 6. Cierre

- [x] **C01.** Revisar el diff final: sin cambios en documentos protegidos, diagramas, fórmulas de Domain, tests estructurales, secretos, configuración local ni base habitual.
- [ ] **C02.** Confirmar que están registrados los controles satisfactorios de la última capa y la cobertura de CA1–CA9. Si hubo cambios de código después de esos controles, volver a ejecutarlos antes del cierre.
- [x] **C03.** Informar los archivos implementados, los resultados de validación y cualquier aceptación externa pendiente, sin dar por completado un caso que no tenga evidencia.

## Estado de verificación de esta implementación

Implementación por las cuatro capas terminada. En cada salida de capa pasaron compilación, 135 pruebas de Domain y 17 de ArchUnitNET, sin modificar las suites originales. La última ejecución volvió a pasar después del ajuste final de las etiquetas temporales. Se ejecutaron 44 comprobaciones funcionales auxiliares satisfactorias con MCP real y MySQL 9.7.0 temporal aislado; no se utilizó la base habitual. La instancia y sus datos desechables ya se retiraron.

En el navegador se comprobó el componente real con identidad y modelo sintéticos: formato, HTML inerte, navegación, minimizado, nueva conversación, Operador, vencimiento y rechazo de otro rol. Las evidencias depuradas están en `artifacts/mod009/`; el ejecutable auxiliar y sus instrucciones están en `arnes/prompting/tests/mod009/`.

| Tarea | Estado y evidencia |
| --- | --- |
| F03 | Verificada: intervalos exactos, extremos incluidos/excluidos y medianoche mediante MCP con reloj controlado; en el chat real, el tramo de dos minutos y las fechas de Uruguay fueron coherentes también por app2, configurada con TZ Asia/Tokyo. |
| F06 | Verificada desde el chat con Administrador OIDC real: principal 200,25 W y parcial 168,37 W, tramo de 120 segundos, advertencia de cobertura y sin enumeración de lecturas. Contraste con las muestras sintéticas de MySQL. |
| F07 | Se detectó un fallo real: el seguimiento desplazó 25 segundos el intervalo y devolvió 5 muestras en lugar de las 6 del promedio anterior. Se corrigió el transporte de límites UTC exactos en el historial y las instrucciones para reutilizarlos con Explicito. Falta repetir el caso desde el chat desplegado con esta corrección; las comprobaciones auxiliares no cierran esta aceptación. |
| F08 | Verificada desde el chat real: dos lecturas en falla clasificadas como antiguas, nombres y nodos correctos; comparación Norte/Sur con suma de promedios por panel (408,62 W y 300 W hoy), sin afirmar una mejora general con cobertura parcial; aclaración ante Mod009 compartido en dos nodos. |
| F09 | Verificada desde el chat real y contrastada con MySQL: ausencia de datos, 0 W medidos, menos de 0,01 W para el positivo pequeño, 168,37 W para el parcial y 40 W con lectura antigua. |
| F11 | Se comprobaron 429/503 con transporte local y fallos de persistencia y MCP. En el chat real se observó el rechazo por contexto excedido, sin inventar resultados. Falta completar errores y reintentos desde el chat real. |
| F12 | Administrador OIDC real reconocido en ambas instancias; 401 sin sesión y rechazo del modelo a cambiar permisos o eliminar datos. El usuario informa inicio real como Operador, todavía sin comprobación de esta sesión por el agente: el control del navegador no está disponible en esta ronda. |
| F13 | Historial conservado al navegar completamente entre app1 y app2 con la sesión real; nueva conversación comprobada. Faltan completar minimizado/navegación, descarte por vencimiento y recuperación ante caída en este entorno. El acceso al motor Docker por CLI continúa denegado. |
| C02 | Los controles de código pasan; el cierre de aceptación sigue pendiente de F07, F11, F12 y F13. |

La revisión automática rechazó inicialmente `--gemini` por no considerar acreditada la autorización para enviar contenido a Google. El usuario autorizó explícitamente enviar preguntas y resultados sintéticos de la instancia aislada con la API configurada. Con esa autorización, la prueba terminó en `Exito` y pasaron sus ocho comprobaciones de contenido y formato. Se guardaron únicamente códigos y verificaciones depuradas en `artifacts/mod009/gemini-autorizado.json`. Esto no sustituye la posterior aceptación desde el chat con OIDC real ni la del balanceador; las casillas pendientes se conservan hasta completar cada escenario íntegro.

Preparación de OIDC y balanceo (2026-11-18): se agregó `arnes/prompting/tests/mod009/preparar-oidc.ps1` para levantar el proyecto separado `solaruy-mod009-aceptacion` en `https://localhost:9449`, con base, claves e imagen propias y zonas UTC/Asia-Tokyo. El script y Compose están validados; la preparación y actualización de datos se ejecutaron contra MySQL temporal aislado usando las migraciones reales, sin duplicados (1 parque, 9 paneles, 23 lecturas; promedio principal de hoy: 200,25 W). El auxiliar compila y las suites originales volvieron a pasar (Domain 135/135; ArchUnitNET 17/17). El MySQL temporal se apagó y sus datos se retiraron. Docker Desktop mantiene activo el despliegue habitual, pero el acceso al motor por CLI está denegado en esta sesión: el arranque aislado debe ejecutarse desde el PowerShell del usuario. No se iniciaron esos contenedores ni se validaron aún OIDC real, ambas instancias o recuperación; F03, F12, F13 y el cierre conservan sus pendientes. Evidencia depurada: `artifacts/mod009/oidc-preparacion.json`.
Actualización posterior al ingreso informado como Operador: el proxy ya conserva 9449 en sus tres selectores (comprobado por HTTP). La corrección de F07 conserva límites temporales UTC completos como metadatos opcionales de la respuesta y del historial; el chat los transporta sin calcular fechas y Application exige una nueva consulta MCP con esos límites. Compilación, Domain 135/135 y ArchUnitNET 17/17 correctos después de Application y Presentation; sintaxis JavaScript correcta. Pasaron 12 comprobaciones locales y 11 con Gemini real autorizado, MCP y MySQL aislados: el seguimiento 25 segundos después mantiene el intervalo exacto, incluidas fracciones, y las seis muestras. Evidencias depuradas: `artifacts/mod009/seguimiento-local.json` y `seguimiento-gemini.json`. La identidad y el reloj de estas dos comprobaciones son sintéticos: F07 continúa pendiente del chat desplegado con la corrección. El usuario informó ingreso OIDC como Operador, pero el control del navegador no está disponible en esta ronda y Docker CLI sigue denegado; no se marcan F12 ni recuperación por caída. Instrucciones reproducibles en `arnes/prompting/tests/mod009/README.md`.