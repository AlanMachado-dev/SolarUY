# Especificaciones técnicas: autenticación externa con OpenID Connect

Fuente funcional: `requirements.md` de esta carpeta. Documento de diseño para revisión y planificación; la integración descrita todavía no está implementada.

## Objetivo, alcance y decisiones generales

Reemplazar la validación de usuarios locales por autenticación externa mediante **OpenID Connect 1.0 (OIDC)**, utilizando **Auth0** como proveedor inicial recomendado. SolarUY reconocerá los roles exactos `Administrador` y `Operador`, y contará con un administrador preparado en el proveedor antes del primer uso.

Se interpreta «OpenID» como OpenID Connect, de acuerdo con el diagrama de arquitectura y la recomendación de Auth0 del requerimiento. OIDC autentica la identidad; SolarUY aplica la autorización de sus casos de uso con los roles recibidos de una fuente confiable.

Se mantienen .NET 10, Blazor Web App con soporte WebAssembly, MySQL y la separación lógica de Domain, Application, Infrastructure y Presentation en el proyecto servidor. El alta de parques actual utiliza SSR y continuará así. La autenticación se resuelve en el servidor mediante redirecciones de navegador completo y una cookie propia de sesión. No se requiere trasladar el alta a WebAssembly ni incorporar una API nueva para completar este requerimiento.

Aplican las reglas de `.agents/rules` y su copia bajo `arnes/.agents/rules`: Clean Architecture, SOLID, nomenclatura, vistas sin lógica de negocio, protección de diagramas y controles de calidad. Se respetan los diagramas de `.agents/images`, que ya muestran un servicio de identidad OpenID Connect externo. No se modifican los diagramas, no se incorpora MediatR ni se crean proyectos adicionales de capas.

### Situación actual y alcance de la adaptación

- `Program.cs` registra autenticación por cookie, usuarios de `Autenticacion:Usuarios`, un verificador de contraseñas y endpoints locales de inicio y cierre de sesión.
- `Infrastructure/Seguridad/CredencialesService.cs` comprueba hashes y crea las identidades locales. `AutenticacionOptions.cs` define esos usuarios.
- `IUsuarioActualService` y `UsuarioActualService` exponen autenticación y pertenencia a roles sin trasladar detalles de ASP.NET Core a Application.
- Tanto `AltaParqueSolar.razor` como `ParqueSolarService` exigen `Administrador`; el menú utiliza `AuthorizeView`.
- El nuevo diseño sustituye la validación de credenciales, conserva el contrato de usuario actual y adapta páginas, endpoints, configuración y documentación.

Quedan fuera del alcance la administración de usuarios desde SolarUY, la importación de contraseñas locales, la sincronización de usuarios con MySQL, permisos granulares, nuevos casos de uso de Operador y la revocación inmediata de todas las sesiones. El acceso a funcionalidades futuras deberá definirse en sus respectivos requerimientos.

## 1. Domain

### 1.1. Modelo de negocio

No se modifica el comportamiento de `ParqueSolar`, `NombreParqueSolar` ni sus validaciones. La autenticación externa es un detalle técnico y no requiere entidades de usuario, tablas de roles ni referencias a tokens dentro del dominio actual.

Domain continúa usando C# puro, sin dependencias de ASP.NET Core, Auth0, JWT, EF Core ni otras capas. No se agregan propiedades de identidad a los parques ni se altera el modelo de dominio para reflejar el proveedor.

### 1.2. Reglas que se preservan

El alta conserva nombre y ubicación obligatorios, normalización y unicidad del nombre, máximo de 100 caracteres, ID autogenerado y capacidad inicial de `0 MW` sin nodos ni paneles. Las pruebas xUnit existentes siguen siendo la referencia para estas reglas.

Este requerimiento no introduce comportamiento nuevo en Domain. Si durante la implementación fuera necesario modificarlo, se debe justificar el cambio de alcance y acompañarlo con la prueba xUnit correspondiente, conforme a las reglas del proyecto.

## 2. Application

### 2.1. Contrato de identidad y dependencias

Se conserva `Application/ParquesSolares/IUsuarioActualService` con sus operaciones `EstaAutenticado` y `PerteneceAlRol(string rol)`. El contrato ya es suficiente para el caso de uso actual; no se requiere moverlo ni agregar un servicio de autenticación de aplicación que replique el middleware.

Application no recibe `HttpContext`, `ClaimsPrincipal`, tokens, secretos, tipos del SDK de Auth0 ni opciones OIDC. La obtención y validación de la identidad corresponde a Infrastructure. Las dependencias de `ParqueSolarService` continúan entrando exclusivamente por constructor.

### 2.2. Autorización del alta de parques

`ParqueSolarService.CrearAsync` mantiene, antes de consultar o escribir datos:

1. Si el usuario no está autenticado, devolver `AutenticacionRequerida`.
2. Si no pertenece al rol exacto `Administrador`, devolver `AccesoDenegado`.
3. Solo después ejecutar validaciones de dominio, comprobación de nombre y persistencia.

La autorización del componente Blazor complementa esta validación. Ocultar un enlace no concede ni revoca permisos del servicio.

| Identidad | Sesión en SolarUY | Alta de parques |
| --- | --- | --- |
| Anónima | No autenticada | Requiere iniciar sesión; no consulta ni escribe parques. |
| `Administrador` | Autenticada | Permitida, sujeta a las validaciones existentes. |
| `Operador` | Autenticada | Denegada. |
| Ambos roles | Autenticada | Permitida por pertenecer a `Administrador`. |
| Sin roles reconocidos | Autenticada | Denegada; no se asigna un rol por defecto. |

Los roles no tienen una jerarquía implícita: `Administrador` no equivale automáticamente a `Operador`. Esta entrega mantiene el permiso ya existente y habilita la identificación del Operador para usos posteriores.

No se generan pruebas unitarias de Application, conforme a las reglas del workspace. La matriz se verifica mediante aceptación funcional y controles arquitectónicos.

## 3. Infrastructure

### 3.1. Integración OIDC y organización

Usar `Microsoft.AspNetCore.Authentication.OpenIdConnect` en el **servidor**, con una versión estable `10.0.x` compatible con las dependencias Microsoft existentes. Registrar la versión exacta al implementar y comprobar restauración y compilación; no agregar este paquete al cliente. No se necesita instalar además un SDK de Auth0 ni implementar manualmente el protocolo.

El diseño adopta un cliente confidencial con **Authorization Code y PKCE**; el secreto permanece en el servidor. Esta elección sigue la [guía oficial de OIDC para aplicaciones web de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0).

Organización propuesta, sin exigir un archivo por cada opción:

```text
SolarUY/
├── Domain/ParquesSolares/                      # Sin cambios funcionales
├── Application/ParquesSolares/                 # Contrato y autorización existentes
├── Infrastructure/Seguridad/
│   ├── AutenticacionExternaOptions.cs          # Opciones propias de SolarUY
│   ├── SeguridadConfiguration.cs               # Registro y validación de opciones
│   ├── OpenIdConnectEventsHandler.cs            # Adaptación de claims y errores
│   └── UsuarioActualService.cs                 # Adaptador del usuario HTTP
├── Presentation/Pages/                         # Acceso externo y páginas existentes
├── Program.cs                                 # Composición y endpoints HTTP
└── appsettings.Local.example.json              # Plantilla ampliada sin secretos reales
arnes/prompting/req003_implementacion_openID/
├── requirements.md
├── specs.md
└── configuracion.md                            # Entregable de implementación
```

`AutenticacionExternaOptions` contiene las opciones propias de SolarUY y evita colisionar con la clase `OpenIdConnectOptions` del framework. Infrastructure encapsula los eventos técnicos y `Program.cs` actúa como raíz de composición. Las dependencias de los adaptadores se inyectan por constructor; no se resuelven servicios desde componentes.

### 3.2. Esquemas y flujo de autenticación

Se definen dos esquemas: `Cookies` para la sesión local y `OpenIdConnect` para el proveedor externo. `Cookies` es el esquema predeterminado de autenticación, challenge y forbid, conservando `LoginPath = /cuenta/iniciar-sesion` y `AccessDeniedPath = /cuenta/acceso-denegado`. El endpoint de inicio externo ejecuta explícitamente el challenge de `OpenIdConnect`, cuyo `SignInScheme` es `Cookies`.

1. El visitante abre la página de inicio de sesión o llega allí desde una ruta protegida.
2. Pulsa el botón de acceso externo, que envía un formulario POST con antifalsificación a `/cuenta/autenticar`.
3. El servidor valida el retorno local y ejecuta el challenge. Auth0 muestra Universal Login y recibe las credenciales; SolarUY no las captura.
4. El navegador vuelve a `/signin-oidc`. El middleware verifica la respuesta y canjea el código desde el servidor.
5. Tras validar la identidad y adaptar los roles, se emite la cookie de SolarUY.
6. El navegador vuelve al destino local válido, o a `/` si no se proporcionó uno. Un Operador puede finalizar el inicio sin ser enviado por defecto al alta de parques.

Configurar `ResponseType = code`, `UsePkce = true`, scopes `openid profile email`, `RequireHttpsMetadata = true`, `MapInboundClaims = false` y `GetClaimsFromUserInfoEndpoint = false`. Los roles deben viajar en el ID token. No solicitar `offline_access`, permisos de Management API ni un audience de API ajeno a esta integración.

Conservar las validaciones estándar del middleware: firma, emisor autorizado, audiencia correspondiente al Client ID, vigencia, `state`, correlación y `nonce`. No sustituirlas por decodificación manual de JWT ni deshabilitarlas para sortear fallos de configuración. Descubrimiento y claves se obtienen de la autoridad HTTPS configurada. El soporte PAR se negocia según los metadatos del proveedor; no se asume obligatorio para Auth0.

Las rutas de callback pertenecen al middleware. No se implementan como páginas Razor ni se les aplica la validación antifalsificación de formularios locales: su protección corresponde al protocolo OIDC. Las redirecciones y callbacks se realizan mediante navegación completa, sin enhanced navigation.

### 3.3. Identidad y roles confiables

La identidad externa estable es el par **`iss` + `sub`**. Nombre y correo son datos para mostrar, no claves para otorgar privilegios. Configurar `NameClaimType = name`; cuando falte, la interfaz puede mostrar una etiqueta neutral.

Para Auth0 se propone el claim personalizado **`https://solaruy.example/roles`**, configurable como `RolesClaimType`. Es un identificador de namespace, no una dirección de servicio que deba resolverse. La misma cadena debe aparecer en el proveedor y en la aplicación.

Crear y asignar los roles en Auth0 Authorization Core. Publicar y vincular al flujo de Login una **Post Login Action** que lea los roles asignados de `event.authorization.roles` y los agregue al ID token usando `api.idToken.setCustomClaim`. La guía de implementación debe incluir el código de esa Action y cómo desplegarla y asociarla al flujo. Auth0 documenta este mecanismo en [roles en el ID token mediante Actions](https://support.auth0.com/center/s/article/add-roles-and-permissions-to-the-id-token-using-actions) y en la [API de Post Login Actions](https://auth0.com/docs/actions/reference/post-login/post-login-api-object).

La adaptación de claims, después de la validación criptográfica, debe:

- Leer únicamente el claim configurado y aceptar valores de texto exactos `Administrador` y `Operador`, con comparación sensible a mayúsculas.
- Convertir el array de roles del token en claims individuales utilizables por `IsInRole`, configurar `RoleClaimType` con el namespace acordado y eliminar duplicados. No tratar un array JSON serializado como un único nombre de rol.
- Descartar valores desconocidos o malformados sin conceder permisos; nunca inferir Administrador por correo, nombre, orden de registro, formulario o metadata editable por el usuario.
- Permitir una identidad válida sin roles reconocidos, conservando el rechazo de las operaciones protegidas.

El mapeo explícito de nombre y roles se apoya en la [documentación de claims de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0). `UsuarioActualService` seguirá consultando el principal autenticado mediante `IHttpContextAccessor`; fuera de una petición autenticada devuelve falso y no conserva identidades en servicios singleton.

### 3.4. Administrador inicial

El estado inicial se prepara **antes del primer uso** en el proveedor, sin crear cuentas privilegiadas al arrancar SolarUY:

1. Crear una aplicación de Auth0 de tipo **Regular Web Application**, habilitar una conexión apropiada y Universal Login.
2. Crear los roles exactos `Administrador` y `Operador`.
3. Crear o seleccionar una cuenta de usuario de la aplicación y asignarle explícitamente `Administrador` desde el Dashboard. Ser administrador del Dashboard de Auth0 no implica tener este rol de SolarUY.
4. Publicar y asociar la Action de roles; comprobar que esa cuenta recibe `Administrador` al iniciar sesión.
5. Entregar el acceso a la persona responsable mediante el mecanismo del proveedor, sin publicar su contraseña en código, documentos ni configuración.

La preparación es operativa y repetible: comprobar si la cuenta y el rol ya existen antes de crearlos. No se asigna Administrador al primer usuario ni se requiere una cuenta Management API en ejecución. La [guía oficial de Auth0 con creación y asignación de roles](https://developer.auth0.com/resources/code-samples/api/standard-library/basic-role-based-access-control) describe las operaciones de Dashboard aplicables.

El criterio de aceptación se considera cumplido cuando existe esa cuenta con el rol asignado y puede acceder al alta mediante una sesión OIDC real. Que el servidor arranque no prueba que el administrador esté preparado. Una cuenta adicional de Operador se utiliza para aceptación; no es una nueva obligación sobre usuarios iniciales de producción.

### 3.5. Configuración y secretos

Agregar la sección `OpenIdConnect` a la plantilla versionada del servidor, conservando la conexión MySQL existente:

```json
{
  "OpenIdConnect": {
    "Authority": "https://<dominio-del-tenant>/",
    "ClientId": "<client-id>",
    "ClientSecret": "<secreto-solo-en-configuracion-privada>",
    "RolesClaimType": "https://solaruy.example/roles"
  }
}
```

El ejemplo muestra únicamente la sección nueva. Los marcadores no son credenciales utilizables. `CallbackPath = /signin-oidc` y `SignedOutCallbackPath = /signout-callback-oidc` se fijan explícitamente en la configuración técnica para evitar divergencias con la guía.

Reutilizar la precedencia existente: `appsettings.json`, `appsettings.{Environment}.json`, `appsettings.Local.json`, variables de entorno y argumentos, de menor a mayor prioridad. El archivo local permanece excluido de Git. Para secretos desplegados, usar el origen privado disponible y variables como `OpenIdConnect__ClientSecret`; no pasarlos como argumentos de consola ni copiarlos a `SolarUY.Client` o `wwwroot`.

`PersistenciaConfiguration.CargarConfiguracion` actualmente limpia los orígenes de configuración y no agrega User Secrets. La guía no debe indicar que `dotnet user-secrets` funciona sin adaptar antes ese cargador. No se requiere esa adaptación si se utilizan el archivo local o variables de entorno.

Validar al iniciar que Authority sea una URL absoluta HTTPS sin credenciales ni fragmentos, que Client ID y secreto estén completos y que `RolesClaimType` no esté vacío. No registrar valores sensibles ni habilitar PII en los diagnósticos de identidad. La validación local de opciones no demuestra disponibilidad del proveedor, credenciales correctas ni existencia del administrador; eso se verifica durante el flujo real.

Registrar en Auth0, para el perfil HTTPS existente:

| Ajuste | URL local |
| --- | --- |
| Allowed Callback URLs | `https://localhost:7178/signin-oidc` |
| Allowed Logout URLs | `https://localhost:7178/signout-callback-oidc` |

La configuración de aplicaciones y protección del secreto se describen en [Application Settings de Auth0](https://auth0.com/docs/get-started/applications/application-settings). En otros entornos se registran las URLs públicas HTTPS exactas. No se necesitan comodines ni una apertura general de CORS para este flujo de redirecciones.

La guía utiliza el perfil `https` y un certificado local confiable. Si existe un proxy, configurar esquema y host públicos mediante forwarded headers exclusivamente de proxies confiables antes de autenticación y redirecciones. No confiar indiscriminadamente en cabeceras enviadas por clientes.

### 3.6. Sesión, cierre y transición

Mantener sesiones de 30 minutos, no persistentes y sin renovación deslizante, con `UseTokenLifetime = false` para que la duración local quede explícita. La cookie tendrá `HttpOnly`, `Secure = Always` y `SameSite = Lax`. Conservar los ajustes de cookies temporales de correlación y nonce que requiere el middleware; no imponerles globalmente `SameSite = Strict`.

Usar un nombre nuevo, por ejemplo **`SolarUY.Sesion.Oidc`**, para que las cookies locales anteriores no autentiquen tras el cambio. Eliminar la cookie antigua al completar inicio o cierre cuando corresponda. El despliegue obliga a iniciar sesión nuevamente.

Para el logout OIDC, preservar únicamente el `id_token` en las propiedades del ticket protegido. Una implementación posible es activar `SaveTokens` y, en `OnTicketReceived`, sustituir los tokens guardados por una colección que contenga solo el ID token antes de emitir la cookie. No conservar access tokens ni refresh tokens innecesarios y verificar el tamaño final de la cookie. El ID token no se devuelve a componentes WebAssembly, almacenamiento JavaScript ni logs.

`POST /cuenta/cerrar-sesion`, autorizado y con antifalsificación, debe eliminar la sesión local y ejecutar el sign-out del esquema OIDC. El handler dirige al `end_session_endpoint` descubierto y envía `id_token_hint`; el retorno pasa por `/signout-callback-oidc` y termina en `/`. El tenant debe publicar el endpoint de cierre en sus metadatos. Se sigue [RP-Initiated Logout de Auth0](https://auth0.com/docs/authenticate/login/logout/log-users-out-of-auth0).

La sesión local debe quedar eliminada aunque el proveedor no responda durante el cierre. Mostrar un mensaje de fallo controlado si no se puede completar el cierre externo. No prometer el cierre de sesiones del proveedor social o corporativo que pueda existir detrás de Auth0.

Los roles de una cookie representan la última autenticación. Un cambio de roles o bloqueo en Auth0 puede tardar hasta la expiración de la sesión local en reflejarse; cerrar sesión y volver a ingresar obtiene los roles actuales. No se implementan consulta remota por petición, refresh automático ni back-channel logout en este alcance. En producción se deben conservar y proteger las claves de Data Protection; si se despliegan varias instancias, deben compartirlas con la misma identificación de aplicación.

Retirar `CredencialesService`, `AutenticacionOptions`, `UsuarioConfigurado`, sus registros de hash y la exigencia de `Autenticacion:Usuarios`. `/cuenta/autenticar` deja de leer nombre y contraseña. No habrá fallback local si falla Auth0. No se requieren migraciones MySQL ni cambios en las tablas de parques.

### 3.7. Errores técnicos

Cancelación, rechazo del proveedor, código vencido, correlación inválida y errores de validación no crean una nueva sesión. Manejar eventos de fallo con códigos de error internos y mensajes genéricos de Presentation, sin reflejar `error_description`, tokens, códigos de autorización ni excepciones completas en la URL o interfaz. No reintentar challenges automáticamente en un bucle.

La caída de Auth0 impide nuevos ingresos; las cookies ya válidas mantienen sus permisos hasta vencer. Mantener el límite existente de diez intentos por minuto por dirección de origen para el POST de inicio externo, con respuesta 429, y no aplicarlo a callbacks del protocolo.

No se generan pruebas unitarias de Infrastructure, conforme a las reglas del workspace.

## 4. Presentation

### 4.1. Páginas y navegación

- **`/cuenta/iniciar-sesion`:** sustituir usuario y contraseña por «Iniciar sesión con proveedor externo». El formulario conserva antifalsificación y desactiva enhanced navigation. Capturar `ReturnUrl` sin ejecutarlo desde el componente; el endpoint valida que sea local antes de incorporarlo a las propiedades protegidas de autenticación. Rechazar destinos externos, relativos a otro host o a los propios endpoints de autenticación para evitar redirecciones abiertas y ciclos. Usar `/` como retorno seguro.
- **`/`:** conservar la página y permitir reconocer que el inicio terminó mediante un estado de sesión en la página o menú, con nombre disponible y roles reconocidos. El Operador no necesita una pantalla nueva de negocio.
- **Menú:** conservar `AuthorizeView` y el enlace de alta exclusivo de Administrador. Presentar el cierre mediante POST y antifalsificación. Mostrar roles es presentación de datos autenticados, sin inferir permisos nuevos.
- **`/parques-solares/nuevo`:** conservar `[Authorize(Roles = "Administrador")]`, formulario, consumo de `IParqueSolarService`, errores y confirmación. Un acceso directo o POST de Operador debe ser rechazado por el servidor.
- **`/cuenta/acceso-denegado`:** conservar la ruta, con un mensaje comprensible de falta de permisos y enlace al inicio. No iniciar automáticamente otro challenge cuando ya existe una sesión sin el rol requerido.

Presentation no conoce clases de Infrastructure. Los endpoints HTTP en la composición coordinan únicamente operaciones del framework de autenticación, antifalsificación y retornos seguros; no validan contraseñas ni implementan reglas del alta.

Conservar `AddCascadingAuthenticationState` y `AuthorizeRouteView`. Cuando aplique, procesar forwarded headers confiables antes de la redirección HTTPS; luego mantener autenticación, autorización, rate limiting y antifalsificación. Revisar el orden concreto durante la implementación.

Las vistas protegidas actuales siguen en SSR. Si se añade estado visible dentro de un componente WebAssembly, transferir solo los datos mínimos de nombre y roles mediante los mecanismos de estado de autenticación de Blazor; esta visualización no autoriza operaciones de servidor. La guía oficial de [Blazor Web App con OIDC](https://learn.microsoft.com/aspnet/core/blazor/security/blazor-web-app-with-oidc) documenta callbacks y estado de autenticación. No es necesario agregar serialización de identidad al cliente si los componentes afectados siguen siendo SSR.

### 4.2. Mensajes y comportamiento ante errores

La página de acceso distinguirá cancelación de fallo técnico con mensajes breves, por ejemplo «Se canceló el inicio de sesión» y «No se pudo iniciar sesión. Intentá nuevamente». Los detalles del proveedor quedan en diagnósticos seguros del servidor.

Un usuario autenticado sin permiso recibe acceso denegado y puede volver al inicio o cerrar sesión. Un formulario local con antifalsificación inválida se rechaza con 400 y permite recargar; no inicia ni cierra sesiones. Al vencer la cookie se solicita un nuevo ingreso sin afirmar que también venció la sesión SSO de Auth0.

### 4.3. Documentación entregable

Durante la implementación crear `configuracion.md` en esta carpeta y actualizar el inicio rápido del `README.md` raíz para que reemplace la preparación de usuarios locales y hashes por la nueva guía. Revisar las instrucciones operativas vigentes de `req002` para que identifiquen el acceso local como antecedente y remitan a `req003`, conservando la información de MySQL. No reescribir los requerimientos ni las especificaciones históricas.

La guía debe ser breve y reproducible, e incluir:

1. Requisitos y versiones finalmente verificadas; flujo navegador → proveedor → callback → cookie → autorización.
2. Preparación de Auth0: aplicación web, conexión, URLs exactas, roles, Action completa y su vinculación al flujo.
3. Creación o selección del administrador inicial y asignación del rol de SolarUY, diferenciándolo de los permisos del Dashboard.
4. Copia o ampliación de configuración local, variables de entorno, secretos privados y precedencia, sin reemplazar la conexión MySQL ni incluir credenciales reales.
5. Comandos desde la raíz para restaurar y ejecutar el perfil HTTPS; preparación del esquema MySQL mediante el procedimiento existente cuando sea necesaria.
6. Cómo iniciar y cerrar sesión, comprobar Administrador y Operador, asignar o retirar roles desde Auth0 y obtener una nueva sesión con esos cambios.
7. Diagnóstico de URLs no autorizadas, Authority o secreto incorrectos, claim ausente, Action no vinculada, certificado local y cookies de correlación; limitación temporal de cambios de permisos y cierre externo.

La entrega de diseño actual genera solamente `specs.md`; la guía, código y actualizaciones operativas corresponden a la fase de implementación.

### 4.4. Aceptación funcional y trazabilidad

Realizar verificaciones HTTP y de navegador con un tenant de prueba o aplicación aislada, un administrador preparado, un Operador y una identidad sin roles. Usar datos de parques destinados a aceptación y registrar resultados sin secretos en `artifacts/req003`, excluidos de Git. No depender de los hashes locales ni del endpoint anterior para simular que OIDC funciona.

| Requisito o riesgo | Verificación y evidencia esperada |
| --- | --- |
| CA1: acceso externo | Desde SolarUY, completar Universal Login real y callback; la sesión persiste al navegar y recargar. Las credenciales solo se ingresan al proveedor. |
| CA1: Administrador | El token validado produce el rol correcto y el usuario puede registrar un parque; comprobar el registro en MySQL y la confirmación con ID y `0 MW`. |
| CA1: Operador | Inicio exitoso, estado de sesión reconocible, enlace de alta oculto y rechazo del GET y POST directos al alta; sin escrituras. |
| CA1: roles múltiples o ausentes | Ambos roles habilitan el alta por Administrador; sin roles, con roles desconocidos o con mayúsculas distintas no hay permisos de alta. |
| CA2: estado inicial | Evidencia de cuenta existente y rol asignado en el proveedor antes del primer ingreso; inicio real y alta exitosa. No existe promoción automática del primer usuario. |
| Adaptación existente | No quedan formulario de contraseñas, validación de hashes, requisito de usuarios locales ni fallback activo; una cookie anterior al cambio no autentica. |
| Protección del servicio | Confirmar que los rechazos de autenticación y rol ocurren antes de acceder al repositorio. Revisar código y observar ausencia de escrituras en los escenarios denegados. |
| Retornos seguros | Destino local válido se conserva; destino externo, esquema relativo, valor malformado o retorno a autenticación termina en `/` sin ciclos. |
| Callbacks inválidos | Respuesta manipulada, sin correlación o código reutilizado no genera una sesión; cancelación o fallo se informa sin filtraciones. |
| Cierre | POST válido elimina la cookie y completa logout del proveedor y callback. Tras cerrar, una ruta protegida exige ingreso; una caída del proveedor no conserva la sesión local. |
| Antifalsificación y límite | POST de inicio y cierre sin token se rechaza; el exceso de solicitudes de inicio devuelve 429; los callbacks legítimos funcionan. |
| Caducidad y cambios de roles | La cookie vence a los 30 minutos sin renovación deslizante. Una nueva autenticación refleja los roles actualizados; documentar la vigencia de cookies anteriores al cambio de rol. |
| Regresión del alta | Persistencia, duplicados, obligatorios, límite de nombre y manejo de errores MySQL mantienen su resultado con identidad externa. |
| Documentación | Otra persona puede configurar el proveedor, preparar el administrador y verificar ambos roles siguiendo la guía sin editar código. |

Los arneses históricos que envían usuario y contraseña al endpoint local requieren adaptación o reemplazo para validar esta versión. No informar sus resultados antiguos como evidencia de OIDC. Los fallos de identidad se provocan en el entorno de prueba; no alterar cuentas ni sesiones de uso habitual.

Si no hay tenant, credenciales de configuración o acceso a las identidades de prueba, registrar las verificaciones externas como **pendientes**. Una compilación exitosa o un token fabricado no satisfacen los criterios de aceptación.

### 4.5. Calidad y preparación de la planificación

No se generan pruebas unitarias de Presentation ni de las otras capas externas, según las reglas del proyecto. Durante la implementación ejecutar los controles existentes desde la raíz:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Los guardarraíles de ArchUnitNET se conservan: corregir el código ante fallos, sin alterar pruebas estructurales para acomodar la integración. La aceptación con Auth0 y MySQL complementa esos controles y debe registrarse por separado.

La planificación debe incluir configuración del proveedor y administrador, integración de servidor, mapeo de roles, renovación del nombre de cookie, inicio y cierre, retiro de credenciales locales, adaptación de vistas, documentación, controles arquitectónicos y aceptación real. Domain y Application se revisan para preservar contratos y reglas, sin introducir cambios artificiales.

Antes de ejecutar la aceptación externa deberán estar disponibles el dominio del tenant, Client ID, secreto, URLs del entorno y cuentas con los roles necesarios. Son datos operativos pendientes de configurar, no valores que deban inventarse o incluirse en este documento.
