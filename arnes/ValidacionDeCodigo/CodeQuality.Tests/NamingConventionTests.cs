using System;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class NamingConventionTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    /// <summary>
    /// Estándar de nomenclatura de C#:
    /// En el ecosistema de Microsoft .NET, es un estándar absoluto que todas las interfaces 
    /// comiencen con la letra 'I' mayúscula. Esto permite distinguir visualmente a simple vista 
    /// un contrato abstracto (interfaz) de una implementación concreta (clase).
    /// </summary>
    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        IArchRule rule = Interfaces()
            .Should().HaveNameStartingWith("I")
            .Because("Por convención en C#, todas las interfaces deben comenzar con la letra 'I'.");

        rule.Check(_architecture);
    }

    /// <summary>
    /// Claridad en el manejo de errores:
    /// Sufijar las clases de error con 'Exception' (ej. UserNotFoundException) permite a otros 
    /// programadores identificar de inmediato que esta clase representa un flujo excepcional en 
    /// el sistema y que está destinada a ser lanzada (throw) o capturada (catch).
    /// </summary>
    [Fact]
    public void ExceptionClasses_ShouldEndWithException()
    {
        IArchRule rule = Classes()
            .That().AreAssignableTo(typeof(Exception))
            .Should().HaveNameEndingWith("Exception")
            .Because("Todas las clases que representan un error o excepción deben terminar con el sufijo 'Exception'.");

        rule.Check(_architecture);
    }

    /// <summary>
    /// Identificación de Casos de Uso / Servicios:
    /// En la capa de Aplicación, las clases que coordinan el flujo de negocio deben ser 
    /// fácilmente identificables. Usar el sufijo 'Service' indica claramente su rol como 
    /// orquestador de reglas y operaciones sin estado.
    /// </summary>
    [Fact]
    public void ApplicationServices_ShouldEndWithService()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(ApplicationNamespace)
            //.And().AreAssignableTo("IService") 
            .Should().HaveNameEndingWith("Service")
            .Because("Los casos de uso en la capa de Aplicación deben identificarse claramente con el sufijo 'Service'.");

        rule.Check(_architecture);
    }
}
