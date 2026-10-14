using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class ArchitectureCoverageTests
{
    [Theory]
    [InlineData(DomainNamespace, "SolarUY.Domain.ParquesSolares.NombreParqueSolar")]
    [InlineData(DomainNamespace, "SolarUY.Domain.ParquesSolares.ParqueSolar")]
    [InlineData(ApplicationNamespace, "SolarUY.Application.ParquesSolares.CrearParqueSolarDto")]
    [InlineData(ApplicationNamespace, "SolarUY.Application.ParquesSolares.Servicios.ParqueSolarService")]
    [InlineData(InfrastructureNamespace, "SolarUY.Infrastructure.Persistencia.ParqueSolarRepository")]
    [InlineData(InfrastructureNamespace, "SolarUY.Infrastructure.Persistencia.SolarUyDbContext")]
    [InlineData(PresentationNamespace, "SolarUY.Presentation.Pages.AltaParqueSolar")]
    public void Architecture_ShouldMatchImplementedLayers(string patron, string nombre)
    {
        Assert.Contains(Types().That().ResideInNamespaceMatching(patron).GetObjects(Architecture),
            tipo => tipo.FullName == nombre);
    }

    [Fact]
    public void Architecture_ShouldLoadSolarUyAndMatchPresentation()
    {
        var tipos = Types().That().ResideInNamespaceMatching(PresentationNamespace)
            .GetObjects(Architecture);

        Assert.Contains(tipos, tipo => tipo.FullName == "SolarUY.Components.App");
    }
}

