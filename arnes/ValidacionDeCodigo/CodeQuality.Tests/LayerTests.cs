using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class LayerTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    /// <summary>
    /// Regla de Dependencia (Clean Architecture):
    /// El Dominio es el centro de la arquitectura. Las dependencias siempre deben apuntar hacia adentro.
    /// El Dominio no debe conocer absolutamente nada sobre cómo se guardan los datos (Infraestructura), 
    /// cómo se orquestan los procesos (Aplicación) ni cómo se muestran al usuario (Presentación).
    /// </summary>
    [Fact]
    public void DomainLayer_ShouldNotHaveDependenciesOnOtherLayers()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(DomainNamespace)
            .Should().NotDependOnAny(
                Classes().That().ResideInNamespace(ApplicationNamespace)
                .Or().ResideInNamespace(InfrastructureNamespace)
                .Or().ResideInNamespace(PresentationNamespace)
            )
            .Because("El Dominio es el núcleo y no debe depender de ninguna otra capa (Clean Architecture).");

        rule.Check(_architecture);
    }

    /// <summary>
    /// Separación de Responsabilidades en la Orquestación:
    /// La capa de Aplicación coordina los Casos de Uso del sistema. Solo debe depender del Dominio 
    /// para ejecutar la lógica de negocio. Nunca debe depender de frameworks de interfaz gráfica (UI) 
    /// ni de implementaciones de bases de datos, manteniéndose agnóstica a estas tecnologías.
    /// </summary>
    [Fact]
    public void ApplicationLayer_ShouldNotDependOnInfrastructureOrPresentation()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(ApplicationNamespace)
            .Should().NotDependOnAny(
                Classes().That().ResideInNamespace(InfrastructureNamespace)
                .Or().ResideInNamespace(PresentationNamespace)
            )
            .Because("La capa de Aplicación solo debe depender del Dominio.");

        rule.Check(_architecture);
    }

    /// <summary>
    /// Evitar acoplamiento directo entre UI y Base de Datos:
    /// La Presentación (ej. Blazor o Controladores API) no debe acceder directamente a la capa de Infraestructura.
    /// Todo pedido debe pasar a través de la capa de Aplicación, lo que permite reusar la lógica, asegurar 
    /// validaciones centralizadas y evitar que la UI quede atada a un motor de base de datos específico.
    /// </summary>
    [Fact]
    public void PresentationLayer_ShouldNotDependOnInfrastructureDirectly()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(PresentationNamespace)
            .Should().NotDependOnAny(
                Classes().That().ResideInNamespace(InfrastructureNamespace)
            )
            .Because("Blazor (UI) no debe acceder directamente a la base de datos o repositorios reales. Debe usar la capa de Aplicación.");

        rule.Check(_architecture);
    }
}
