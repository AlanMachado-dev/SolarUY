# Registro histórico de ajustes del arnés

Relacionado con [req001: alta de parque solar](requerimientos/req001_alta_parque_solar/tasks.md). Las rutas de este registro describen el estado al autorizar los cambios; la configuración compartida vigente está en [AGENTS.md](../../AGENTS.md).

Autorizados explícitamente por el usuario y aplicados el 2026-10-14. Este documento registra el alcance aprobado.

1. En `arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`, cambiar `net8.0` a `net10.0` y agregar referencia a `SolarUY/SolarUY.csproj`. Conservar paquetes y reglas existentes.
2. En `ArchitectureSetup.cs`, cargar `typeof(SolarUY.Components.App).Assembly` en lugar de `typeof(object).Assembly`.
3. Usar `ResideInNamespaceMatching` (API de ArchUnitNET 0.13.4) en los selectores que utilizan los patrones existentes, tanto para capas como para dependencias de EF. Así los patrones identifican los namespaces reales.
4. En `ApplicationServices_ShouldEndWithService`, seleccionar las clases del namespace `SolarUY.Application.ParquesSolares.Servicios`. Allí se colocarán las implementaciones de servicios; los contratos y DTO permanecerán en `SolarUY.Application.ParquesSolares`. Mantener la exigencia de sufijo `Service` para todas las clases seleccionadas.
5. Añadir una comprobación de cobertura que demuestre que el ensamblado de SolarUY está cargado y que los selectores reconocen sus tipos reales, evitando éxitos con selección vacía.
6. Incorporar el proyecto de calidad a `SolarUY.slnx`.

No modificar el proyecto `CodeQuality.Tests.csproj` que ya existe sin seguimiento en la raíz: el plan identifica como arnés el ubicado en `arnes/ValidacionDeCodigo/CodeQuality.Tests`.

Las reglas de aislamiento de capas, de repositorios, de excepciones y de interfaces conservan su significado. Este ajuste permite verificar los DTO exigidos por las especificaciones sin renombrarlos ni desplazarlos de Application.

## Ajuste adicional para la implementación incremental

La primera ejecución conectada descubre 10 pruebas: 2 pasan y 8 fallan porque aún no existen tipos de Domain, Application, Infrastructure, repositorios, interfaces o excepciones. ArchUnitNET 0.13.4 exige por defecto resultados positivos incluso para selecciones vacías.

Se propone añadir `WithoutRequiringPositiveResults()` a las reglas existentes. Sus prohibiciones y convenciones siguen aplicándose a todos los tipos que efectivamente existan. La comprobación de cobertura del ensamblado sigue siendo obligatoria; al incorporar Domain, Application e Infrastructure se ampliará para comprobar sus tipos reales, y al cierre deberá demostrar presencia de las cuatro capas, servicio y repositorio. Esto permite pasar los controles de la capa actual antes de implementar la siguiente, sin añadir clases ficticias de capas futuras.

El usuario autorizó explícitamente este ajuste adicional el 2026-10-14. Fue aplicado temporalmente durante la implementación por capas. Por solicitud explícita posterior del usuario, se retiraron las nueve llamadas a `WithoutRequiringPositiveResults()` de las reglas. Se conservan las comprobaciones de cobertura de las cuatro capas, el servicio y el repositorio reales.

Revalidación del 2026-10-14: compilación correcta, 17/17 pruebas arquitectónicas y 15/15 pruebas de dominio correctas, sin omisiones. Las reglas vuelven a exigir resultados positivos y fallan ante selecciones vacías.
