# Principios SOLID y Buenas Prácticas de Código

El agente debe revisar activamente la lógica de los métodos generados o analizados para garantizar que cumplan con los principios SOLID, con especial énfasis en el Principio de Responsabilidad Única (SRP).

## 1. Principio de Responsabilidad Única (SRP) en Métodos
- **Una sola tarea:** Los métodos deben hacer exactamente lo que su nombre indica y nada más. No deben tener efectos secundarios ocultos.
- **Detección de Violaciones:** El agente debe marcar como incorrecto cualquier método que combine múltiples operaciones de negocio distintas. 
  - *Ejemplo explícito de lo que NO se debe hacer:* Un método `CrearInscripcion(Estudiante estudiante, Curso curso)` que primero verifica si el estudiante existe en la base de datos y, si no existe, lo crea antes de inscribirlo. Crear un estudiante y crear una inscripción son dos responsabilidades de negocio diferentes.
  - *Solución esperada:* Separar las responsabilidades. El caso de uso debe invocar primero a la lógica de creación/obtención del estudiante (ej. `EstudianteService.ObtenerOCrear(...)`) y luego pasar ese estudiante al método de inscripción (ej. `InscripcionService.Inscribir(...)`).

## 2. Complejidad Ciclomática y Tamaño
- **Métodos cortos:** Si un método requiere múltiples bloques `if/else` para manejar diferentes tareas no relacionadas directamente con su propósito principal, el agente debe sugerir extraer esa lógica a métodos privados o clases especializadas.

## 3. Nombrado que revele la intención
- Si el agente detecta que un método hace dos cosas justificadas, debe exigir que el nombre lo refleje (ej. `ObtenerOCrearEstudiante`). Si el nombre usa la conjunción "Y" o "And" (ej. `CrearInscripcionYEstudiante`), el agente debe advertir que probablemente se esté violando SRP y sugerir una refactorización.
