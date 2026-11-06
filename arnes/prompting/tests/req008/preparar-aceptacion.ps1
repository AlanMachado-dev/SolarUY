$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath 'SolarUY.slnx')) { throw 'Ejecutá esta herramienta desde la raíz de SolarUY.' }
$directorio = Join-Path $PWD '.solaruy-acceptance'
if (Test-Path -LiteralPath (Join-Path $directorio 'host.json')) {
    Copy-Item -LiteralPath nginx.conf -Destination (Join-Path $directorio 'nginx-oidc.conf')
    Write-Output 'La aceptación ya tiene configuración privada; se conserva sin cambios.'
    exit 0
}
& "$PSScriptRoot/preparar-certificados.ps1"
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'No se pudieron preparar certificados.' }
New-Item -ItemType Directory -Path $directorio -Force | Out-Null
Copy-Item -LiteralPath nginx.conf -Destination (Join-Path $directorio 'nginx-oidc.conf')
& "$PSScriptRoot/proteger-directorio.ps1" -Directorio $directorio
$configuracion = Get-Content -LiteralPath SolarUY/appsettings.Local.json -Raw | ConvertFrom-Json -AsHashtable
$password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$passwordRoot = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
[System.IO.File]::WriteAllText((Join-Path $directorio 'mysql-password.txt'), $password)
[System.IO.File]::WriteAllText((Join-Path $directorio 'mysql-root-password.txt'), $passwordRoot)
$configuracion['ConnectionStrings']['SolarUy'] = "Server=mysql-pruebas;Port=3306;Database=solaruy_req008;User ID=solaruy;Password=$password;OldGuids=True;SslMode=Disabled;AllowPublicKeyRetrieval=True"
$configuracion | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $directorio 'appsettings.Local.json')
$configuracion['ConnectionStrings']['SolarUy'] = "Server=127.0.0.1;Port=13308;Database=solaruy_req008;User ID=solaruy;Password=$password;OldGuids=True;SslMode=Disabled;AllowPublicKeyRetrieval=True"
$configuracion | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $directorio 'host.json')
@'
SOLARUY_PROYECTO=solaruy-req008-aceptacion
SOLARUY_CONFIG_LOCAL=./.solaruy-acceptance/appsettings.Local.json
SOLARUY_SUBRED=172.30.81.0/24
SOLARUY_PROXY_IP=172.30.81.10
SOLARUY_PUERTO=9443
SOLARUY_DIAGNOSTICO_1=14000
SOLARUY_DIAGNOSTICO_2=14001
'@ | Set-Content -LiteralPath (Join-Path $directorio 'compose.env')
Write-Output 'Aceptación preparada con base solaruy_req008 aislada; la configuración habitual permanece intacta.'
