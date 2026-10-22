# req001 — Configuración del alta de parque solar

> Identificador: **req001 (alta de parque solar)**. La documentación está en requerimientos/req001_alta_parque_solar y las herramientas en tests/req001. Ver [estructura actual](../../README.md).

**Guía histórica:** describe SQLite y autenticación local de la primera entrega. No ejecutar sus comandos como configuración de la versión vigente. Usar la [guía MySQL de mod001](../../modificaciones/mod001_cambio_base_datos/configuracion.md) y la [guía OIDC de req002](../req002_implementacion_openID/configuracion.md).

Requiere .NET 10 y Python 3 para el generador auxiliar de hashes. Toda la funcionalidad se ejecuta en el servidor. No se registran usuarios desde el formulario de alta.

## Base SQLite

Desde PowerShell, en la raíz del repositorio:

```powershell
$env:ConnectionStrings__SolarUy = 'Data Source=C:/estudio/NET/SolarUY/solaruy-desarrollo.db'
dotnet tool restore
dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
```

La base no se migra automáticamente al arrancar; aplicar las migraciones antes de usar el alta. Para otros entornos, usar su ruta de conexión externa y el mismo procedimiento de migración. Los archivos SQLite locales se excluyen de Git.

## Identidades provisionadas por el servidor

Generar dos hashes, introduciendo contraseñas propias cuando se soliciten:

```powershell
python arnes/prompting/tests/req001/generar_hash.py
```

Configurar externamente las siguientes claves (los valores entre ángulos son indicaciones, no credenciales):

```powershell
$env:Autenticacion__Usuarios__0__Nombre = 'administrador-desarrollo'
$env:Autenticacion__Usuarios__0__PasswordHash = '<primer hash generado>'
$env:Autenticacion__Usuarios__0__Roles__0 = 'Administrador'
$env:Autenticacion__Usuarios__1__Nombre = 'usuario-desarrollo'
$env:Autenticacion__Usuarios__1__PasswordHash = '<segundo hash generado>'
$env:Autenticacion__Usuarios__1__Roles__0 = 'Usuario'
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

Las credenciales se validan mediante ASP.NET Core Identity y se emite una cookie de sesión por 30 minutos. Los roles se leen de esta configuración y se comparan exactamente; el navegador no puede asignarlos. Se exige al menos una identidad configurada y nombres únicos al arrancar. No se incluyen cuentas ni contraseñas por defecto. En producción, usar configuración protegida y HTTPS; la cookie exige transporte seguro fuera de desarrollo.

Abrir `/cuenta/iniciar-sesion` y usar la identidad administradora para acceder a `/parques-solares/nuevo`. La segunda identidad permite comprobar acceso denegado. Inicio y cierre de sesión requieren token antifalsificación; el inicio tiene un límite de 10 intentos por minuto y dirección IP.

## Controles

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Las verificaciones HTTP de aceptación usarán identidades efímeras independientes de estas cuentas y una base aislada, sin modificar bases existentes.

Después de compilar, ejecutar con Python nativo de Windows:

```powershell
python arnes/prompting/tests/req001/verificar_aceptacion.py
```

El arnés genera su propia base, aplica la migración, inicia un servidor oculto en `127.0.0.1:5284`, verifica F01–F22 y lo detiene al terminar. Ese puerto debe estar libre. Registra resultados y logs en `artifacts/req001`, excluido de Git. La opción `--mantener-servidor` lo deja activo solo si todas las verificaciones pasan, para completar F23 con teclado; el PID queda en `artifacts/req001/ultima-aceptacion.json`. Las credenciales efímeras de ese servidor se guardan únicamente en su directorio local de evidencia.
