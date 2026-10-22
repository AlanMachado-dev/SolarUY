# req002 — Configurar el acceso externo de SolarUY

> Identificador vigente: **req002 (OpenID Connect)**, antes req003. Las evidencias en artifacts/req003 y los prefijos técnicos de pruebas conservan su nombre original. Ver [estructura y equivalencias](../../README.md).

Implementación local verificada el 20/10/2026 en Windows: SDK .NET 10.0.400, runtime 10.0.11, `Microsoft.AspNetCore.Authentication.OpenIdConnect` **10.0.12**, IdentityModel 8.19.2, EF Core 10.0.12 y MySql.EntityFrameworkCore 10.0.9. La restauración conserva advertencias NU1903 preexistentes del paquete WebAssembly.Server 10.0.12. No se actualizaron dependencias ajenas al requerimiento.

El navegador envía un POST protegido a SolarUY, se redirige a Universal Login, vuelve a `/signin-oidc` y recibe una cookie local después de validar el protocolo. Las credenciales se ingresan exclusivamente en Auth0. La autorización del alta sigue en el servicio de Application y exige el rol exacto `Administrador`.

**Estado de aceptación (20/10/2026):** se verificaron Universal Login y cierre reales, Administrador, Operador, ambos roles y cuenta sin roles, con MySQL en un esquema exclusivo de aceptación. La base habitual se conservó. El usuario preparó las cuentas y confirmó el administrador anterior al primer ingreso. Se omitieron por decisión del usuario las variantes de roles desconocidos, malformados y con distinta capitalización. La aplicación Auth0 es la prevista para el sistema; no se certificó una aplicación separada de producción (E01). Resultados y límites en [aceptacion.md](aceptacion.md) y [tasks.md](tasks.md).

## 1. Preparar Auth0 antes del primer ingreso

Usar un tenant de prueba o una aplicación aislada bajo responsabilidad de quien administre identidades:

1. En Applications, crear o seleccionar una **Regular Web Application**. Habilitar una conexión apropiada para sus usuarios y Universal Login. Usar Authorization Code; SolarUY agrega PKCE. No configurar una SPA, audience de API ni permisos de Management API para esta integración.
2. En sus ajustes registrar exactamente:

   | Ajuste | Perfil local HTTPS |
   | --- | --- |
   | Allowed Callback URLs | `https://localhost:7178/signin-oidc` |
   | Allowed Logout URLs | `https://localhost:7178/signout-callback-oidc` |

   En otros entornos agregar sus URLs públicas HTTPS exactas. No hacen falta comodines ni CORS abierto.
3. Comprobar que el documento `/.well-known/openid-configuration` de la autoridad publique `end_session_endpoint`. Si falta, activar **RP-Initiated Logout End Session Endpoint Discovery** en Settings → Advanced → Login and Logout. El middleware usa descubrimiento; no se construye una URL `/v2/logout` manualmente.
4. En User Management → Roles, crear o reutilizar los roles **`Administrador`** y **`Operador`**, respetando mayúsculas. No tienen jerarquía implícita.
5. En User Management → Users, crear o seleccionar la cuenta inicial de la aplicación, comprobar si ya tiene el rol y asignarle explícitamente `Administrador` en Roles. Hacerlo **antes del primer ingreso**. Administrar el Dashboard no otorga este rol en SolarUY. Entregar el acceso por invitación o restablecimiento mediante el proveedor; no publicar contraseñas.
6. Preparar además una cuenta Operador y otra sin roles para aceptación. Planificar ambos roles y variantes inválidas con identidades dedicadas, sin alterar usuarios habituales.

En Actions → Library, crear una Action para **Login / Post Login**, con este código completo:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const claim = 'https://solaruy.example/roles';
  const roles = event.authorization?.roles;
  api.idToken.setCustomClaim(claim, Array.isArray(roles) ? roles : []);
};
```

Guardar y **Deploy**. En Actions → Flows → Login, insertar la Action entre Start y Complete y aplicar los cambios. Publicarla sin vincularla al flujo no alcanza. El namespace debe coincidir exactamente con `RolesClaimType`; identifica un claim y no es un servicio que deba resolver DNS. Se usan roles administrados de Authorization Core, nunca metadata editable, correo o nombre para conceder permisos. Al probar, comprobar la presencia de los roles en el servidor sin copiar tokens a servicios públicos o evidencias.

Referencias oficiales: [cliente OIDC de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0), [roles en ID token mediante Actions](https://support.auth0.com/center/s/article/add-roles-and-permissions-to-the-id-token-using-actions), [logout OIDC de Auth0](https://auth0.com/docs/authenticate/login/logout/log-users-out-of-auth0).

## 2. Configuración privada del servidor

Desde la raíz, copiar la plantilla **solo si no existe** el archivo local:

```powershell
if (-not (Test-Path SolarUY/appsettings.Local.json)) {
    Copy-Item SolarUY/appsettings.Local.example.json SolarUY/appsettings.Local.json
}
```

Si ya existe, conservar la conexión MySQL y agregar únicamente esta sección al objeto raíz:

```json
"OpenIdConnect": {
  "Authority": "https://<dominio-del-tenant>/",
  "ClientId": "<client-id>",
  "ClientSecret": "<secreto-solo-en-configuracion-privada>",
  "RolesClaimType": "https://solaruy.example/roles"
}
```

Reemplazar marcadores con los ajustes de la aplicación Auth0. Authority debe ser HTTPS absoluta sin credenciales ni fragmentos. Las opciones incompletas fallan al iniciar con mensajes que identifican la clave, sin su valor. Esto no comprueba disponibilidad, secreto correcto o existencia del administrador.

El archivo `SolarUY/appsettings.Local.json` y `artifacts/` están excluidos de Git. Retirar la sección histórica `Autenticacion` de la configuración privada cuando su responsable confirme la transición: ya no se lee y no permite acceso. La implementación conserva el archivo privado existente para evitar sobrescribir la conexión u otros datos.

La precedencia, de menor a mayor, sigue siendo `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json` → variables de entorno → argumentos. Variables disponibles: `OpenIdConnect__Authority`, `OpenIdConnect__ClientId`, `OpenIdConnect__ClientSecret`, `OpenIdConnect__RolesClaimType` y `ConnectionStrings__SolarUy`. Proveer secretos desde la configuración protegida del proceso o el gestor del despliegue; no pegarlos en argumentos ni historial de consola. No copiarlos al cliente o a `wwwroot`.

El cargador de configuración limpia sus fuentes y **no carga User Secrets**. `dotnet user-secrets` por sí solo no configura esta aplicación. Usar el archivo local o variables. Reiniciar el servidor al cambiar opciones.

## 3. Restaurar y ejecutar

Requiere MySQL con el esquema existente y certificado HTTPS local confiable. Desde la raíz:

```powershell
dotnet restore SolarUY.slnx
dotnet dev-certs https --trust
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

Si faltan las tablas, seguir la [preparación de MySQL de mod001](../../modificaciones/mod001_cambio_base_datos/configuracion.md), sobre el destino elegido. OIDC no agrega tablas ni migraciones. No reinicializar la base privada ni generar otra migración.

Abrir [Iniciar sesión](https://localhost:7178/cuenta/iniciar-sesion), pulsar **Iniciar sesión con proveedor externo** y completar Universal Login. Un ingreso sin destino explícito termina en `/`. El menú permite comprobar el nombre o una etiqueta neutral y los roles reconocidos. Administrador puede registrar parques y ver ID y `0 MW`; Operador no ve el enlace y recibe acceso denegado al intentar el alta directamente. Ambos roles permiten el alta por Administrador; sin roles reconocidos la sesión es válida pero el alta se deniega.

Para terminar, pulsar **Cerrar sesión**. El POST elimina la cookie local antes de preparar el cierre OIDC con ID token y retorna por el callback permitido a `/`. Un fallo manejable muestra un mensaje controlado; si el navegador ya está en un proveedor inaccesible, puede quedarse allí aunque SolarUY haya borrado su cookie. No se garantiza cerrar la sesión de un proveedor social o corporativo detrás de Auth0.

Las sesiones duran **30 minutos**, no son persistentes y no tienen renovación deslizante. Al vencer se pide nuevo ingreso; el SSO externo puede seguir activo. Cambios de rol o bloqueo pueden tardar hasta ese vencimiento en reflejarse. Cerrar e ingresar nuevamente obtiene los roles actualizados. No hay refresh remoto, consulta por petición ni revocación inmediata. El despliegue invalida las sesiones locales antiguas por el nombre nuevo `SolarUY.Sesion.Oidc`.

Solo el ID token se conserva en el ticket protegido para logout. No se transfieren tokens al cliente ni al almacenamiento JavaScript. Verificar tamaño de cookie con el tenant real; los perfiles con muchos claims pueden requerir reducir el ID token emitido por el proveedor.

## 4. Despliegue y diagnóstico

Data Protection usa el identificador común **`SolarUY`**. Conservar el anillo de claves fuera del almacenamiento efímero del contenedor, protegerlo en reposo con el mecanismo del host, limitar permisos a la cuenta del servicio y respaldarlo de forma protegida. Varias instancias deben compartir anillo e identificador y poder descifrar las claves. Configurar ese almacenamiento en el despliegue antes de servir tráfico; los valores predeterminados del host no garantizan persistencia en contenedores.

Si hay proxy, configurar esquema y host públicos con forwarded headers **solo de proxies conocidos**, antes de HTTPS y autenticación. El arranque actual no confía en cabeceras arbitrarias ni abre CORS. Registrar las URLs públicas exactas en Auth0. No habilitar logs PII, HTTP de tokens o query strings de callbacks; revisar también el registro de acceso del proxy. Los logs de identidad de SolarUY usan códigos internos sin excepciones completas.

| Síntoma | Revisión |
| --- | --- |
| Falla al arrancar | Completar la clave `OpenIdConnect` indicada; no usar los marcadores de la plantilla. Conservar MySQL válido. |
| Error al iniciar con proveedor | Authority del tenant correcto, Client ID y secreto de Regular Web Application vigente; conectividad HTTPS y descubrimiento. No desactivar validaciones. |
| Callback o cierre rechazado | URL HTTPS exacta, puerto, path y dominio coincidentes con las listas de Auth0; `end_session_endpoint` publicado. |
| Sesión sin permisos | Rol asignado en Authorization Core, namespace idéntico y Action desplegada **y vinculada** a Login. Cerrar e ingresar nuevamente. |
| Error de certificado | Confiar el certificado local, abrir el perfil `https`; revisar reloj y CA del proveedor. |
| Correlación o state inválido | Iniciar de nuevo desde el botón; comprobar cookies, mismo host, HTTPS, proxy y claves compartidas. No reutilizar callbacks ni imponer SameSite Strict a nonce/correlación. |
| Formulario 400 | Recargar la página para obtener un nuevo token antifalsificación. No se inició ni cerró sesión con ese POST. |
| Inicio 429 | Esperar un minuto. Hay diez POST por minuto y origen; callbacks no consumen este límite. |
| Fallo de cierre externo | La sesión local fue eliminada; revisar descubrimiento y disponibilidad del proveedor. Reintentar desde el proveedor si quedó inaccesible. |

## 5. Controles y aceptación

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
python arnes/prompting/tests/req002/verificar_controles_locales.py
```

El último recorrido usa un proceso propio, opciones ficticias y ninguna conexión física a MySQL. Comprueba HTTP local; **no valida Auth0 ni genera sesiones simuladas**. Seguir [aceptacion.md](aceptacion.md) para los casos externos y conservar resultados depurados en `artifacts/req003`.

Los arneses `arnes/prompting/tests/req001/verificar_aceptacion.py`, `arnes/prompting/tests/req001/generar_hash.py` y el recorrido de login de `arnes/prompting/tests/mod001/verificar_aceptacion.py` corresponden al acceso local histórico. Sus casos de contraseñas y hashes se sustituyen por F01–F22 de req002; la regresión de alta/MySQL se repite con identidad externa en F23–F24. No ejecutarlos para afirmar compatibilidad OIDC ni introducir un bypass. `arnes/prompting/tests/mod001/verificar_persistencia.cs` sigue siendo una referencia de persistencia independiente del proveedor, sujeto a un destino aislado.
