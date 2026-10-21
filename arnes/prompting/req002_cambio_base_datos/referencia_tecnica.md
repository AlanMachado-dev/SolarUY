# Referencia técnica y aceptación de MySQL

Para configurar y encender la aplicación, seguir la [guía de uso](configuracion.md). Este documento conserva los detalles técnicos y las instrucciones de aceptación.

Ejecutar los comandos desde la raíz del repositorio. La aplicación y las herramientas EF utilizan la misma configuración de Infrastructure. No se aplican migraciones al arrancar.

## Versiones y esquema

Verificado en este entorno: SDK .NET 10.0.400, runtime 10.0.11, EF Core / Relational / Design 10.0.12, herramienta `dotnet-ef` 10.0.12, proveedor oficial `MySql.EntityFrameworkCore` 10.0.9 y dependencia transitiva `MySql.Data` 26.7.0. Servidor y cliente MySQL 8.4.3. Python 3.13 ejecuta el arnés con su biblioteca estándar.

El esquema requiere InnoDB y `utf8mb4`. La columna `ClaveNombre` tiene colación `utf8mb4_0900_bin` (`NO PAD`) e índice único completo `UX_ParquesSolares_ClaveNombre`. Los nombres se normalizan en Domain y admiten como máximo 100 valores escalares Unicode, tanto en el texto visible como en su clave. Acentos y espacios interiores siguen siendo significativos. Ubicación se guarda como `longtext`; ID como `char(36)` y la capacidad inicial derivada no se almacena.

Se mantiene el proveedor elegido en [specs.md](specs.md). Su generación de SQL omite charset y colación: la migración inicial los establece explícitamente antes de crear el índice. Las opciones compartidas fijan `OldGuids=true` para que el lector entregue el `char(36)` como texto a la conversión reversible del modelo; esto **no** cambia el almacenamiento a binario. El diseñador y snapshot generados requieren el import de extensiones del proveedor. No mezclar esta configuración con Pomelo.

La restauración advierte NU1903 por `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.12, ya presente antes del cambio. Este requerimiento conserva las versiones de Microsoft acordadas en las especificaciones.

## Base y permisos

Preparar una base **nueva, vacía y dedicada**, en MySQL real, local o remoto. No usar MariaDB para validar esta aceptación. Un administrador puede crearla, reemplazando los marcadores y sin ejecutar el ejemplo literalmente:

```sql
CREATE DATABASE `<base>` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin;
```

Preparar las cuentas mediante un canal administrativo protegido. La cuenta de ejecución necesita `SELECT` e `INSERT` en `ParquesSolares`; no necesita crear bases, administrar usuarios ni modificar esquemas. La cuenta que aplica migraciones requiere además crear y alterar tablas e índices y leer/escribir `__EFMigrationsHistory`. Otorgar permisos adicionales para futuras migraciones solo cuando sus operaciones los requieran. Las cuentas pueden ser diferentes: suministrar la conexión de migraciones externamente al comando EF.

Verificar con el cliente MySQL, sin poner contraseñas en argumentos (usar `--password` para solicitarla):

```powershell
mysql --host=<host> --port=<puerto> --user=<usuario> --password --ssl-mode=VERIFY_IDENTITY --ssl-ca=<ruta-ca.pem> --database=<base>
```

```sql
SELECT VERSION(), @@default_storage_engine, @@character_set_server;
SELECT COLLATION_NAME, PAD_ATTRIBUTE
FROM information_schema.COLLATIONS WHERE COLLATION_NAME = 'utf8mb4_0900_bin';
SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE();
```

Antes de la primera migración, la última consulta debe estar vacía. Si hay tablas o datos, detener la transición y definir su conservación; no reinicializar la base.

## Archivo local y prioridades

```powershell
if (-not (Test-Path SolarUY/appsettings.Local.json)) {
    Copy-Item SolarUY/appsettings.Local.example.json SolarUY/appsettings.Local.json
}
```

Editar `SolarUY/appsettings.Local.json`, reemplazando todos los marcadores de la plantilla:

```json
{
  "ConnectionStrings": {
    "SolarUy": "Server=<host>;Port=<puerto>;Database=<base>;User ID=<usuario>;Password=<contraseña>;SslMode=Required;"
  }
}
```

`Server` acepta un host remoto. Ajustar `Port`, `Database`, `User ID`, `Password` y las opciones TLS al entorno. No requiere Docker. El archivo local ya está excluido expresamente de Git; comprobarlo con `git check-ignore SolarUY/appsettings.Local.json`. La plantilla contiene solo marcadores y es versionable. No copiar la conexión a `SolarUY.Client`, `wwwroot`, documentos ni evidencias.

La prioridad, de menor a mayor, es:

1. `appsettings.json`.
2. `appsettings.{Environment}.json`.
3. `appsettings.Local.json`.
4. Variables de entorno, incluida `ConnectionStrings__SolarUy`.
5. Argumentos, por ejemplo `--ConnectionStrings:SolarUy`.

El archivo local es opcional si otra fuente aporta la conexión. La fábrica busca el proyecto servidor desde la raíz o desde el directorio del ensamblado. La selección del entorno coincide con ASP.NET Core: argumentos `--environment`, luego `DOTNET_ENVIRONMENT`, luego `ASPNETCORE_ENVIRONMENT`, y finalmente `Production`. `dotnet run` puede establecer el entorno mediante el perfil de lanzamiento; para comparaciones controladas usar `--no-launch-profile` y el mismo entorno en ambos comandos.

Para sobrescribir la conexión, definir `ConnectionStrings__SolarUy` en un gestor de secretos, en la configuración protegida del proceso o en la sesión que ejecutará los comandos. No pegar contraseñas reales en historial de terminal ni argumentos de procesos de uso habitual. El arnés verifica la prioridad de argumentos con conexiones efímeras sin escribir esos argumentos en sus evidencias.

La validación al configurar servicios rechaza conexión ausente, vacía o sintácticamente inválida. No abre una conexión física ni demuestra credenciales o disponibilidad. No existe conexión alternativa ni base por defecto.

## Transporte cifrado

`SslMode=Required` exige cifrado y sirve para un entorno local controlado; no verifica por sí solo la identidad del servidor. En producción usar `SslMode=VerifyFull` y una autoridad confiable, por ejemplo `SslCa=<ruta-ca.pem>`, con un certificado cuyo nombre coincida con `Server`. `VerifyCA` verifica la autoridad pero no el nombre. Ajustar certificados de cliente cuando la instancia los exija. No resolver errores de certificados deshabilitando su validación de forma general. La opción del cliente MySQL equivalente a verificar la identidad es `--ssl-mode=VERIFY_IDENTITY`.

## Restaurar, migrar y arrancar

```powershell
dotnet restore SolarUY.slnx
dotnet tool restore
dotnet build SolarUY.slnx
dotnet ef migrations script --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj --output artifacts/req002/migracion.sql
dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
```

Revisar el SQL antes de actualizar. La migración `InicialMySql` ya está incluida: **no volver a generarla**. Ejecutar `database update` otra vez debe indicar que el esquema está actualizado y conservar los registros. No usar `EnsureCreated`, actualización automática al arrancar, `database update 0`, ni borrar archivos de bases anteriores.

El acceso con usuarios locales y hashes descrito originalmente en req002 es histórico. La configuración vigente y el administrador se preparan mediante [OpenID Connect en req003](../req003_implementacion_openID/configuracion.md), conservando esta conexión MySQL. Luego:

```powershell
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

Entrar en `/cuenta/iniciar-sesion` y registrar un parque desde `/parques-solares/nuevo`. Comprobar la confirmación con ID, nombre, ubicación y `0 MW`. Consultar mediante parámetros en el cliente MySQL el mismo ID y su `ClaveNombre`; el arnés comprueba además `ExisteNombreAsync` con un contexto nuevo y después de reiniciar el servidor. Un nombre equivalente debe devolver el error de duplicado, sin otro registro.

## Diagnóstico

| Síntoma | Comprobación |
| --- | --- |
| Rechazo al configurar servicios | Revisar presencia, sintaxis, servidor, base, usuario y prioridad de las fuentes. El diagnóstico omite los valores de conexión. |
| Conexión rechazada o timeout | Verificar servicio MySQL, host, puerto, firewall y conectividad desde el equipo que ejecuta SolarUY. |
| Error de autenticación (1045) | Revisar usuario, contraseña y origen autorizado de la cuenta en MySQL. |
| Base inexistente (1049) | Crear el destino dedicado correcto; las migraciones no sustituyen la preparación administrativa. |
| Permisos insuficientes (1044/1142/1143) | Diferenciar cuenta de ejecución, migraciones y aceptación. No convertir el fallo en nombre duplicado. |
| Tabla inexistente (1146) | Aplicar la migración al mismo destino y entorno que consume el servidor. |
| Error TLS | Verificar CA, nombre del certificado, rutas y políticas de transporte de la instancia. |
| Mensaje genérico en el formulario | Revisar disponibilidad y permisos con el cliente; el servidor registra el tipo de fallo sin volcar SQL, valores ni excepciones completas. |

## Arnés MySQL y evidencias

El arnés nuevo es independiente del script histórico de `req001`; no lo modifica ni lo importa. Requiere .NET 10, herramientas EF restauradas, Python 3.13 y acceso a MySQL 8.4. No necesita instalar un conector Python: crea un ejecutable funcional auxiliar dentro de `artifacts/req002` que referencia el servidor. No incorpora proyectos de capas, pruebas unitarias externas al dominio ni endpoints de diagnóstico.

Configurar externamente:

| Opción | Uso |
| --- | --- |
| `SOLARUY_ACEPTACION_BASE` | Nombre **nuevo** con formato `solaruy_req002_aceptacion_` seguido por 12 caracteres hexadecimales. |
| `SOLARUY_ACEPTACION_CONNECTION` | Conexión de aceptación cuyo `Database` coincida exactamente con esa opción. La cuenta necesita crear/eliminar ese esquema y operar tablas, índices y triggers dentro de él. |
| `SOLARUY_ACEPTACION_READONLY_CONNECTION` | Cuenta existente de solo lectura, con acceso `SELECT` a los esquemas efímeros de aceptación. El arnés selecciona el esquema de esta ejecución; no modifica ni elimina esa cuenta. |
| `ConnectionStrings:SolarUyAceptacionLectura` | Alternativa local protegida para la conexión de solo lectura, dentro de `appsettings.Local.json`. |
| `SOLARUY_ACEPTACION_ADMIN_CONNECTION` | Alternativa para crear una cuenta efímera de solo lectura y otorgarle `SELECT`; solo se requiere si no se proporciona una cuenta existente. |

Un administrador debe limitar los permisos de aceptación al prefijo de esquemas previsto. Si la cuenta de lectura solo tiene acceso a la base habitual, debe autorizarse también el esquema de aceptación: el arnés nunca utiliza la base habitual para provocar fallos. El modo de creación de cuenta efímera usa origen `localhost`; para MySQL remoto proporcionar una cuenta de lectura existente autorizada para el origen real.

Se puede otorgar `SELECT` sobre el nombre exacto configurado en `SOLARUY_ACEPTACION_BASE` antes de ejecutar el arnés. Si se usan permisos por prefijo y `partial_revokes=OFF`, comprobar que el permiso incluya el comodín final; un permiso sobre `solaruy_req002_aceptacion_` sin `%` solo cubre esa base literal. Ejemplo administrativo con marcadores:

```sql
GRANT SELECT ON `solaruy\_req002\_aceptacion\_%`.* TO '<usuario-lectura>'@'<origen>';
SHOW GRANTS FOR '<usuario-lectura>'@'<origen>';
```

Conservar el nombre elegido durante una revisión pendiente. Cada nueva ejecución prepara otro esquema y requiere permisos para ese nombre; no otorgar acceso por error a un esquema de una ejecución anterior ya eliminada.

```powershell
python arnes/prompting/req002_cambio_base_datos/verificar_aceptacion.py
```

Opciones: `--puerto 5285`, `--solo-infraestructura` y `--mantener-servidor`. Antes de crear recursos, el arnés rechaza un nombre no identificado o un esquema ya existente. Crea un segundo esquema efímero para comprobar las prioridades. Durante esa comprobación sustituye temporalmente la conexión del archivo local, conserva sus bytes originales en memoria y los restaura en `finally`; ejecutar sin ediciones concurrentes de ese archivo.

**Arnés histórico incompatible con el acceso OIDC actual:** la aceptación de login se sustituye por req003; no ejecutarlo como evidencia de identidad externa. Generaba identidades aleatorias para `Administrador`, `Usuario` y `administrador`, inicia únicamente su servidor oculto y lo detiene al terminar, incluso ante fallos. Los casos incluyen esquema, GUID, lectura, duplicado concurrente, clave primaria, permisos, HTTP, reinicio, Unicode, longitudes, solicitudes manipuladas, autorización, antifalsificación y fallos de escritura mediante un trigger exclusivo del esquema efímero. Los logs se depuran antes de guardarse.

Las evidencias quedan en `artifacts/req002/ejecucion-<id>/resultados.json`, logs de migraciones y servidor; `artifacts/req002/ultima-ejecucion.json` apunta al último resultado. Si faltan MySQL, credenciales o permisos, devuelve un resultado no exitoso y conserva el caso pendiente. Un recorrido con teclado se registra separadamente; la aceptación automática no lo da por hecho.

`--mantener-servidor` preserva recursos para revisar con teclado cuando los casos ejecutables terminan. Guarda PID y URL en `servidor-manual.json` y las identidades efímeras en `identidades-privadas.json`, ambos ignorados. Finalizada la revisión, detener únicamente ese PID y eliminar únicamente los esquemas y cuentas efímeros registrados en esa ejecución; no borrar cuentas existentes. Los archivos de credenciales temporales se eliminan durante la limpieza habitual. Las evidencias depuradas pueden conservarse.

El script de aceptación de `req001` corresponde al motor anterior y **no valida MySQL**. La carpeta se conserva como antecedente inmutable.

## Conservación de datos

Este cambio prepara un esquema nuevo y no importa registros del motor anterior. No elimina bases locales ni respaldos. Una futura importación requiere origen, volumen, respaldo y procedimiento acordados, conservar IDs y validar nombres y claves de hasta 100 escalares Unicode. No truncar registros automáticamente.

## Fuentes técnicas

- [Proveedor oficial versionado y dependencias](https://www.nuget.org/packages/MySql.EntityFrameworkCore/10.0.9).
- [Configuración EF Core de Connector/NET](https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework-core.html).
- [Colaciones binarias y NO PAD de MySQL](https://dev.mysql.com/doc/refman/8.4/en/charset-binary-collations.html).
