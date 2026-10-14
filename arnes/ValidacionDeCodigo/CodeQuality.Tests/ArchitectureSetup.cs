using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace CodeQuality.Tests;

public static class ArchitectureSetup
{
    // =========================================================================
    // PASO PARA EL ALUMNO: ¿CÓMO CONECTAR TUS PROYECTOS A ESTOS TESTS?
    // =========================================================================
    // ArchUnitNET no lee carpetas, lee proyectos compilados (.dll).
    // Solo necesitas agregar UNA LÍNEA por cada proyecto (.csproj) que tengas en tu solución.
    // 
    // Para que estos tests puedan analizar tu código, necesitas:
    // 1. Agregar una "Referencia de Proyecto" desde este proyecto de tests hacia tus proyectos reales.
    // 2. Descomentar las líneas de abajo y reemplazar los nombres por CUALQUIER clase que exista dentro de tu proyecto.
    // Al pasarle el tipo (typeof) de una clase, le estamos entregando ese proyecto entero a ArchUnitNET.

    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            // BORRA ESTA LÍNEA DE ABAJO cuando hayas agregado las tuyas:
            typeof(object).Assembly

        // DESCOMENTA Y MODIFICA 
        // typeof (una clase cualqueiera de tu proyecto)


        )
        .Build();

    // Constantes para identificar las capas por Namespace
    public const string DomainNamespace = ".*Domain.*";
    public const string ApplicationNamespace = ".*Application.*";
    public const string InfrastructureNamespace = ".*Infrastructure.*";
    public const string PresentationNamespace = ".*Presentation.*|.*Blazor.*|.*UI.*|.*Components.*";
}
