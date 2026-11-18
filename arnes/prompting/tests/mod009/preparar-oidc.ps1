param([switch]$SoloPreparar)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Ejecutar con PowerShell 7 (pwsh).' }
$raizMod009 = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location -LiteralPath $raizMod009
$privadoMod009 = Join-Path $raizMod009 '.solaruy-acceptance/mod009'
if (-not $SoloPreparar) {
    docker version --format '{{.Server.Version}}'
    if ($LASTEXITCODE -ne 0) { throw 'Esta terminal no puede acceder al motor de Docker. No se cambió el despliegue habitual.' }
}
foreach ($archivoMod009 in 'claves.pfx','claves-password.txt','localhost.crt','localhost.key','asistente.private.json') {
    if (-not (Test-Path -LiteralPath (Join-Path '.solaruy-private' $archivoMod009) -PathType Leaf)) {
        throw "Falta el archivo privado requerido: $archivoMod009. No se reemplazan certificados existentes."
    }
}
git check-ignore --quiet .solaruy-acceptance/mod009/host.private.json
if ($LASTEXITCODE -ne 0) { throw 'La configuración privada debe estar excluida de Git.' }
New-Item -ItemType Directory -Path $privadoMod009 -Force | Out-Null
& "$PSScriptRoot/../req008/proteger-directorio.ps1" -Directorio $privadoMod009
$archivosConfiguracionMod009 = @('host.private.json','appsettings.Local.json','mysql-password.txt','mysql-root-password.txt')
$existentesMod009 = @($archivosConfiguracionMod009 | Where-Object { Test-Path -LiteralPath (Join-Path $privadoMod009 $_) })
if ($existentesMod009.Count -ne 0 -and $existentesMod009.Count -ne $archivosConfiguracionMod009.Count) {
    throw 'La configuración aislada está incompleta. No se regeneran contraseñas de una instalación existente.'
}
if ($existentesMod009.Count -eq 0) {
    try {
        $configuracionMod009 = Get-Content -LiteralPath 'SolarUY/appsettings.Local.json' -Raw | ConvertFrom-Json -AsHashtable
        if (-not $configuracionMod009.OpenIdConnect) { throw 'OIDC ausente.' }
    } catch { throw 'No se pudo leer la configuración OIDC local; no se muestran sus valores.' }
    $passwordMod009 = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $passwordRaizMod009 = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllText((Join-Path $privadoMod009 'mysql-password.txt'), $passwordMod009)
    [IO.File]::WriteAllText((Join-Path $privadoMod009 'mysql-root-password.txt'), $passwordRaizMod009)
    $configuracionAisladaMod009 = @{OpenIdConnect=$configuracionMod009.OpenIdConnect; ConnectionStrings=@{
        SolarUy="Server=mysql-pruebas;Port=3306;Database=solaruy_mod009;User ID=solaruy;Password=$passwordMod009;OldGuids=True;SslMode=Disabled;AllowPublicKeyRetrieval=True"
    }}
    $configuracionAisladaMod009 | ConvertTo-Json -Depth 30 | Set-Content -Encoding utf8 (Join-Path $privadoMod009 'appsettings.Local.json')
    $configuracionAisladaMod009.ConnectionStrings.SolarUy = "Server=127.0.0.1;Port=43329;Database=solaruy_mod009;User ID=solaruy;Password=$passwordMod009;OldGuids=True;SslMode=Disabled;AllowPublicKeyRetrieval=True"
    $configuracionAisladaMod009 | ConvertTo-Json -Depth 30 | Set-Content -Encoding utf8 (Join-Path $privadoMod009 'host.private.json')
}

# Selectores exclusivos del proxy de aceptación: cambian afinidad, nunca identidad ni permisos.
$rutasMod009 = @'
    map $cookie_Mod009_Destino $backend_mod009 {
        default backend;
        app1 backend_mod009_app1;
        app2 backend_mod009_app2;
    }
    upstream backend_mod009_app1 { zone mod009_app1 64k; server app1:8080 resolve; }
    upstream backend_mod009_app2 { zone mod009_app2 64k; server app2:8080 resolve; }

'@
$selectoresMod009 = @'
        location = /_mod009 {
            default_type text/html;
            add_header Cache-Control no-store;
            return 200 '<!doctype html><html lang="es"><meta charset="utf-8"><title>Aceptacion mod009</title><h1>Destino de prueba</h1><p>Conserva la sesion OIDC. Usar sin circuitos de monitoreo abiertos.</p><p><a href="/_mod009/app1">Usar app1</a> | <a href="/_mod009/app2">Usar app2</a> | <a href="/_mod009/balanceador">Usar balanceador</a></p></html>';
        }
        location = /_mod009/app1 {
            add_header Set-Cookie "Mod009_Destino=app1; Path=/; HttpOnly; Secure; SameSite=Lax";
            return 303 https://$http_host/;
        }
        location = /_mod009/app2 {
            add_header Set-Cookie "Mod009_Destino=app2; Path=/; HttpOnly; Secure; SameSite=Lax";
            return 303 https://$http_host/;
        }
        location = /_mod009/balanceador {
            add_header Set-Cookie "Mod009_Destino=; Path=/; Max-Age=0; HttpOnly; Secure; SameSite=Lax";
            return 303 https://$http_host/;
        }

'@
$nginxMod009 = [IO.File]::ReadAllText((Join-Path $raizMod009 'nginx.conf'))
$nginxMod009 = $nginxMod009.Replace('    server {', $rutasMod009 + "`n    server {")
$nginxMod009 = $nginxMod009.Replace('        location / {', $selectoresMod009 + "`n        location / {")
$nginxMod009 = $nginxMod009.Replace('proxy_pass http://backend;', 'proxy_pass http://$backend_mod009;')
[IO.File]::WriteAllText((Join-Path $privadoMod009 'nginx.conf'), $nginxMod009)
('SOLARUY_MOD009_RAIZ=' + $raizMod009.Replace('\','/')) | Set-Content -Encoding utf8 (Join-Path $privadoMod009 'compose.env')
$composeMod009 = @('--env-file','.solaruy-acceptance/mod009/compose.env','-f','arnes/prompting/tests/mod009/docker-compose.oidc.yml')
docker compose @composeMod009 config --quiet
if ($LASTEXITCODE -ne 0) { throw 'La configuración Compose aislada no es válida.' }
if ($SoloPreparar) { Write-Output 'Configuración privada y Compose preparados. No se arrancaron contenedores.'; return }

docker compose @composeMod009 build app1
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de la imagen aislada.' }
docker compose @composeMod009 up -d --wait --wait-timeout 180 mysql-pruebas
if ($LASTEXITCODE -ne 0) { throw 'MySQL de aceptación no quedó disponible.' }
dotnet run --project arnes/prompting/tests/mod009/VerificarClaridad.csproj -c Release -- --preparar-oidc
if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación de datos aislados; no se inicia la aceptación.' }
docker compose @composeMod009 up -d --no-build --wait --wait-timeout 180 app1 app2 nginx
if ($LASTEXITCODE -ne 0) { throw 'Las instancias de aceptación no quedaron disponibles.' }
docker compose @composeMod009 exec -T nginx nginx -t
if ($LASTEXITCODE -ne 0) { throw 'La configuración del proxy de aceptación no es válida.' }
docker compose @composeMod009 exec -T nginx nginx -s reload
if ($LASTEXITCODE -ne 0) { throw 'No se pudo recargar el proxy de aceptación.' }
docker compose @composeMod009 ps
Write-Output 'Aceptación lista en https://localhost:9449. Proyecto solaruy-mod009-aceptacion; el despliegue habitual no se modificó.'
