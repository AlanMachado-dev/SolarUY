# Instrucciones para trabajar en SolarUY

SolarUY es una aplicación Blazor sobre .NET 10 para gestionar parques solares. Las capas Domain, Application, Infrastructure y Presentation son carpetas y namespaces del servidor; SolarUY.Client contiene los componentes de cliente.

## Reglas del proyecto

Antes de diseñar, planificar, implementar o revisar cambios, leer los siete documentos de [.agents/rules](.agents/rules), en este orden:

1. [Clean Architecture](.agents/rules/1_clean_architecture.md).
2. [Principios SOLID](.agents/rules/2_principios_solid.md).
3. [Convenciones de nomenclatura](.agents/rules/3_convenciones_nomenclatura.md).
4. [Modelado de dominio](.agents/rules/4_modelado_dominio.md).
5. [Capa de presentación](.agents/rules/5_capa_presentacion.md).
6. [Protección de diagramas](.agents/rules/6_diagramas_de_SolarUY.md).
7. [Flujo de trabajo y controles](.agents/rules/7_flujo_trabajo_ia.md).

Esta es la configuración compartida del repositorio. Mantener las reglas y skills comunes únicamente en la carpeta .agents de la raíz.

## Diseño y planificación

- Respetar los diagramas de [.agents/Images](.agents/Images); no modificar su contenido.
- Requerimientos: arnes/prompting/requerimientos/<requerimiento>/.
- Modificaciones: arnes/prompting/modificaciones/<modificacion>/.
- Seguir requirements.md, specs.md y tasks.md de la carpeta indicada por el usuario. Implementar por capas y marcar cada subtarea al verificarla.
- Las skills del proyecto están en [.agents/skills](.agents/skills): sdd-generar-specs y sdd-generar-tasks. Utilizarlas para sus respectivos trabajos de diseño y planificación, conservando el alcance solicitado.

## Controles

Ejecutar desde la raíz:

~~~powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
~~~

No avanzar a la siguiente capa de implementación hasta que compile y pasen Domain y ArchUnitNET. Ante un fallo, corregir el código conforme a las reglas; no modificar los tests estructurales para acomodar la implementación. Crear pruebas unitarias al cambiar comportamiento de Domain; no generarlas en Application, Infrastructure ni Presentation.

Las pruebas funcionales auxiliares complementan estos controles. Si se necesitan, guardarlas en arnes/prompting/tests/<identificador>/, por ejemplo req004 o mod002, separadas de la documentación. No presentar una inspección o una simulación como aceptación externa real.

## Configuración y datos privados

Conservar SolarUY/appsettings.Local.json y la base habitual del usuario. No incluir secretos, contraseñas, tokens ni valores de cookies en código, documentos o evidencias. Para pruebas que escriban datos o provoquen fallos, utilizar un destino aislado identificado. Guardar las evidencias depuradas en artifacts/, excluido de Git.
