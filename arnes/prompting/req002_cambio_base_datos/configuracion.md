# Configurar y encender SolarUY

SolarUY guarda los parques solares en MySQL. La aplicación y las herramientas que preparan sus tablas leen la conexión desde `SolarUY/appsettings.Local.json`. Los datos permanecen en la base aunque cierres el proyecto.

El usuario de MySQL permite acceder a la base. El usuario que inicia sesión en SolarUY se configura por separado y necesita el rol `Administrador` para registrar parques.

## Preparación inicial

Necesitás .NET 10 y MySQL 8.4. Python 3 se utiliza para generar el hash de la contraseña de acceso. Ejecutá los comandos en PowerShell, desde la raíz del repositorio, donde está `SolarUY.slnx`.

### 1. Encender MySQL y preparar la base

Iniciá el servicio MySQL con tu gestor habitual. En el entorno que configuramos, la conexión es `localhost:3306` y la base se llama `solaruy`; ya tiene las tablas necesarias.

Para una instalación nueva, crear primero una base vacía desde el administrador de MySQL:

```sql
CREATE DATABASE solaruy CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin;
```

La cuenta que prepare las tablas debe poder crearlas y modificarlas. Para el uso diario, alcanza con consultar e insertar parques. Si la base ya existe, conservarla y continuar.

### 2. Configurar la conexión

Si todavía no existe el archivo local, copiar la plantilla:

```powershell
if (-not (Test-Path SolarUY/appsettings.Local.json)) {
    Copy-Item SolarUY/appsettings.Local.example.json SolarUY/appsettings.Local.json
}
```

Abrí `SolarUY/appsettings.Local.json` y completá la conexión:

```json
{
  "ConnectionStrings": {
    "SolarUy": "Server=localhost;Port=3306;Database=solaruy;User ID=<usuario-mysql>;Password=<contraseña-mysql>;SslMode=Required;"
  }
}
```

Reemplazá los valores entre `< >` por tus credenciales de MySQL. Si usás otro servidor, puerto o base, cambiá `Server`, `Port` o `Database`. Si el archivo ya está configurado, conservá sus valores y las demás secciones.

Este archivo queda excluido de Git. `SslMode=Required` exige una conexión cifrada; para producción, configurar también la verificación del certificado según la [referencia técnica](referencia_tecnica.md#transporte-cifrado).

### 3. Configurar el acceso a SolarUY

El acceso local con hashes de req002 es un antecedente histórico. La versión actual usa OpenID Connect: preparar la aplicación Auth0, sus roles y la sección privada siguiendo la [guía de req003](../req003_implementacion_openID/configuracion.md). Conservar la conexión MySQL del paso anterior. Ya no se utiliza Autenticacion:Usuarios.

### 4. Preparar el proyecto y sus tablas

```powershell
dotnet restore SolarUY.slnx
dotnet tool restore
dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
```

El último comando aplica las migraciones incluidas en el proyecto. Si ya están aplicadas, deja la base actualizada sin repetir su creación. La aplicación no las aplica al encenderse.

## Encender el proyecto para usarlo

Con MySQL encendido, ejecutar desde la raíz:

```powershell
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

Esperá a que la terminal indique que la aplicación está escuchando. Con el perfil actual, abrí [https://localhost:7178/cuenta/iniciar-sesion](https://localhost:7178/cuenta/iniciar-sesion).

Iniciá sesión con el proveedor externo preparado en el paso 3. Luego entrá en **Registrar parque solar**, completá nombre y ubicación y enviá el formulario. La confirmación muestra el ID asignado y la capacidad inicial de `0 MW`.

Para detener la aplicación, presioná `Ctrl+C` en esa terminal. En los siguientes usos alcanza con encender MySQL y repetir `dotnet run`; la configuración y los datos quedan guardados. Si modificás el archivo local, reiniciá la aplicación.

## Si no inicia

| Problema | Qué revisar |
| --- | --- |
| No conecta a MySQL | Que el servicio esté encendido y coincidan servidor, puerto, base, usuario y contraseña. |
| Faltan las tablas | Ejecutar `dotnet ef database update` del paso 4. |
| Solicita configurar OpenIdConnect | Completar la sección privada según la guía de req003. |
| No permite iniciar sesión o registrar parques | Revisar Auth0, Action de roles y rol exacto `Administrador` según req003. |
| El navegador no confía en el certificado local | Ejecutar `dotnet dev-certs https --trust` y volver a abrir la dirección HTTPS. |

Si una configuración local no parece aplicarse, revisar si existe la variable `ConnectionStrings__SolarUy`: las variables de entorno tienen prioridad sobre el archivo local y los argumentos tienen prioridad sobre ambas.

Los detalles de versiones, permisos, pruebas y diagnóstico están en la [referencia técnica y de aceptación](referencia_tecnica.md).
