param(
    [string]$ArchivoPrivado = 'C:\estudio\NET\SolarUY\.solaruy-private\asistente.private.json'
)
$ErrorActionPreference = 'Stop'
$resultado = [ordered]@{
    FechaUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Modelo = 'gemini-3.5-flash-lite'
    ConfiguracionValida = $false
    ConsultaModeloHttp = $null
    GeneracionHttp = $null
    CodigoProveedor = $null
    GeneracionValida = $false
    Fase = 'Configuracion'
    TipoFallo = $null
    NivelGratuito = 'Pendiente de confirmar en la cuenta; una clave y una respuesta API no acreditan el nivel'
}
$cliente = [System.Net.Http.HttpClient]::new()
$cliente.Timeout = [TimeSpan]::FromSeconds(60)
try {
    $configuracion = Get-Content -LiteralPath $ArchivoPrivado -Raw | ConvertFrom-Json
    if ($configuracion.Asistente.Modelo -ne $resultado.Modelo -or
        -not $configuracion.Asistente.Habilitado -or
        [string]::IsNullOrWhiteSpace($configuracion.Asistente.ClaveApi)) {
        throw [FormatException]::new()
    }
    $resultado.ConfiguracionValida = $true
    $cliente.DefaultRequestHeaders.Add('x-goog-api-key', [string]$configuracion.Asistente.ClaveApi)
    $resultado.Fase = 'ConsultaModelo'
    $consulta = $cliente.GetAsync('https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite').GetAwaiter().GetResult()
    $resultado.ConsultaModeloHttp = [int]$consulta.StatusCode
    if ($consulta.IsSuccessStatusCode) {
        $resultado.Fase = 'Generacion'
        # Una única generación mínima; no envía datos operativos ni conversaciones.
        $cuerpo = @{ contents = @(@{ role = 'user'; parts = @(@{ text = 'Responde solamente OK.' }) });
            generationConfig = @{ maxOutputTokens = 512; thinkingConfig = @{ thinkingLevel = 'low' } } } | ConvertTo-Json -Depth 8 -Compress
        $contenido = [System.Net.Http.StringContent]::new($cuerpo, [System.Text.Encoding]::UTF8, 'application/json')
        $generacion = $cliente.PostAsync('https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent', $contenido).GetAwaiter().GetResult()
        $resultado.GeneracionHttp = [int]$generacion.StatusCode
        $respuesta = $generacion.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if ($generacion.IsSuccessStatusCode) {
            $resultado.GeneracionValida = @($respuesta.candidates).Count -gt 0 -and
                @($respuesta.candidates[0].content.parts | Where-Object { -not [string]::IsNullOrWhiteSpace($_.text) }).Count -gt 0
        }
    } else {
        $respuesta = $consulta.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    }
    if ($respuesta.error.status -in @('NOT_FOUND','PERMISSION_DENIED','UNAUTHENTICATED','RESOURCE_EXHAUSTED','INVALID_ARGUMENT','UNAVAILABLE','INTERNAL')) {
        $resultado.CodigoProveedor = $respuesta.error.status
    }
} catch {
    # Nunca escribir excepciones/cuerpos/cabeceras: podrían contener credenciales o datos privados.
    $resultado.CodigoProveedor = 'ErrorConfiguracionOConexion'
    $resultado.TipoFallo = $_.Exception.GetBaseException().GetType().Name
} finally {
    $cliente.Dispose()
}
New-Item -ItemType Directory -Force artifacts/req009 | Out-Null
$resultado | ConvertTo-Json | Set-Content -LiteralPath artifacts/req009/gemini-35-flash-lite.json
$resultado | ConvertTo-Json
if (-not $resultado.GeneracionValida) { exit 1 }
