$ErrorActionPreference = 'Stop'
$contenedor = 'solaruy-req009-aceptacion-app1-1'
$modelo = 'gemini-3.5-flash-lite'
$privada = Get-Content -LiteralPath '.solaruy-private/asistente.private.json' -Raw | ConvertFrom-Json
$claveApi = [string]$privada.Asistente.ClaveApi
if ($privada.Asistente.Modelo -ne $modelo -or $claveApi -notmatch '^[A-Za-z0-9_.=+/-]+$') {
    throw 'Configuración privada inválida; no se realizaron solicitudes.'
}
$etiqueta = docker inspect --format '{{index .Config.Labels "solaruy.prueba"}}' $contenedor
if ($LASTEXITCODE -ne 0 -or $etiqueta.Trim() -ne 'req009') { throw 'Contenedor de aceptación no reconocido.' }
$catalogo = @(Get-Content -LiteralPath artifacts/req009/catalogo-mcp.json -Raw | ConvertFrom-Json)
$esperados = @('BuscarElementos','ObtenerDetalle','ConsultarEstadoActual','ConsultarHistorial','ResumirMediciones','CompararMediciones')
if ($catalogo.Count -ne 6 -or @(Compare-Object ($catalogo.Nombre | Sort-Object) ($esperados | Sort-Object)).Count -ne 0) {
    throw 'El catálogo local no corresponde a las seis herramientas de req009.'
}
$declaraciones = @($catalogo | ForEach-Object { [ordered]@{
    name = $_.Nombre; description = $_.Descripcion
    parametersJsonSchema = ($_.EsquemaJson | ConvertFrom-Json -AsHashtable)
} })
$minima = [ordered]@{
    name = 'BuscarElementos'
    description = 'Declaración mínima de búsqueda para diagnóstico; esta prueba no ejecuta herramientas.'
    parametersJsonSchema = [ordered]@{
        type = 'object'
        properties = @{ consulta = @{ type = 'object'; properties = @{ Tipo = @{ type = 'string' } }; additionalProperties = $false } }
        required = @('consulta'); additionalProperties = $false
    }
}
$plantilla = [ordered]@{
    contents = @(@{ role = 'user'; parts = @(@{ text = 'Responde solamente OK.' }) })
    generationConfig = @{ maxOutputTokens = 512; thinkingConfig = @{ thinkingLevel = 'low' } }
} | ConvertTo-Json -Depth 20 -Compress
$inicio = [DateTimeOffset]::UtcNow
$directorio = Join-Path 'artifacts/req009' ('comparacion-gemini-' + $inicio.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Force $directorio | Out-Null

function InvocarVariante([string]$nombre, [object[]]$herramientas) {
    $resultado = [ordered]@{
        Variante = $nombre; InicioUtc = [DateTimeOffset]::UtcNow.ToString('O'); CantidadHerramientas = $herramientas.Count
        BytesSolicitud = 0; Http = $null; SalidaCurl = $null; DuracionSegundos = $null
        CodigoProveedor = $null; Categoria = $null; DemandaAltaProveedor = $false; ErrorEsquema = $false
        TextoGenerado = $false; LlamadasFuncion = 0; MotivoFinalizacion = $null
    }
    try {
        $solicitud = $plantilla | ConvertFrom-Json -AsHashtable
        if ($herramientas.Count -gt 0) { $solicitud.tools = @(@{ functionDeclarations = $herramientas }) }
        # Se omite toolConfig en los tres casos: AUTO es el modo predeterminado para function_declarations.
        $json = $solicitud | ConvertTo-Json -Depth 100 -Compress
        $resultado.BytesSolicitud = [Text.Encoding]::UTF8.GetByteCount($json)
        $datosCurl = $json.Replace('\','\\').Replace('"','\"')
        # Credencial por stdin; respuesta solo en memoria y sin registro de cabeceras/cuerpos.
        $opciones = @"
url = "https://generativelanguage.googleapis.com/v1beta/models/${modelo}:generateContent"
request = "POST"
header = "Content-Type: application/json"
header = "x-goog-api-key: $claveApi"
data = "$datosCurl"
connect-timeout = 10
max-time = 60
silent
write-out = "\n%{http_code}\n%{time_total}"
"@
        $salida = ($opciones | docker exec -i $contenedor curl --config -) -join "`n"
        $resultado.SalidaCurl = $LASTEXITCODE
        $coincidencia = [regex]::Match($salida, '(?s)^(?<cuerpo>.*)\r?\n(?<http>\d{3})\r?\n(?<segundos>[0-9.]+)\s*$')
        if (-not $coincidencia.Success) { throw [FormatException]::new() }
        $resultado.Http = [int]$coincidencia.Groups['http'].Value
        $resultado.DuracionSegundos = [double]::Parse($coincidencia.Groups['segundos'].Value,[Globalization.CultureInfo]::InvariantCulture)
        if ($resultado.Http -eq 0) {
            $resultado.Categoria = if ($resultado.SalidaCurl -eq 28) { 'TiempoAgotado' } else { 'FalloConexion' }
        } else {
            $respuesta = $coincidencia.Groups['cuerpo'].Value | ConvertFrom-Json
            if ($resultado.Http -eq 200) {
                $partes = @($respuesta.candidates | ForEach-Object { $_.content.parts })
                $resultado.TextoGenerado = @($partes | Where-Object { -not $_.thought -and -not [string]::IsNullOrWhiteSpace($_.text) }).Count -gt 0
                $resultado.LlamadasFuncion = @($partes | Where-Object { $null -ne $_.functionCall }).Count
                $fin = [string]$respuesta.candidates[0].finishReason
                if ($fin -cmatch '^[A-Z_]{1,50}$') { $resultado.MotivoFinalizacion = $fin }
                $resultado.Categoria = 'SolicitudAceptada'
            } else {
                $codigo = [string]$respuesta.error.status
                if ($codigo -in @('NOT_FOUND','PERMISSION_DENIED','UNAUTHENTICATED','RESOURCE_EXHAUSTED','INVALID_ARGUMENT','UNAVAILABLE','INTERNAL')) {
                    $resultado.CodigoProveedor = $codigo
                }
                $mensaje = [string]$respuesta.error.message
                $resultado.DemandaAltaProveedor = $resultado.Http -eq 503 -and $mensaje -match 'overload|high demand|out of capacity|no capacity|insufficient capacity'
                $resultado.ErrorEsquema = $resultado.Http -eq 400 -and $mensaje -match 'schema|parametersJsonSchema|functionDeclarations|Invalid JSON payload'
                $resultado.Categoria = if ($resultado.CodigoProveedor) { $resultado.CodigoProveedor } else { 'FalloProveedor' }
            }
        }
    } catch { $resultado.Categoria = 'ErrorConfiguracionOConexion' }
    $resultado.FinUtc = [DateTimeOffset]::UtcNow.ToString('O')
    [pscustomobject]$resultado
}

$resultados = @()
$variantes = @(
    @{ Nombre = 'Sin herramientas'; Herramientas = @() },
    @{ Nombre = 'Una herramienta mínima'; Herramientas = @($minima) },
    @{ Nombre = 'Seis herramientas MCP'; Herramientas = $declaraciones }
)
foreach ($variante in $variantes) {
    $resultado = InvocarVariante -nombre $variante.Nombre -herramientas $variante.Herramientas
    $resultados += $resultado
    $resultado | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $directorio ('caso-' + $resultados.Count + '.json'))
    Write-Output ($resultado | ConvertTo-Json -Depth 5 -Compress)
    if ($resultado.Http -eq 429) { Write-Output 'Comparación detenida por cuota; no se ejecutarán más casos.'; break }
}
[ordered]@{
    InicioUtc = $inicio.ToString('O'); FinUtc = [DateTimeOffset]::UtcNow.ToString('O'); Modelo = $modelo
    MaximoTokensSalida = 512; NivelPensamiento = 'low'; TiempoMaximoPorCasoSegundos = 60
    SeleccionFunciones = 'AUTO predeterminado; toolConfig omitido en todos los casos'
    IntentosPorVariante = 1; ReintentosAutomaticos = 0; HerramientasEjecutadas = 0
    Limitacion = 'Una muestra por variante, consecutivas y no simultáneas; no demuestra causalidad ni estabilidad.'
    Resultados = $resultados
} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $directorio 'resumen.json')
Write-Output ('Evidencia depurada: ' + $directorio)
