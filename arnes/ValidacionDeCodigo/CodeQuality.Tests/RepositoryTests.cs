using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class RepositoryTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    /// <summary>
    /// Principio de Inversión de Dependencias (DIP):
    /// Los repositorios concretos (las clases que realmente hacen consultas SQL o manejan archivos) 
    /// son detalles de implementación técnica, por lo tanto, pertenecen a la capa de Infraestructura.
    /// El núcleo (Dominio/Aplicación) solo debe conocer las interfaces de estos repositorios, nunca su implementación.
    /// </summary>
    [Fact]
    public void RepositoryImplementations_ShouldResideInInfrastructure()
    {
        IArchRule rule = Classes()
            .That().HaveNameEndingWith("Repository")
            .Should().ResideInNamespace(InfrastructureNamespace)
            .Because("Las implementaciones concretas de los repositorios deben estar en la capa de Infraestructura.");

        rule.Check(_architecture);
    }

    /// <summary>
    /// Claridad y consistencia en el patrón Repository:
    /// Establece un lenguaje común en el equipo. Saber que todas las interfaces que manejan persistencia
    /// se llaman 'IRepository' (ej. IUserRepository) facilita la búsqueda, el mantenimiento y la lectura del código.
    /// </summary>
    [Fact]
    public void RepositoryInterfaces_ShouldStartWithIAndEndWithRepository()
    {
        IArchRule rule = Interfaces()
            .That().HaveNameEndingWith("Repository")
            .Should().HaveNameStartingWith("I")
            .Because("Las interfaces de repositorios deben seguir la convención 'IRepository'.");

        rule.Check(_architecture);
    }
}
