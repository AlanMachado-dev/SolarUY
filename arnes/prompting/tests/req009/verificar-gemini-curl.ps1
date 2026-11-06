param([string]$ArchivoPrivado = 'C:\estudio\NET\SolarUY\.solaruy-private\asistente.private.json')
$ErrorActionPreference = 'Stop'
$resultado = [ordered]@{ FechaUtc = [DateTimeOffset]::UtcNow.ToString('O');
    Modelo = 'gemini-3.5-flash-lite'; Transporte = 'curl'; Http = $null; CodigoProveedor = $null; GeneracionValida = $false; SalidaCurl = $null }
try {
    $privada = Get-Content -LiteralPath $ArchivoPrivado -Raw | ConvertFrom-Json
    $claveApi = [string]$privada.Asistente.ClaveApi
    if ($privada.Asistente.Modelo -ne $resultado.Modelo -or $claveApi -notmatch '^[A-Za-z0-9_.=+/-]+$') { throw [FormatException]::new() }
    # La clave pasa por stdin: nunca aparece en argumentos del proceso, archivos de evidencia o salida.
    $opciones = @"
url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent"
request = "POST"
header = "Content-Type: application/json"
header = "x-goog-api-key: $claveApi"
data = "{\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"Responde solamente OK.\"}]}],\"generationConfig\":{\"maxOutputTokens\":512,\"thinkingConfig\":{\"thinkingLevel\":\"low\"}}}"
connect-timeout = 10
max-time = 60
silent
write-out = "\n%{http_code}"
"@
    $salida = ($opciones | curl.exe --config -) -join "`n"
    $resultado.SalidaCurl = $LASTEXITCODE
    $posicion = $salida.LastIndexOf("`n")
    if ($posicion -lt 0) { throw [FormatException]::new() }
    $resultado.Http = [int]$salida.Substring($posicion + 1).Trim()
    if ($resultado.Http -eq 0) {
        $resultado.CodigoProveedor = if ($resultado.SalidaCurl -eq 28) { 'TiempoAgotado' } else { 'FalloConexion' }
    }
    $respuesta = $salida.Substring(0, $posicion) | ConvertFrom-Json
    if ($resultado.Http -eq 200) {
        $resultado.GeneracionValida = @($respuesta.candidates[0].content.parts | Where-Object { -not [string]::IsNullOrWhiteSpace($_.text) }).Count -gt 0
    }
    if ($respuesta.error.status -in @('NOT_FOUND','PERMISSION_DENIED','UNAUTHENTICATED','RESOURCE_EXHAUSTED','INVALID_ARGUMENT','UNAVAILABLE','INTERNAL')) {
        $resultado.CodigoProveedor = $respuesta.error.status
    }
} catch { $resultado.CodigoProveedor = 'ErrorConfiguracionOConexion' }
New-Item -ItemType Directory -Force artifacts/req009 | Out-Null
$resultado | ConvertTo-Json | Set-Content artifacts/req009/gemini-35-flash-lite-curl.json
$resultado | ConvertTo-Json
if (-not $resultado.GeneracionValida) { exit 1 }
