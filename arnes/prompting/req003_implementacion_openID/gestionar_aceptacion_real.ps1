param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preparar', 'Iniciar', 'Detener', 'Estado', 'ActivarFalloMySql', 'DesactivarFalloMySql', 'Limpiar')]
    [string]$Accion,
    [ValidateRange(1024, 65535)]
    [int]$Puerto = 7178,
    [switch]$ProveedorNoDisponible
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$salida = Join-Path $raiz 'artifacts/req003'
$estadoRuta = Join-Path $salida 'entorno-real.json'
$procesoRuta = Join-Path $salida 'proceso-real.json'
New-Item -ItemType Directory -Path $salida -Force | Out-Null
$configuracion = Get-Content (Join-Path $raiz 'SolarUY/appsettings.Local.json') -Raw | ConvertFrom-Json
$opciones = [System.Data.Common.DbConnectionStringBuilder]::new()
$opciones.set_ConnectionString($configuracion.ConnectionStrings.SolarUy)
if ($env:ConnectionStrings__SolarUy) { $opciones.set_ConnectionString($env:ConnectionStrings__SolarUy) }
$baseHabitual = [string]$opciones['database']

function InvocarMySql([string]$Sql, [string]$Base = '') {
    $modoSsl = switch ([string]$opciones['sslmode']) {
        'Required' { 'REQUIRED' }
        'VerifyFull' { 'VERIFY_IDENTITY' }
        'VerifyCA' { 'VERIFY_CA' }
        default { throw 'El arnés requiere TLS configurado en la conexión MySQL.' }
    }
    $argumentos = @("--host=$($opciones['server'])", "--port=$($opciones['port'])",
        "--user=$($opciones['user id'])", "--ssl-mode=$modoSsl", '--connect-timeout=5',
        '--default-character-set=utf8mb4', '--batch', '--skip-column-names', "--execute=$Sql")
    if ($Base) { $argumentos += "--database=$Base" }
    foreach ($par in @(@('sslca', '--ssl-ca'), @('sslcert', '--ssl-cert'), @('sslkey', '--ssl-key'))) {
        if ($opciones.ContainsKey($par[0])) { $argumentos += "$($par[1])=$($opciones[$par[0]])" }
    }
    $passwordAnterior = $env:MYSQL_PWD
    $env:MYSQL_PWD = [string]$opciones['password']
    try {
        $resultado = & (Get-Command mysql).Source @argumentos 2>&1
        if ($LASTEXITCODE -ne 0) {
            $codigo = [regex]::Match(($resultado -join ' '), 'ERROR (\d+)').Groups[1].Value
            throw "Falló MySQL. Código: $codigo. No se registran detalles de conexión."
        }
        return $resultado
    }
    finally { $env:MYSQL_PWD = $passwordAnterior }
}

function LeerEstado {
    if (-not (Test-Path -LiteralPath $estadoRuta)) { throw 'Primero ejecutar Preparar.' }
    $estado = Get-Content -LiteralPath $estadoRuta -Raw | ConvertFrom-Json
    if ($estado.Origen -cne 'req003-aceptacion-aislada' -or
        $estado.Base -cnotmatch '^solaruy_req003_aceptacion_[a-f0-9]{12}$' -or
        $estado.Base -ceq $baseHabitual) { throw 'El destino no es una base aislada de esta ejecución.' }
    return $estado
}

function DetenerServidorPropio {
    if (-not (Test-Path -LiteralPath $procesoRuta)) { return }
    $registro = Get-Content -LiteralPath $procesoRuta -Raw | ConvertFrom-Json
    if ($registro.Origen -cne 'req003-servidor-propio') { throw 'Proceso ajeno al arnés.' }
    $proceso = Get-Process -Id $registro.Pid -ErrorAction SilentlyContinue
    if ($proceso) {
        if ($proceso.StartTime.ToUniversalTime().Ticks -ne ([DateTimeOffset]$registro.InicioUtc).UtcTicks) {
            throw 'El PID fue reutilizado; no se detiene el proceso.'
        }
        Stop-Process -InputObject $proceso
        $proceso.WaitForExit(10000) | Out-Null
    }
}

switch ($Accion) {
    'Preparar' {
        if (Test-Path -LiteralPath $estadoRuta) { throw 'Ya hay un destino registrado. Revisarlo antes de preparar otro.' }
        $base = 'solaruy_req003_aceptacion_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
        $existe = InvocarMySql "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '$base';"
        if ([int]$existe -ne 0) { throw 'La base generada ya existe; no se utiliza.' }
        InvocarMySql "CREATE DATABASE $base CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin;" | Out-Null
        $estado = [pscustomobject]@{ Origen = 'req003-aceptacion-aislada'; Base = $base;
            CreadaUtc = [DateTimeOffset]::UtcNow.ToString('o'); Migrada = $false }
        $estado | ConvertTo-Json | Set-Content -LiteralPath $estadoRuta
        $opciones['database'] = $base
        $conexionAnterior = $env:ConnectionStrings__SolarUy
        $env:ConnectionStrings__SolarUy = $opciones.get_ConnectionString()
        try {
            Push-Location $raiz
            dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj --no-build *> (Join-Path $salida 'migracion-aceptacion.log')
            if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar la migración existente en el destino aislado.' }
            $estado.Migrada = $true
            $estado | ConvertTo-Json | Set-Content -LiteralPath $estadoRuta
        }
        finally { Pop-Location; $env:ConnectionStrings__SolarUy = $conexionAnterior }
        $estado | ConvertTo-Json
    }
    'Detener' { DetenerServidorPropio; 'Servidor propio detenido.' }
    'Iniciar' {
        $estado = LeerEstado
        if (-not $estado.Migrada) { throw 'La base aislada no está migrada.' }
        DetenerServidorPropio
        $escuchas = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
        if ($escuchas.Port -contains $Puerto) { throw 'El puerto solicitado pertenece a otro proceso.' }
        $directorio = Join-Path $salida 'configuracion-servidor-real'
        New-Item -ItemType Directory -Path $directorio -Force | Out-Null
        '{}' | Set-Content (Join-Path $directorio 'appsettings.json')
        $variables = @{}
        foreach ($nombre in @('Authority', 'ClientId', 'ClientSecret', 'RolesClaimType')) {
            $clave = "OpenIdConnect__$nombre"
            $variables[$clave] = if (Test-Path "Env:$clave") { (Get-Item "Env:$clave").Value } else { $null }
            if (-not (Test-Path "Env:$clave")) { Set-Item "Env:$clave" $configuracion.OpenIdConnect.$nombre }
        }
        if ($ProveedorNoDisponible) { $env:OpenIdConnect__Authority = 'https://proveedor-no-disponible.invalid/' }
        $opciones['database'] = $estado.Base
        foreach ($clave in @('ConnectionStrings__SolarUy', 'ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT',
            'Logging__LogLevel__SolarUY.Infrastructure.Seguridad.SesionCookieEventsHandler')) {
            $variables[$clave] = if (Test-Path "Env:$clave") { (Get-Item "Env:$clave").Value } else { $null }
        }
        $env:ConnectionStrings__SolarUy = $opciones.get_ConnectionString()
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:DOTNET_ENVIRONMENT = 'Development'
        Set-Item 'Env:Logging__LogLevel__SolarUY.Infrastructure.Seguridad.SesionCookieEventsHandler' 'Debug'
        try {
            $dll = Join-Path $raiz 'SolarUY/bin/Debug/net10.0/SolarUY.dll'
            $inicio = @{
                FilePath = 'dotnet'
                ArgumentList = @("`"$dll`"", '--contentRoot', "`"$directorio`"", '--urls', "https://localhost:$Puerto")
                WindowStyle = 'Hidden'
                PassThru = $true
                RedirectStandardOutput = Join-Path $salida 'servidor-real-salida.log'
                RedirectStandardError = Join-Path $salida 'servidor-real-error.log'
            }
            $proceso = Start-Process @inicio
            [pscustomobject]@{ Pid = $proceso.Id; InicioUtc = $proceso.StartTime.ToUniversalTime().ToString('o');
                Origen = 'req003-servidor-propio'; Base = $estado.Base; Puerto = $Puerto } | ConvertTo-Json | Set-Content $procesoRuta
        }
        finally {
            foreach ($par in $variables.GetEnumerator()) {
                if ($null -eq $par.Value) { Remove-Item "Env:$($par.Key)" -ErrorAction SilentlyContinue }
                else { Set-Item "Env:$($par.Key)" $par.Value }
            }
        }
        'Servidor iniciado en HTTPS con MySQL aislado y sin Autenticacion:Usuarios.'
    }
    'Estado' {
        $estado = LeerEstado
        $cantidad = InvocarMySql 'SELECT COUNT(*) FROM ParquesSolares;' $estado.Base
        $filas = InvocarMySql 'SELECT ID, Nombre, Ubicacion FROM ParquesSolares ORDER BY Nombre;' $estado.Base
        [pscustomobject]@{ Base = $estado.Base; Cantidad = [int]$cantidad; Registros = @($filas) } | ConvertTo-Json
    }
    'ActivarFalloMySql' {
        $estado = LeerEstado
        InvocarMySql "CREATE TRIGGER req003_rechazo BEFORE INSERT ON ParquesSolares FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req003 fallo de aceptación';" $estado.Base | Out-Null
        'Fallo de escritura activado únicamente en la base aislada.'
    }
    'DesactivarFalloMySql' {
        $estado = LeerEstado
        InvocarMySql 'DROP TRIGGER IF EXISTS req003_rechazo;' $estado.Base | Out-Null
        'Fallo de escritura retirado de la base aislada.'
    }
    'Limpiar' {
        $estado = LeerEstado
        DetenerServidorPropio
        InvocarMySql "DROP DATABASE $($estado.Base);" | Out-Null
        $estado | Add-Member -NotePropertyName EliminadaUtc -NotePropertyValue ([DateTimeOffset]::UtcNow.ToString('o')) -Force
        $estado | ConvertTo-Json | Set-Content -LiteralPath $estadoRuta
        'Base aislada de esta ejecución eliminada; base habitual conservada.'
    }
}
