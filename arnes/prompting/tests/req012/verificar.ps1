param([switch]$Oidc)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path 'artifacts/req012' -Force | Out-Null
$env:MYSQL_ROOT_PASSWORD = [Guid]::NewGuid().ToString('N')
$nombre = 'solaruy-req012-mysql'
if (docker ps -a --filter "name=^/$nombre$" --format '{{.Names}}') { throw 'Ya existe el contenedor aislado de req012.' }
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
docker run --detach --rm --name $nombre --publish 127.0.0.1:39312:3306 --env MYSQL_ROOT_PASSWORD mysql:8.4 | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
try {
    $env:SOLARUY_REQ012_TEST_MYSQL = "Server=127.0.0.1;Port=39312;Database=solaruy_req012_validacion;User ID=root;Password=$env:MYSQL_ROOT_PASSWORD;SslMode=Preferred"
    $listo = $false
    for ($intento = 0; $intento -lt 45; $intento++) {
        docker exec $nombre mysqladmin ping -h 127.0.0.1 *> $null
        if ($LASTEXITCODE -eq 0) { $listo = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $listo) { throw 'El contenedor MySQL aislado no respondió' }
    dotnet run --project arnes/prompting/tests/req012/VerificarMantenimiento.csproj *> artifacts/req012/mysql.txt
    $resultado = $LASTEXITCODE
    Get-Content artifacts/req012/mysql.txt | Select-String -Pattern '^OK|^FALLO|^Verificación|error CS|error MSB'
    if ($resultado -ne 0) { exit $resultado }
    $dll = (Resolve-Path 'arnes/prompting/tests/req012/bin/Debug/net10.0/VerificarMantenimiento.dll').Path
    if ($Oidc) {
        Write-Output 'Aceptación OIDC: https://localhost:9449/cuenta/iniciar-sesion'
        dotnet $dll oidc
        exit $LASTEXITCODE
    }
    $ejecutables = @()
    try {
        foreach ($puerto in 39313,39314) {
            $ejecutables += Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($dll,'web',$puerto) -WindowStyle Hidden -PassThru -RedirectStandardOutput "artifacts/req012/web-$puerto.txt" -RedirectStandardError "artifacts/req012/web-$puerto-err.txt"
        }
        dotnet $dll reinicio programar *> artifacts/req012/reinicio.txt
        if ($LASTEXITCODE -ne 0) { Get-Content artifacts/req012/reinicio.txt; exit 1 }
        $ejecutables | Where-Object { -not $_.HasExited } | Stop-Process
        $ejecutables | Wait-Process -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 9
        $ejecutables = @()
        foreach ($puerto in 39313,39314) {
            $ejecutables += Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($dll,'web',$puerto) -WindowStyle Hidden -PassThru -RedirectStandardOutput "artifacts/req012/web-$puerto.txt" -RedirectStandardError "artifacts/req012/web-$puerto-err.txt"
        }
        Start-Sleep -Seconds 3
        dotnet $dll reinicio verificar *>> artifacts/req012/reinicio.txt
        if ($LASTEXITCODE -ne 0) { Get-Content artifacts/req012/reinicio.txt; exit 1 }
        Get-Content artifacts/req012/reinicio.txt
        node arnes/prompting/tests/req012/verificar-navegador.cjs *> artifacts/req012/navegador.txt
        $codigo = $LASTEXITCODE
        Get-Content artifacts/req012/navegador.txt
        exit $codigo
    }
    finally {
        $ejecutables | Where-Object { -not $_.HasExited } | Stop-Process
    }
}
finally {
    docker stop $nombre *> $null
}
