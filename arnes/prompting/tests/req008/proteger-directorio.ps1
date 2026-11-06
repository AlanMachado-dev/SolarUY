param([Parameter(Mandatory)][string]$Directorio)
$ErrorActionPreference = 'Stop'
if ($IsWindows) {
    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls $Directorio /grant:r ('*{0}:(OI)(CI)F' -f $sid) /inheritance:r | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron restringir los permisos del directorio privado.' }
}
else {
    & chmod 700 $Directorio
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron restringir los permisos del directorio privado.' }
}
