using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace CodeQuality.Tests;

public static class ArchitectureSetup
{
    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(SolarUY.Components.App).Assembly)
        .Build();

    public const string DomainNamespace = ".*Domain.*";
    public const string ApplicationNamespace = ".*Application.*";
    public const string InfrastructureNamespace = ".*Infrastructure.*";
    public const string PresentationNamespace = ".*Presentation.*|.*Blazor.*|.*UI.*|.*Components.*";
    public const string ApplicationServicesNamespace = "SolarUY.Application.ParquesSolares.Servicios";
}
