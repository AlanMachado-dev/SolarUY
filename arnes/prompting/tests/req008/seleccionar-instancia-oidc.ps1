param([Parameter(Mandatory)][ValidateSet('app1','app2','ambas')][string]$Destino)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path SolarUY.slnx)) { throw 'Ejecutá desde la raíz del repositorio.' }
$entorno = Get-Content -LiteralPath .solaruy-acceptance/compose.env -Raw
if ($entorno -notmatch '(?m)^SOLARUY_PROYECTO=solaruy-req008-aceptacion\s*$') {
    throw 'La selección de instancia requiere el proyecto aislado de aceptación.'
}
$configuracion = Get-Content -LiteralPath nginx.conf -Raw
$ubicacion = '        location / {'
if (-not $configuracion.Contains($ubicacion)) { throw 'No se reconoce la ubicación pública de Nginx.' }
$auxiliares = @'
        # Auxiliares exclusivos del proyecto de aceptación; no leen la cookie autenticada.
        location = /__req008/renovar-afinidad {
            add_header Set-Cookie "SolarUY_Afinidad=; Path=/; Max-Age=0; HttpOnly; Secure; SameSite=Lax" always;
            add_header Cache-Control "no-store" always;
            return 302 https://$http_host/parques-solares;
        }
        location = /__req008/permisos-oidc {
            alias /pruebas/permisos-oidc.html;
            default_type text/html;
            add_header Cache-Control "no-store" always;
        }
'@
$configuracion = $configuracion.Replace($ubicacion, $auxiliares + [Environment]::NewLine + $ubicacion)
if ($Destino -ne 'ambas') {
    $otra = if ($Destino -eq 'app1') { 'app2' } else { 'app1' }
    $linea = "server ${otra}:8080 resolve max_fails=1 fail_timeout=2s;"
    if (-not $configuracion.Contains($linea)) { throw 'No se reconoce la configuración del upstream.' }
    $configuracion = $configuracion.Replace($linea, "server ${otra}:8080 resolve max_fails=1 fail_timeout=2s down;")
}
[System.IO.File]::WriteAllText((Join-Path $PWD '.solaruy-acceptance/nginx-oidc.conf'), $configuracion)
$aceptacion = @('--env-file', '.solaruy-acceptance/compose.env', '-f', 'docker-compose.yml', '-f', 'arnes/prompting/tests/req008/docker-compose.aceptacion.yml')
& docker compose @aceptacion exec nginx nginx -t
if ($LASTEXITCODE -ne 0) { throw 'La configuración temporal de Nginx no es válida.' }
& docker compose @aceptacion exec nginx nginx -s reload
if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar la selección de instancia.' }
Write-Output "Destino de aceptación: $Destino. No se modificaron cookies ni el Nginx habitual."
