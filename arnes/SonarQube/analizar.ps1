# =========================================================================
# CONFIGURACIÓN DEL PROYECTO
# =========================================================================

# 1. Pega aquí el Token que generaste en SonarQube
$SONAR_TOKEN = "pega_tu_token_aqui"

# 2. Inventa un nombre para tu proyecto (sin espacios) o usa el nombre de tu taller
$PROJECT_KEY = "MiProyecto"

# =========================================================================
# NO MODIFICAR ABAJO DE ESTA LÍNEA
# =========================================================================

# =========================================================================
# RUTAS Y VALIDACIONES
# =========================================================================

# 3. Leer la ruta del proyecto desde el primer parámetro (o usar la carpeta actual por defecto)
param([string]$ProjectPath = $PWD.Path)

# Convertir a ruta absoluta y resolverla
try {
    $ABS_PATH = (Resolve-Path $ProjectPath).Path
} catch {
    Write-Host "ERROR: El directorio $ProjectPath no existe." -ForegroundColor Red
    exit 1
}

if ($SONAR_TOKEN -eq "pega_tu_token_aqui") {
    Write-Host "ERROR: Debes modificar el archivo analizar.ps1 y poner tu SONAR_TOKEN." -ForegroundColor Red
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
$imageExists = docker images -q $IMAGE_NAME
if ([string]::IsNullOrWhiteSpace($imageExists)) {
    Write-Host "La imagen de Docker '$IMAGE_NAME' no existe en tu sistema." -ForegroundColor Yellow
    Write-Host "Construyéndola automáticamente por primera vez (esto puede tardar unos minutos)..." -ForegroundColor Yellow
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
    docker build -t $IMAGE_NAME "$ScriptDir/scanner"
}

Write-Host "Iniciando contenedor analizador para $PROJECT_KEY..." -ForegroundColor Cyan
Write-Host "Directorio a analizar: $ABS_PATH" -ForegroundColor Cyan

docker run --rm --network host `
  -v "${ABS_PATH}:/app" `
  -e SONAR_HOST_URL="http://localhost:9000" `
  -e SONAR_TOKEN=$SONAR_TOKEN `
  -e PROJECT_KEY=$PROJECT_KEY `
  mi-taller-scanner
