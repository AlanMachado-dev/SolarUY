using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class DomainTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;


    /// <summary>
    /// Principio de Ignorancia de Persistencia (Persistence Ignorance):
    /// El Dominio es el corazón del negocio y contiene las reglas puras de la aplicación. 
    /// No debe estar acoplado a tecnologías de infraestructura como Entity Framework (ORM) ni bases de datos. 
    /// Esto garantiza que el negocio sea testeable de forma aislada y no dependa de detalles técnicos externos.
    /// </summary>
    [Fact]
    public void DomainLayer_ShouldNotUseEntityFramework()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(DomainNamespace)
            .Should().NotDependOnAnyTypesThat().ResideInNamespace("Microsoft.EntityFrameworkCore.*")
            .Because("El dominio no debe estar acoplado al ORM.");

        rule.Check(_architecture);
    }
}
