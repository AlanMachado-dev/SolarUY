param([Parameter(Mandatory=$true)][string]$Etapa)
$ErrorActionPreference = 'Stop'
$controles = @(
    @{ Nombre='build'; Argumentos=@('build','SolarUY.slnx') },
    @{ Nombre='domain'; Argumentos=@('test','tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj') },
    @{ Nombre='arquitectura'; Argumentos=@('test','arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj') }
)
foreach ($control in $controles) {
    $registro = "artifacts/mod002/$Etapa-$($control.Nombre).log"
    & dotnet @($control.Argumentos) *> $registro
    if ($LASTEXITCODE -ne 0) { Get-Content $registro -Tail 30; throw "Falló $($control.Nombre) en $Etapa" }
    Get-Content $registro -Tail 3
}
