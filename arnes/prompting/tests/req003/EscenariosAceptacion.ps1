param([Parameter(Mandatory = $true)][ValidateSet('Estado','Vacio','Fallo','Restaurar')][string]$Accion)

$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$salida = Join-Path $raiz 'artifacts/req003'
$servidor = Get-Content (Join-Path $salida 'oidc-servidor-proceso.json') -Raw | ConvertFrom-Json
$mysql = Get-Content (Join-Path $salida 'mysql-oidc-proceso.json') -Raw | ConvertFrom-Json
$destino = Get-Content (Join-Path $salida 'destino-aislado.json') -Raw | ConvertFrom-Json
if ($servidor.Origen -cne 'req003-oidc-servidor-propio' -or $mysql.Origen -cne 'req003-mysql-oidc-propio' -or
    $destino.Origen -cne 'req003-consultas-aisladas' -or $mysql.Puerto -ne 13306 -or
    $servidor.Base -cne $destino.Base -or $destino.Base -cnotmatch '^solaruy_req003_aceptacion_[a-f0-9]{12}$') {
    throw 'El destino no es la instancia aislada de req003.'
}
foreach ($registro in @($servidor,$mysql)) {
    $proceso = Get-Process -Id $registro.Pid -ErrorAction Stop
    if ($proceso.StartTime.ToUniversalTime().Ticks -ne ([DateTimeOffset]$registro.InicioUtc).UtcTicks) {
        throw 'Un proceso fue reemplazado; no se modifica el destino.'
    }
}
if ((Resolve-Path -LiteralPath $mysql.Directorio).Path -cne (Join-Path $salida 'mysql-oidc')) {
    throw 'El directorio no corresponde a la instancia propia.'
}

function InvocarSql([string]$Sql) {
    $resultado = & 'C:/wamp64/bin/mysql/mysql9.1.0/bin/mysql.exe' --no-defaults --protocol=tcp `
        --host=127.0.0.1 --port=13306 --user=root "--database=$($destino.Base)" `
        --batch --skip-column-names --default-character-set=utf8mb4 "--execute=$Sql" 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Falló una operación del escenario aislado; no se registran detalles internos.' }
    return $resultado
}

$estadoRuta = Join-Path $salida 'escenario-oidc.json'
$estado = if (Test-Path -LiteralPath $estadoRuta) { Get-Content $estadoRuta -Raw | ConvertFrom-Json } else { $null }
if ($estado -and ($estado.Origen -cne 'req003-escenario-aislado' -or $estado.Base -cne $destino.Base)) {
    throw 'El estado del escenario corresponde a otro destino.'
}

switch ($Accion) {
    'Estado' {
        if ($estado -and $estado.Modo -ne 'Normal') { throw 'Restaurar el escenario antes de consultar el estado normal.' }
        InvocarSql 'SELECT ID,Nombre,Ubicacion FROM ParquesSolares ORDER BY Nombre,ID;'
    }
    { $_ -in 'Vacio','Fallo' } {
        if ($estado -and $estado.Modo -ne 'Normal') { throw 'Ya hay un escenario activo; restaurarlo primero.' }
        $estado = [pscustomobject]@{ Origen = 'req003-escenario-aislado'; Base = $destino.Base; Modo = $Accion }
        $estado | ConvertTo-Json | Set-Content $estadoRuta
        InvocarSql 'RENAME TABLE ParquesSolares TO ParquesSolares_req003_respaldo;' | Out-Null
        if ($Accion -eq 'Vacio') {
            InvocarSql 'CREATE TABLE ParquesSolares LIKE ParquesSolares_req003_respaldo;' | Out-Null
        }
        "Escenario $Accion activado exclusivamente en la base aislada."
    }
    'Restaurar' {
        if (-not $estado -or $estado.Modo -eq 'Normal') { 'El escenario ya es normal.'; break }
        if ($estado.Modo -eq 'Vacio') {
            $cantidad = InvocarSql 'SELECT COUNT(*) FROM ParquesSolares;'
            if ([int]$cantidad -ne 0) { throw 'La tabla vacía recibió registros; no se retira.' }
            InvocarSql 'DROP TABLE ParquesSolares;' | Out-Null
        }
        elseif ($estado.Modo -ne 'Fallo') { throw 'Modo desconocido; no se modifica el destino.' }
        InvocarSql 'RENAME TABLE ParquesSolares_req003_respaldo TO ParquesSolares;' | Out-Null
        $estado.Modo = 'Normal'
        $estado | ConvertTo-Json | Set-Content $estadoRuta
        'Registros de aceptación restaurados.'
    }
}
