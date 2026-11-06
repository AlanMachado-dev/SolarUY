$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath 'SolarUY.slnx')) { throw 'Ejecutar desde la raíz de SolarUY.' }
$raiz = (Resolve-Path '.').Path
$directorio = Join-Path $raiz '.solaruy-acceptance/req009'
New-Item -ItemType Directory -Force $directorio | Out-Null
& "$PSScriptRoot/../req008/proteger-directorio.ps1" -Directorio $directorio
if (-not (Test-Path -LiteralPath (Join-Path $directorio 'host.private.json'))) {
    $base = Get-Content -LiteralPath '.solaruy-acceptance/appsettings.Local.json' -Raw | ConvertFrom-Json -AsHashtable
    if (-not $base.OpenIdConnect) { throw 'No se encontró la configuración OIDC privada de aceptación.' }
    $password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $passwordRoot = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllText((Join-Path $directorio 'mysql-password.txt'), $password)
    [IO.File]::WriteAllText((Join-Path $directorio 'mysql-root-password.txt'), $passwordRoot)
    $configuracion = @{ OpenIdConnect = $base.OpenIdConnect; ConnectionStrings = @{
        SolarUy = "Server=mysql-pruebas;Port=3306;Database=solaruy_req009;User ID=solaruy;Password=$password;OldGuids=True;SslMode=Disabled;AllowPublicKeyRetrieval=True"
    } }
    $configuracion | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $directorio 'appsettings.Local.json')
    $configuracion.ConnectionStrings.SolarUy = "Server=127.0.0.1;Port=43319;Database=solaruy_req009;User ID=solaruy;Password=$password;OldGuids=True;SslMode=Disabled;AllowPublicKeyRetrieval=True"
    $configuracion | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $directorio 'host.private.json')
}
New-Item -ItemType Directory -Force (Join-Path $directorio 'claves') | Out-Null
$nginxAceptacion = [IO.File]::ReadAllText((Join-Path $raiz 'nginx.conf')).Replace('        location / {', "        location = /_aceptacion/req009/permisos { alias /run/pruebas/permisos-oidc.html; default_type text/html; add_header Cache-Control no-store; }`n        location / {")
[IO.File]::WriteAllText((Join-Path $directorio 'nginx.conf'), $nginxAceptacion)
if (-not (Test-Path -LiteralPath (Join-Path $directorio 'compose.env'))) {
    ('SOLARUY_REQ009_RAIZ=' + $raiz.Replace('\','/')) | Set-Content (Join-Path $directorio 'compose.env')
}
Write-Output 'Aceptación req009 preparada: base solaruy_req009, MySQL 43319, HTTPS 9449, diagnóstico 14319/14320. Asistente deshabilitado para las verificaciones independientes del proveedor.'
