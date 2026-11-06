param([string]$Directorio = '.solaruy-private', [switch]$UsarCertificadoDesarrolloConfiable)
$ErrorActionPreference = 'Stop'
$destino = [System.IO.Path]::GetFullPath((Join-Path $PWD $Directorio))
New-Item -ItemType Directory -Path $destino -Force | Out-Null
& "$PSScriptRoot/proteger-directorio.ps1" -Directorio $destino
$archivos = @('localhost.crt', 'localhost.key', 'claves.pfx', 'claves-password.txt')
$existentes = @($archivos | Where-Object { Test-Path -LiteralPath (Join-Path $destino $_) })
if ($UsarCertificadoDesarrolloConfiable) {
    if (-not $IsWindows -or $existentes.Count -ne 4) {
        throw 'Prepará primero los cuatro archivos privados y ejecutá esta opción en Windows.'
    }
    $confiables = @(Get-ChildItem Cert:/CurrentUser/Root | Select-Object -ExpandProperty Thumbprint)
    $desarrollo = Get-ChildItem Cert:/CurrentUser/My | Where-Object {
        $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) -and ($confiables -contains $_.Thumbprint) -and
        @($_.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.4.1.311.84.1.1' }).Count -gt 0
    } | Sort-Object NotAfter -Descending | Select-Object -First 1
    if ($null -eq $desarrollo) { throw 'No existe un certificado de desarrollo ya confiable para este usuario.' }
    $claveTls = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($desarrollo)
    if ($null -eq $claveTls) { throw 'El certificado de desarrollo no tiene una clave RSA exportable.' }
    try {
        [System.IO.File]::WriteAllText((Join-Path $destino 'localhost.crt'), $desarrollo.ExportCertificatePem())
        [System.IO.File]::WriteAllText((Join-Path $destino 'localhost.key'), $claveTls.ExportPkcs8PrivateKeyPem())
    } finally { $claveTls.Dispose() }
    Write-Output 'Se exportó el certificado de desarrollo ya confiable. No se cambió el almacén de confianza ni las claves de Data Protection.'
    exit 0
}
if ($archivos | Where-Object { Test-Path -LiteralPath (Join-Path $destino $_) }) {
    if ($existentes.Count -ne 4) {
        throw 'Material privado incompleto: usá un directorio nuevo o completá los archivos existentes sin reemplazar claves en uso.'
    }
    Write-Output 'Se conserva el material privado existente.'
    exit 0
}
$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $solicitud = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddDnsName('localhost')
    $solicitud.CertificateExtensions.Add($san.Build())
    $certificado = $solicitud.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(1))
    try {
        [System.IO.File]::WriteAllText((Join-Path $destino 'localhost.crt'), $certificado.ExportCertificatePem())
        [System.IO.File]::WriteAllText((Join-Path $destino 'localhost.key'), $rsa.ExportPkcs8PrivateKeyPem())
    } finally { $certificado.Dispose() }
} finally { $rsa.Dispose() }
$rsaClaves = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $solicitudClaves = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=SolarUY Data Protection', $rsaClaves, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificadoClaves = $solicitudClaves.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(2))
    try {
        $password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        [System.IO.File]::WriteAllBytes((Join-Path $destino 'claves.pfx'), $certificadoClaves.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
        [System.IO.File]::WriteAllText((Join-Path $destino 'claves-password.txt'), $password)
    } finally { $certificadoClaves.Dispose() }
} finally { $rsaClaves.Dispose() }
Write-Output 'Certificados locales preparados. Confiá explícitamente en localhost.crt para usar el navegador sin advertencias.'
