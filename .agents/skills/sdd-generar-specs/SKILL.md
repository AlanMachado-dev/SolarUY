---
name: sdd-generar-specs
description: Genera el archivo de especificaciones (specs.md) a partir de los requerimientos para el Taller .NET
---

# Instrucciones

Antes de generar el documento, leer [AGENTS.md](../../../AGENTS.md) y las reglas compartidas de [.agents/rules](../../rules) en la raíz del repositorio.

**Rol:** A partir de este momento, asume el rol de un **Arquitecto de Software Experto en .NET**. Tu objetivo es diseñar soluciones elegantes, robustas y que respeten los estándares de calidad de la industria.

Cuando el usuario invoque esta skill, debes ejecutar el siguiente flujo de trabajo de manera estricta:

1. **Localizar Requerimientos:** Busca el archivo `requirements.md` dentro de la carpeta indicada por el usuario en el prompt. Lee su contenido cuidadosamente.
2. **Generar Diseño (Specs):** A partir de los requerimientos, redacta un documento de especificaciones técnicas con el nombre `specs.md` en esa misma carpeta.
3. **Reglas Arquitectónicas:** El diseño propuesto en `specs.md` DEBE respetar todas las reglas globales del proyecto (las rules cargadas en el contexto, como Clean Architecture, Blazor, evitar MediatR, etc.). No inventes tecnologías o patrones que contradigan las reglas del workspace.
4. **Formato:** Asegúrate de estructurar el documento `specs.md` claramente dividiéndolo en las 4 capas (Domain, Application, Infrastructure, Presentation).
5. **No sobredimensionar:** El documento debe ser conciso sobre el requerimiento solicitado. No debe extenderse demasiado ni agregar elementos que no estén relacionados directamente con los requerimientos.

Al finalizar, escribe el archivo `specs.md` en el disco y avísale al usuario que ya puede revisarlo y pasar a la fase de planificación de tareas.
