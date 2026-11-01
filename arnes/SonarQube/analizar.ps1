param([string]$ProjectPath = $PWD.Path)

# =========================================================================
# CONFIGURACIÓN DEL PROYECTO
# =========================================================================

# El token se lee de sonar-token.txt, junto a este script; no se guarda en código.

# 2. Inventa un nombre para tu proyecto (sin espacios) o usa el nombre de tu taller
$PROJECT_KEY = "SolarUY"

# =========================================================================
# NO MODIFICAR ABAJO DE ESTA LÍNEA
# =========================================================================

# =========================================================================
# RUTAS Y VALIDACIONES
# =========================================================================

# Convertir a ruta absoluta y resolverla
try {
    $ABS_PATH = (Resolve-Path $ProjectPath).Path
} catch {
    Write-Host "ERROR: El directorio $ProjectPath no existe." -ForegroundColor Red
    exit 1
}

$TokenFile = Join-Path $PSScriptRoot 'sonar-token.txt'
if (-not (Test-Path -LiteralPath $TokenFile -PathType Leaf)) {
    Write-Host "ERROR: Falta sonar-token.txt junto al script. Copiá sonar-token.example.txt y completá tu token." -ForegroundColor Red
    exit 1
}
try {
    $SONAR_TOKEN = [string](Get-Content -LiteralPath $TokenFile -Raw -Encoding UTF8 -ErrorAction Stop)
    $SONAR_TOKEN = $SONAR_TOKEN.Trim()
} catch {
    Write-Host "ERROR: No se pudo leer sonar-token.txt. Revisá que sea un archivo de texto UTF-8 accesible." -ForegroundColor Red
    exit 1
}
if ([string]::IsNullOrWhiteSpace($SONAR_TOKEN) -or $SONAR_TOKEN -eq 'pega_tu_token_aqui' -or $SONAR_TOKEN -match '\s') {
    Write-Host "ERROR: sonar-token.txt debe contener solo tu token en una línea, sin el marcador de ejemplo." -ForegroundColor Red
    exit 1
}

# =========================================================================
# CONFIGURAR QUALITY GATE ESTRICTO
# =========================================================================
Write-Host "Configurando Quality Gate Estricto en SonarQube..." -ForegroundColor Cyan
$EncodedToken = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${SONAR_TOKEN}:"))
$Headers = @{ Authorization = "Basic $EncodedToken" }
$BaseUrl = "http://localhost:9000/api/qualitygates"
$QGName = "EstrictoTaller"

try { Invoke-RestMethod -Uri "$BaseUrl/create?name=$QGName" -Method Post -Headers $Headers -ErrorAction SilentlyContinue | Out-Null } catch {}
try { Invoke-RestMethod -Uri "$BaseUrl/create_condition?gateName=$QGName&metric=blocker_violations&op=GT&error=0" -Method Post -Headers $Headers -ErrorAction SilentlyContinue | Out-Null } catch {}
try { Invoke-RestMethod -Uri "$BaseUrl/create_condition?gateName=$QGName&metric=security_rating&op=GT&error=1" -Method Post -Headers $Headers -ErrorAction SilentlyContinue | Out-Null } catch {}
try { Invoke-RestMethod -Uri "$BaseUrl/create_condition?gateName=$QGName&metric=reliability_rating&op=GT&error=1" -Method Post -Headers $Headers -ErrorAction SilentlyContinue | Out-Null } catch {}
try { Invoke-RestMethod -Uri "$BaseUrl/set_as_default?name=$QGName" -Method Post -Headers $Headers -ErrorAction SilentlyContinue | Out-Null } catch {}

$IMAGE_NAME = "mi-taller-scanner"
$ScriptDir = $PSScriptRoot
Write-Host "Actualizando la imagen del escáner (Docker reutiliza las capas sin cambios)..." -ForegroundColor Cyan
docker build -t $IMAGE_NAME "$ScriptDir/scanner"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo construir la imagen del escáner." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Iniciando contenedor analizador para $PROJECT_KEY..." -ForegroundColor Cyan
Write-Host "Directorio a analizar: $ABS_PATH" -ForegroundColor Cyan

# Docker hereda el valor desde el entorno del proceso; el token no va en sus argumentos.
$PreviousSonarToken = [Environment]::GetEnvironmentVariable('SONAR_TOKEN', 'Process')
try {
    [Environment]::SetEnvironmentVariable('SONAR_TOKEN', $SONAR_TOKEN, 'Process')
    docker run --rm --network host `
      -v "${ABS_PATH}:/app" `
      -e SONAR_HOST_URL="http://localhost:9000" `
      -e SONAR_TOKEN `
      -e PROJECT_KEY=$PROJECT_KEY `
      $IMAGE_NAME
    $ScannerExitCode = $LASTEXITCODE
} finally {
    [Environment]::SetEnvironmentVariable('SONAR_TOKEN', $PreviousSonarToken, 'Process')
}
exit $ScannerExitCode
