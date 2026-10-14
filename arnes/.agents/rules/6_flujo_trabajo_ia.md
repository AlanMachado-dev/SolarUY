# ⚠️ Instrucciones Obligatorias de Flujo de Trabajo (Para el Agente IA)

Como asistente de Inteligencia Artificial operando en este proyecto, estás sujeto a un ecosistema estricto de control de calidad y arquitectura. Tu objetivo no es solo generar código, sino generar código que cumpla con los estándares.

Para garantizar esto, **DEBES** seguir este flujo de trabajo de manera ineludible:

## 1. Conciencia de las Reglas
Antes de escribir cualquier bloque de código C#, debes tener en cuenta las reglas estáticas y de diseño definidas en los otros archivos de esta carpeta (`1_clean_architecture.md`, `3_convenciones_nomenclatura.md`, `2_principios_solid.md`, etc).

## 2. Auto-Validación Continua (Ejecución de Tests)
No asumas que tu código es perfecto. Cada vez que crees, modifiques o refactorices código (especialmente si agregas nuevas dependencias, clases, repositorios o servicios), tienes la **OBLIGACIÓN** de verificar que no has roto la arquitectura.
- Debes usar tu herramienta de terminal (ej. `run_command`) para ejecutar las pruebas automatizadas del proyecto de control de calidad.
- El comando a ejecutar es: `dotnet test` apuntando al proyecto `CodeQuality.Tests` (deberás buscar la ruta en el espacio de trabajo del usuario si no es evidente).

## 3. Auto-Corrección
Si al ejecutar `dotnet test` algún test de ArchUnitNET falla, **NO le pidas al usuario que lo arregle ni des por terminada tu tarea.**
1. Lee el mensaje de error del test fallido (que indica qué regla arquitectónica rompiste).
2. Modifica el código que generaste para solucionar el problema.
3. Vuelve a ejecutar los tests.
Solo debes reportar tu trabajo al usuario cuando todas las pruebas pasen en verde.
4. No se deben de modificar los test para que pasen sin consultar al usuario explícitamente que autorice esta modificación.

## 4. TDD y Pruebas Unitarias
- **Obligatorio para el Dominio:** Cada vez que generes o modifiques comportamiento o lógica dentro de la capa de `Domain` (ej. métodos de entidades, validaciones, cálculos), **DEBES** generar simultáneamente la prueba unitaria (xUnit) correspondiente para validar ese comportamiento.
- **Silencio en otras capas:** Para las capas de `Application`, `Infrastructure` o `Presentation`, NO generes pruebas unitarias. Además, NO debes preguntarle ni sugerirle al usuario la creación de tests en estas capas. Mantén tus respuestas enfocadas estrictamente en lo que el usuario solicitó.

## 5. Integridad de los Guardarraíles (Prohibido Alterar Tests Estructurales)
- **Regla de Inmutabilidad:** Tienes estrictamente **PROHIBIDO** modificar los archivos del proyecto `CodeQuality.Tests` (los tests de ArchUnitNET) para lograr que una prueba pase. 
- **Resolución de Conflictos:** Si un test arquitectónico falla, tu obligación es SIEMPRE corregir el código fuente del sistema para que cumpla con el test. NUNCA debes relajar ni alterar el test para adaptarlo al código mal diseñado.
- **Intervención Humana:** Si tras analizar un error crees genuinamente que la regla de ArchUnitNET está mal planteada, está desactualizada, o se requiere una excepción justificada para el diseño actual, debes **DETENERTE**. Explica la situación al usuario humano y solicita su autorización explícita antes de editar cualquier archivo de pruebas de calidad.
