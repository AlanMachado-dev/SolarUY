param([switch]$ConHerramientas)
$ErrorActionPreference = 'Stop'
$resultado = [ordered]@{ FechaUtc = [DateTimeOffset]::UtcNow.ToString('O'); Modelo = 'gemini-3.5-flash-lite';
    Transporte = 'curl desde app1 de aceptación'; Http = $null; CodigoProveedor = $null;
    Motivos = @(); Categoria = $null; GeneracionValida = $false; ConHerramientas = $ConHerramientas.IsPresent }
try {
    $privada = Get-Content -LiteralPath '.solaruy-private/asistente.private.json' -Raw | ConvertFrom-Json
    $claveApi = [string]$privada.Asistente.ClaveApi
    if ($privada.Asistente.Modelo -ne $resultado.Modelo -or $claveApi -notmatch '^[A-Za-z0-9_.=+/-]+$') { throw [FormatException]::new() }
    $etiqueta = docker inspect --format '{{index .Config.Labels "solaruy.prueba"}}' solaruy-req009-aceptacion-app1-1
    if ($LASTEXITCODE -ne 0 -or $etiqueta.Trim() -ne 'req009') { throw [InvalidOperationException]::new() }
    $solicitud = @{ contents = @(@{ role = 'user'; parts = @(@{ text = 'Responde solamente OK.' }) });
        generationConfig = @{ maxOutputTokens = 512; thinkingConfig = @{ thinkingLevel = 'low' } } }
    if ($ConHerramientas) {
        $catalogo = Get-Content -LiteralPath artifacts/req009/catalogo-mcp.json -Raw | ConvertFrom-Json
        $declaraciones = @($catalogo | ForEach-Object { @{ name = $_.Nombre; description = $_.Descripcion;
            parametersJsonSchema = ($_.EsquemaJson | ConvertFrom-Json -AsHashtable) } })
        $solicitud.tools = @(@{ functionDeclarations = $declaraciones })
        $solicitud.toolConfig = @{ functionCallingConfig = @{ mode = 'AUTO' } }
    }
    $datos = ($solicitud | ConvertTo-Json -Depth 100 -Compress).Replace('\','\\').Replace('"','\"')
    # La clave solo se transmite por stdin al curl del contenedor aislado. No se imprime el cuerpo de respuesta.
    $opciones = @"
url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent"
request = "POST"
header = "Content-Type: application/json"
header = "x-goog-api-key: $claveApi"
data = "$datos"
connect-timeout = 10
max-time = 60
silent
write-out = "\n%{http_code}"
"@
    $salida = ($opciones | docker exec -i solaruy-req009-aceptacion-app1-1 curl --config -) -join "`n"
    $posicion = $salida.LastIndexOf("`n")
    if ($posicion -lt 0) { throw [FormatException]::new() }
    $resultado.Http = [int]$salida.Substring($posicion + 1).Trim()
    $respuesta = $salida.Substring(0, $posicion) | ConvertFrom-Json
    if ($resultado.Http -eq 200) {
        $resultado.GeneracionValida = @($respuesta.candidates[0].content.parts | Where-Object { -not [string]::IsNullOrWhiteSpace($_.text) }).Count -gt 0
    }
    if ($respuesta.error.status -in @('NOT_FOUND','PERMISSION_DENIED','UNAUTHENTICATED','RESOURCE_EXHAUSTED','INVALID_ARGUMENT','UNAVAILABLE','INTERNAL')) {
        $resultado.CodigoProveedor = $respuesta.error.status
    }
    $resultado.Motivos = @($respuesta.error.details | ForEach-Object { $_.reason } | Where-Object { $_ -cmatch '^[A-Z0-9_]{1,80}$' })
    $mensaje = [string]$respuesta.error.message
    $resultado.Categoria = if ($mensaje -match 'API key not valid|API_KEY_INVALID') { 'ClaveInvalida' }
        elseif ($mensaje -match 'not found for API version|not supported for generateContent') { 'ModeloNoDisponibleParaGenerar' }
        elseif ($mensaje -match 'Invalid JSON payload|Unknown name|Invalid value at') { 'ContratoJsonInvalido' }
        elseif ($mensaje -match 'location is not supported') { 'RegionNoAdmitida' }
        elseif ($resultado.CodigoProveedor) { $resultado.CodigoProveedor }
        else { 'SinCategoriaDeError' }
} catch { $resultado.Categoria = 'ErrorConfiguracionOConexion' }
$rutaEvidencia = if ($ConHerramientas) { 'artifacts/req009/gemini-35-flash-lite-contenedor-herramientas.json' } else { 'artifacts/req009/gemini-35-flash-lite-contenedor.json' }
$resultado | ConvertTo-Json -Depth 5 | Set-Content $rutaEvidencia
$resultado | ConvertTo-Json -Depth 5
if (-not $resultado.GeneracionValida) { exit 1 }
