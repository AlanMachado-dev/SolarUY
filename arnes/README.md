# Herramientas de Control de Calidad - Taller .NET

Este directorio contiene las herramientas y configuraciones necesarias para garantizar la calidad, el diseño y la arquitectura del código del Taller .NET. 

El enfoque utilizado combina el análisis de métricas estáticas con validaciones arquitectónicas y la asistencia guiada por Inteligencia Artificial.

---

## 1. 📊 SonarQube (`/SonarQube`)
Esta carpeta contiene la configuración (archivos de Docker y scripts de ejecución) para levantar y utilizar **SonarQube**.

**Propósito:**
SonarQube es la herramienta principal para el análisis estático continuo. Se encarga de evaluar la calidad interna del código a nivel de implementación.
- Detecta **Código Duplicado** (Copy/Paste).
- Encuentra **Bugs** y vulnerabilidades de seguridad.
- Detecta **Code Smells** (malos olores en el código), como métodos con demasiada complejidad ciclomática o clases gigantes.

---

## 2. 🏗️ Validación de Código (`/ValidacionDeCodigo`)
Esta carpeta contiene el ecosistema diseñado para proteger las convenciones del proyecto (Nomenclatura, Clean Architecture y principios SOLID). Está compuesto por dos partes que trabajan en conjunto: la guía (IA) y los guardarraíles (Tests).

### A. Reglas para el Agente IA (`/agents/rules`)
Contiene archivos Markdown con directrices explícitas sobre cómo debe estructurarse el código. El agente de IA utiliza estas reglas de manera probabilística para enseñar a los estudiantes, sugerir refactorizaciones y evitar que violen principios como el de Responsabilidad Única (SRP) o mezclen capas.

⚠️ **¿Cómo implementar estas reglas en un proyecto real?**
Para que el agente de IA reconozca y utilice estas directrices en el proyecto principal de los alumnos, debes hacer lo siguiente:
1. Copiar la carpeta `agents` ubicada aquí.
2. Pegarla en el **directorio raíz** de la Solución `.sln` del proyecto final.
3. **Renombrar** la carpeta para que empiece con un punto: `.agents` (ej. `/MiProyectoRaiz/.agents/rules/`). El punto inicial es obligatorio para que el sistema de IA la detecte como una carpeta de configuración. Esto puedo cambiar dependiendo del agente que se utilice.

### B. Pruebas Deterministas / Guardarraíles (`/CodeQuality.Tests`)
Contiene un proyecto de pruebas unitarias implementado con **ArchUnitNET**. 
Mientras que el agente de IA es una ayuda proactiva, estos tests actúan como **guardarraíles** (red de seguridad determinista). 

**Propósito:**
Asegurar matemáticamente que las reglas arquitectónicas no se rompan por accidente o desconocimiento. Si el código no cumple con los test establecido, el mismo fallará automáticamente.
- Asegura que la capa de Presentación no acceda a la Base de Datos.
- Verifica que las Entidades del Dominio no dependan de Entity Framework.
- Garantiza convenciones de nomenclatura (ej. que las interfaces empiecen con `I` y los repositorios terminen en `Repository`).

**Implementación:**
Solo basta con agregar el archivo `.csproj` de esta carpeta a la solución del Taller e indicarle en el archivo de configuración qué ensamblados (proyectos) debe evaluar. Depende de cada IDE.
