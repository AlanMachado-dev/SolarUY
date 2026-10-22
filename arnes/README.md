# Herramientas de control de calidad — Taller .NET

El arnés reúne el análisis estático, las pruebas de arquitectura y el flujo de trabajo asistido por IA de SolarUY.

## Configuración compartida del agente

La configuración activa se mantiene en la raíz del repositorio:

- [AGENTS.md](../AGENTS.md): entrada de instrucciones para Codex, con contexto, reglas y comandos de control.
- [.agents/rules](../.agents/rules): siete documentos de arquitectura, diseño, nomenclatura y flujo de trabajo.
- [.agents/Images](../.agents/Images): diagramas oficiales; no modificar su contenido.
- [.agents/skills](../.agents/skills): sdd-generar-specs y sdd-generar-tasks.

No mantener una segunda copia bajo arnes. Al actualizar una regla o skill, editar su versión de la raíz. AGENTS.md indica expresamente que deben leerse los documentos de rules; el nombre de esa carpeta por sí solo no garantiza su carga automática.

Para reutilizar este material en otro proyecto, adaptar AGENTS.md, las reglas y las skills de la raíz al proyecto de destino. Los diagramas de SolarUY son específicos de este sistema.

## SonarQube

[SonarQube](SonarQube) contiene la configuración para levantar el analizador. Complementa la revisión con detección de duplicación, errores, vulnerabilidades y problemas de mantenibilidad.

## Pruebas de arquitectura

[CodeQuality.Tests](ValidacionDeCodigo/CodeQuality.Tests) contiene los controles existentes de ArchUnitNET sobre capas, dependencias y nomenclatura. El proyecto ya está incluido en SolarUY.slnx.

Ejecutar desde la raíz:

~~~powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
~~~

Corregir el código cuando fallen los controles. No alterar los tests estructurales para adaptar las reglas a la implementación.

## Documentación y pruebas funcionales

El [índice de prompting](prompting/README.md) reúne las ubicaciones vigentes y las equivalencias con los identificadores históricos.

- [prompting/requerimientos](prompting/requerimientos): requirements.md, specs.md y tasks.md de cada requerimiento.
- [prompting/modificaciones](prompting/modificaciones): documentación de cambios.
- [prompting/tests](prompting/tests): pruebas funcionales auxiliares agrupadas por identificador, como req002 o mod001.

Estas pruebas complementan Domain y ArchUnitNET. Las evidencias se conservan depuradas en artifacts/, excluido de Git. La configuración privada del servidor y los datos habituales no pertenecen al arnés.
