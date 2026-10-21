# Plan de implementación: autenticación externa con OpenID Connect

## Base del plan y seguimiento

Fuentes: [specs.md](specs.md), [requirements.md](requirements.md) y las reglas del proyecto en `.agents/rules` y `arnes/.agents/rules`. Este plan implementa OIDC con Auth0, roles exactos `Administrador` y `Operador`, administrador inicial preparado en el proveedor y adaptación de la autenticación actual.

Seguimiento al cierre del 20/10/2026: las casillas marcadas indican trabajo completado y verificado, incluido Auth0 real. Quedan E01 (no se certificó aplicación separada de producción) y F07 (variantes de roles omitidas por decisión del usuario; cuenta sin roles comprobada). Consultar aceptacion.md. El registro histórico conserva el estado de cada etapa; la matriz y este resumen reflejan el resultado actual.

Orden del código: preparación → Domain → Application → Infrastructure → Presentation → documentación y aceptación → cierre. No avanzar a la siguiente capa sin compilación y controles de Domain y ArchUnitNET correctos. Ejecutar además ArchUnitNET después de cada cambio de código, dependencias o estructura conforme a las reglas; corregir el código ante fallos y repetir los controles afectados.

La preparación externa de Auth0 puede realizarse independientemente del código. Si faltan acceso al tenant, secretos o identidades, continuar el trabajo local posible y dejar pendientes las tareas externas. No confundir restauración, compilación o identidades simuladas con aceptación real de OIDC.

No generar pruebas unitarias de Application, Infrastructure ni Presentation. No modificar `CodeQuality.Tests` para acomodar el cambio. Domain no requiere comportamiento nuevo; cualquier modificación de su lógica debe acompañarse con su prueba xUnit. Se conservan los diagramas, MySQL, el esquema y el flujo SSR del alta; no se agregan MediatR, proyectos de capas, una API nueva ni gestión de usuarios dentro de SolarUY.

## 0. Preparación y línea base

- [x] P01. Registrar el estado del repositorio y los cambios preexistentes; identificar los archivos actuales de seguridad, composición, páginas y documentación que intervienen.
- [x] P02. Registrar un inventario y hashes de los diagramas de `.agents/images` y de los documentos fuente de `req003`, para comprobar al cierre que la implementación no los alteró.
- [x] P03. Ejecutar los tres controles de calidad del apartado «Comandos de control» y registrar la línea base, distinguiendo errores y advertencias preexistentes.
- [x] P04. Comprobar la disponibilidad de .NET 10 y del perfil HTTPS en `SolarUY/Properties/launchSettings.json`, con certificado local confiable para `https://localhost:7178`.
- [x] P05. Identificar la instancia MySQL ya configurada y un destino aislado para aceptación; preservar la configuración privada y no reinicializar bases ni migraciones existentes.
- [x] P06. Identificar el tenant o aplicación de Auth0 de prueba, su responsable y las URLs del entorno; registrar solamente qué datos faltan, sin copiar secretos al plan.
- [x] P07. Preparar `artifacts/req003` para evidencias sin secretos y comprobar que quede excluido de Git, al igual que `SolarUY/appsettings.Local.json`.

Resultado esperado: situación inicial conocida y trabajo local separado de las dependencias externas. No se exige disponer de Auth0 para revisar Domain o Application.

## 1. Domain

Dependencias: P01–P03. Referencia: sección 1 de `specs.md`. Esta capa se conserva sin cambios funcionales.

- [x] D01. Revisar que `ParqueSolar` y `NombreParqueSolar` mantengan las reglas actuales de obligatorios, normalización, unicidad, máximo de 100 caracteres, ID autogenerado y capacidad inicial de `0 MW`.
- [x] D02. Confirmar que el diseño de identidad no introduzca usuarios, roles, tokens, atributos de autorización ni referencias a ASP.NET Core, Auth0 o EF Core en Domain.
- [x] D03. Confirmar que no se necesita cambiar comportamiento de dominio; si se detecta una necesidad real dentro del alcance, justificarla y acompañar el cambio con xUnit, sin agregar lógica de autenticación al dominio.
- [x] D04. Ejecutar compilación de la solución, pruebas existentes de Domain y ArchUnitNET; registrar la salida de capa y si la revisión no requirió cambios.

Resultado esperado: reglas de parques intactas y dominio independiente del proveedor de identidad.

## 2. Application

Dependencia: D04. Referencia: sección 2 de `specs.md`. Archivos principales: `IUsuarioActualService.cs` y `Servicios/ParqueSolarService.cs` en `SolarUY/Application/ParquesSolares`.

- [x] A01. Conservar el contrato y ubicación de `IUsuarioActualService`, con `EstaAutenticado` y `PerteneceAlRol(string rol)`, sin exponer tipos del framework ni datos del protocolo OIDC.
- [x] A02. Verificar que `ParqueSolarService.CrearAsync` rechace anónimos con `AutenticacionRequerida` antes de consultar o escribir parques.
- [x] A03. Verificar que el servicio rechace identidades sin el rol exacto `Administrador` con `AccesoDenegado`, antes de usar el repositorio y sin jerarquías implícitas entre roles.
- [x] A04. Conservar validación de dominio, consulta de nombre, guardado confirmado, traducción del duplicado y DTO existentes, con dependencias por constructor.
- [x] A05. Revisar que Application no incorpore `HttpContext`, `ClaimsPrincipal`, Auth0, JWT, configuración OIDC ni un servicio que duplique el middleware.
- [x] A06. Ejecutar compilación, pruebas de Domain y ArchUnitNET; registrar la salida de capa y cualquier corrección necesaria.

Resultado esperado: el caso de uso conserva sus permisos y puede operar con la nueva identidad a través del mismo contrato.

## 3. Infrastructure

Dependencia del código: A06. Referencias: secciones 3.1–3.7 de `specs.md`. Ubicación principal: `SolarUY/Infrastructure/Seguridad`; `Program.cs` conserva el papel de raíz de composición y adaptación HTTP.

### 3.1. Proveedor externo y administrador inicial

Dependencia externa: acceso a la aplicación o tenant de prueba identificado en P06. Estas tareas no bloquean la compilación de las capas locales.

- [ ] E01. Crear o seleccionar una aplicación de Auth0 de tipo Regular Web Application y habilitar una conexión apropiada y Universal Login, sin reutilizar recursos de producción para aceptación.
- [x] E02. Registrar la URL de callback exacta `https://localhost:7178/signin-oidc` y las equivalentes HTTPS del entorno que se vaya a verificar.
- [x] E03. Registrar la URL de cierre exacta `https://localhost:7178/signout-callback-oidc` y comprobar que los metadatos publiquen `end_session_endpoint`.
- [x] E04. Crear o reutilizar los roles de aplicación exactos `Administrador` y `Operador` en Auth0 Authorization Core.
- [x] E05. Crear o seleccionar una cuenta de aplicación y asignarle explícitamente `Administrador` antes del primer uso, comprobando antes si la cuenta y asignación ya existen.
- [x] E06. Preparar una cuenta de Operador y otra sin roles para aceptación; definir cómo verificar ambos roles simultáneos y valores desconocidos sin afectar cuentas habituales.
- [x] E07. Preparar una Post Login Action que lea `event.authorization.roles` y publique el array en el ID token mediante `api.idToken.setCustomClaim`, con el claim `https://solaruy.example/roles`.
- [x] E08. Publicar y vincular la Action al flujo Login de Auth0; comprobar que utiliza exactamente el mismo namespace que SolarUY.
- [x] E09. Disponer del dominio del tenant, Client ID y secreto mediante configuración privada del servidor, sin incluirlos en documentos, código, evidencias o argumentos de consola.
- [x] E10. Registrar cómo la persona responsable obtendrá acceso a la cuenta inicial mediante el proveedor, sin enviar credenciales ni confundir el rol de SolarUY con permisos del Dashboard.

### 3.2. Dependencias y configuración del servidor

- [x] I01. Agregar al servidor `Microsoft.AspNetCore.Authentication.OpenIdConnect` en una versión estable `10.0.x` compatible con las dependencias existentes; registrar la versión exacta restaurada.
- [x] I02. Comprobar que el paquete y sus dependencias no se agreguen a `SolarUY.Client` y que no se incorpore además un SDK de Auth0 innecesario.
- [x] I03. Crear `AutenticacionExternaOptions` en Infrastructure con `Authority`, `ClientId`, `ClientSecret` y `RolesClaimType`, vinculada a la sección `OpenIdConnect`.
- [x] I04. Agregar validación de inicio para Authority absoluta HTTPS sin credenciales ni fragmentos, Client ID y secreto completos y nombre del claim no vacío, con errores sin valores sensibles.
- [x] I05. Crear `SeguridadConfiguration` para encapsular registro y opciones técnicas; mantener la llamada de composición en `Program.cs` y la inyección por constructor en adaptadores.
- [x] I06. Ampliar `SolarUY/appsettings.Local.example.json` con marcadores de la sección nueva, conservando la conexión MySQL y sin incorporar secretos reales.
- [x] I07. Reutilizar la precedencia existente de archivos, variables y argumentos; comprobar que `OpenIdConnect__ClientSecret` pueda sobrescribir el archivo local sin registrarlo ni pasarlo por consola.
- [x] I08. Conservar el cargador de configuración existente salvo necesidad justificada; no documentar User Secrets como disponible sin agregar antes su origen al cargador.

### 3.3. Esquemas, protocolo y claims

- [x] I09. Registrar `Cookies` como esquema predeterminado de autenticación, challenge y forbid, manteniendo las rutas locales de inicio y acceso denegado.
- [x] I10. Registrar `OpenIdConnect` con `SignInScheme = Cookies`, Authority y credenciales del servidor; fijar `/signin-oidc` y `/signout-callback-oidc` como callbacks del middleware.
- [x] I11. Configurar `ResponseType = code`, `UsePkce = true`, scopes `openid profile email`, `RequireHttpsMetadata = true`, `MapInboundClaims = false` y `GetClaimsFromUserInfoEndpoint = false`.
- [x] I12. Conservar validaciones de firma, emisor, audiencia, vigencia, nonce, state y correlación; usar descubrimiento de claves y negociación PAR según metadatos, sin exigir PAR al proveedor.
- [x] I13. Crear `OpenIdConnectEventsHandler` o un adaptador equivalente en Infrastructure para concentrar claims y fallos técnicos con dependencias por constructor.
- [x] I14. Configurar `NameClaimType = name` y `RoleClaimType` con `RolesClaimType`; conservar `iss` y `sub` como identidad externa, sin usar correo o nombre para otorgar roles.
- [x] I15. Adaptar el array del claim de roles ya validado a claims individuales exactos `Administrador` y `Operador`, eliminando duplicados sin interpretar el array completo como un nombre de rol.
- [x] I16. Descartar valores desconocidos o malformados y preservar autenticación sin permisos cuando no haya roles reconocidos; no inferir privilegios de formularios ni metadata editable.
- [x] I17. Conservar `UsuarioActualService` como implementación scoped de `IUsuarioActualService`, basada en el principal HTTP y con resultado falso fuera de una petición autenticada.

### 3.4. Cookie, tokens y errores

- [x] I18. Cambiar el nombre de cookie a `SolarUY.Sesion.Oidc` para invalidar la autenticación local anterior y preparar la eliminación de la cookie antigua al completar inicio o cierre.
- [x] I19. Configurar cookie no persistente, 30 minutos, sin renovación deslizante, `UseTokenLifetime = false`, `HttpOnly`, `Secure = Always` y `SameSite = Lax`.
- [x] I20. Conservar los ajustes de cookies temporales de correlación y nonce requeridos por OIDC, sin imponerles globalmente `SameSite = Strict`.
- [x] I21. Preservar únicamente el `id_token` en las propiedades protegidas del ticket para logout, por ejemplo filtrando los tokens en `OnTicketReceived` después de `SaveTokens`; no guardar access ni refresh tokens innecesarios.
- [x] I22. Manejar cancelación y fallos remotos con códigos internos y redirección local controlada; impedir que una respuesta inválida cree una nueva sesión o genere un bucle de challenges.
- [x] I23. Revisar logs y configuración de diagnósticos para no exponer secreto, token, código de autorización, PII ni `error_description`; mantener también las protecciones de diagnóstico MySQL existentes.
- [x] I24. Definir la conservación y protección de claves de Data Protection para despliegue; documentar almacenamiento compartido y nombre de aplicación común si hay varias instancias, sin agregar infraestructura de despliegue ajena al alcance.

### 3.5. Composición HTTP y retiro del acceso local

Estas tareas se ejecutan en el mismo tramo de integración que el retiro de credenciales, para no dejar referencias a tipos eliminados.

- [x] I25. Adaptar `POST /cuenta/autenticar` para validar antifalsificación y ejecutar un challenge explícito de `OpenIdConnect`, dejando de leer nombre y contraseña.
- [x] I26. Validar el retorno recibido antes de guardarlo en las propiedades protegidas de autenticación; aceptar destinos locales seguros y usar `/` ante externos, valores malformados o rutas de autenticación que generen ciclos.
- [x] I27. Conservar el límite de diez intentos por minuto por origen y respuesta 429 en el POST de inicio; excluir los callbacks de esa política y de la antifalsificación de formularios locales.
- [x] I28. Adaptar `POST /cuenta/cerrar-sesion` para exigir autenticación y antifalsificación y eliminar la cookie local antes de completar el cierre remoto.
- [x] I29. Ejecutar logout OIDC mediante el endpoint descubierto y `id_token_hint`, con retorno a `/signout-callback-oidc` y destino final `/`.
- [x] I30. Manejar fallos locales al preparar el logout para que no se conserve la cookie; proporcionar un resultado controlado y documentar que una caída posterior en el sitio externo puede impedir el retorno al navegador.
- [x] I31. Retirar `CredencialesService`, `AutenticacionOptions`, `UsuarioConfigurado`, sus registros de hash y la validación de `Autenticacion:Usuarios`; limpiar imports y referencias sin borrar configuración privada ajena al cambio.
- [x] I32. Verificar que no exista fallback local ni promoción automática del primer usuario y que no se soliciten `offline_access`, Management API o audience de una API no utilizada.
- [x] I33. Revisar el orden del pipeline: forwarded headers confiables si aplica, redirección HTTPS, autenticación, autorización, rate limiting y antifalsificación; no habilitar confianza general en proxies ni CORS abierto.
- [x] I34. Ejecutar compilación, pruebas de Domain y ArchUnitNET; comprobar ausencia de cambios en esquema y migraciones MySQL y registrar la salida de capa.

Resultado esperado: identidad externa integrada en el servidor, sesión local protegida y eliminación del mecanismo de contraseñas locales. Las pruebas reales con Auth0 quedan pendientes hasta completar el proveedor y las pantallas.

## 4. Presentation

Dependencia: I34. Referencias: secciones 4.1–4.2 de `specs.md`. Mantener el alta SSR y componentes independientes de Infrastructure.

- [x] V01. Reemplazar los campos de usuario y contraseña de `IniciarSesion.razor` por un botón «Iniciar sesión con proveedor externo».
- [x] V02. Conservar formulario POST a `/cuenta/autenticar`, token antifalsificación y navegación completa con enhanced navigation deshabilitada.
- [x] V03. Capturar y enviar `ReturnUrl` desde la página sin ejecutar redirecciones del lado del componente; dejar la validación definitiva en el endpoint I26.
- [x] V04. Mostrar mensajes diferenciados de cancelación y fallo técnico a partir de códigos internos conocidos, sin renderizar detalles libres del proveedor.
- [x] V05. Mostrar un estado de sesión en el inicio o menú con nombre disponible o etiqueta neutral y roles reconocidos; permitir que Operador y usuarios sin roles concluyan el ingreso en `/`.
- [x] V06. Conservar el enlace de alta dentro de `AuthorizeView Roles="Administrador"` y el formulario POST de cierre con antifalsificación y navegación completa.
- [x] V07. Mantener `[Authorize(Roles = "Administrador")]` en `AltaParqueSolar.razor` y su consumo exclusivo de `IParqueSolarService`.
- [x] V08. Preservar campos, validaciones, errores y confirmación del alta con ID y `0 MW`, sin duplicar decisiones del servicio ni introducir lógica de negocio en Razor.
- [x] V09. Conservar `/cuenta/acceso-denegado` con mensaje de falta de permiso y enlace al inicio; evitar challenges automáticos para identidades autenticadas sin el rol.
- [x] V10. Conservar `AddCascadingAuthenticationState` y `AuthorizeRouteView`, comprobando la continuidad del estado en páginas y menú SSR.
- [x] V11. Confirmar que los componentes afectados sigan en SSR; si un componente WebAssembly necesita estado visible, transferir solamente nombre y roles mediante el mecanismo de Blazor, sin trasladar tokens ni permisos de servidor al cliente.
- [x] V12. Verificar que Presentation no referencie clases concretas de Infrastructure y que los recursos públicos no contengan opciones privadas, secretos ni tokens.
- [x] V13. Comprobar que los errores de antifalsificación permitan recargar y que el vencimiento de sesión solicite un nuevo ingreso sin afirmar que también venció el SSO externo.
- [x] V14. Ejecutar compilación, pruebas de Domain y ArchUnitNET; registrar la salida de capa.

Resultado esperado: acceso externo y cierre utilizables, estado de sesión visible y alta protegida con el mismo comportamiento funcional.

## 5. Documentación y limpieza

Dependencias: decisiones e implementación de Infrastructure y Presentation. No reescribir requerimientos ni especificaciones históricas.

- [x] C01. Crear `arnes/prompting/req003_implementacion_openID/configuracion.md` con el flujo de autenticación y las versiones efectivamente verificadas.
- [x] C02. Documentar aplicación de Auth0, conexión, URLs exactas, roles y código completo de la Post Login Action, incluyendo publicación y vinculación al flujo.
- [x] C03. Documentar la preparación del administrador inicial, diferenciando el rol de SolarUY del acceso al Dashboard y sin publicar credenciales.
- [x] C04. Documentar la ampliación del archivo local y variables de entorno, su precedencia y exclusión de Git, conservando la conexión MySQL y explicando la situación real de User Secrets.
- [x] C05. Incluir comandos de restauración y arranque desde la raíz con el perfil HTTPS; remitir a la preparación MySQL existente si falta el esquema, sin generar otra migración por este requerimiento.
- [x] C06. Explicar inicio, cierre, comprobación de ambos roles y actualización de permisos mediante una nueva autenticación; documentar la vigencia de 30 minutos y el alcance del cierre externo.
- [x] C07. Agregar diagnóstico breve de Authority y secreto incorrectos, callback o logout no permitido, claim ausente, Action no vinculada, certificado local y correlación.
- [x] C08. Actualizar el inicio rápido del `README.md` raíz para utilizar acceso externo y enlazar la guía nueva, retirando la preparación de hashes de la ruta operativa vigente.
- [x] C09. Revisar `configuracion.md` y `referencia_tecnica.md` de `req002` para señalar el acceso local como antecedente y remitir a `req003`, conservando las indicaciones de MySQL.
- [x] C10. Identificar los arneses históricos dependientes del login local y documentar qué casos se sustituyen por la aceptación nueva; no presentarlos como validación actual de OIDC ni mantener un bypass para hacerlos funcionar.
- [x] C11. Buscar referencias activas a `CredencialesService`, `Autenticacion:Usuarios`, `UsuarioConfigurado`, hashes y formulario de contraseñas; distinguir referencias históricas de instrucciones o código que deban retirarse.
- [x] C12. Preparar el recorrido reproducible de aceptación HTTP y navegador y su registro de resultados en `artifacts/req003`, con datos aislados y sin unit tests de capas externas.

## 6. Aceptación funcional

Dependencias: V14, entorno MySQL de aceptación y E01–E09 para los casos externos. Registrar para cada caso resultado, entorno y evidencia depurada. Se pueden ejecutar controles locales antes de disponer de Auth0; el resto queda explícitamente pendiente.

### 6.1. Acceso externo y permisos

- [x] F01. Iniciar el servidor con la configuración privada nueva, sin `Autenticacion:Usuarios`, y comprobar que las opciones incompletas fallan con un mensaje seguro y útil.
- [x] F02. Completar Universal Login real como Administrador y comprobar retorno, cookie nueva, navegación y recarga; verificar que las credenciales se ingresan exclusivamente en el proveedor.
- [x] F03. Confirmar que la cuenta administradora y su rol existían antes de ese primer ingreso; registrar evidencia sin contraseñas ni tokens completos.
- [x] F04. Registrar un parque como Administrador y comprobar la confirmación con ID y `0 MW` y el mismo registro persistido en MySQL.
- [x] F05. Completar ingreso como Operador, comprobar estado visible y enlace de alta oculto, e intentar GET y POST directos al alta; deben rechazarse sin escrituras.
- [x] F06. Ingresar con ambos roles y comprobar que el alta se permite por pertenecer a Administrador, sin agregar jerarquía implícita de roles.
- [ ] F07. Verificar con identidades y claims del entorno de prueba que ausencia de roles, valores desconocidos, malformados o con capitalización distinta no concedan permisos de alta.
- [x] F08. Revisar el orden de validaciones del servicio y comprobar que anónimos y roles incorrectos no alcanzan consultas ni escrituras del repositorio; complementar la inspección con observación del flujo sin habilitar logs de datos sensibles.

### 6.2. Retornos, callbacks y solicitudes locales

- [x] F09. Iniciar desde una ruta protegida y comprobar que un `ReturnUrl` local válido se conserva al completar el acceso.
- [x] F10. Probar retornos externos, relativos a otro host, malformados o dirigidos a los endpoints de autenticación; comprobar retorno a `/` sin redirección abierta ni ciclos.
- [x] F11. Cancelar o rechazar el ingreso desde el proveedor y comprobar mensaje controlado, ausencia de una nueva sesión y posibilidad de reintentar manualmente.
- [x] F12. Manipular state o correlación y repetir un código ya consumido en el entorno aislado; comprobar rechazo sin una nueva cookie de sesión ni filtraciones en respuesta o logs.
- [x] F13. Enviar POST de inicio sin token antifalsificación o con token inválido; comprobar 400 y ausencia de challenge externo.
- [x] F14. Enviar POST de cierre autenticado sin token antifalsificación o con token inválido; comprobar 400 y conservación de la sesión legítima.
- [x] F15. Superar el límite de inicio de sesión y comprobar 429; verificar por separado que los callbacks legítimos no queden sujetos a ese límite ni a antifalsificación de formularios locales.

### 6.3. Sesión y cierre

- [x] F16. Comprobar atributos de `SolarUY.Sesion.Oidc`, ausencia de persistencia y expiración a los 30 minutos sin renovación deslizante; validar también el comportamiento al vencer.
- [x] F17. Presentar una cookie válida del mecanismo anterior capturada únicamente en el entorno de prueba o revisar su incompatibilidad con el nombre nuevo; confirmar mediante petición que la cookie anterior no autentica y que el flujo nuevo la elimina cuando corresponde.
- [x] F18. Inspeccionar en el servidor, sin volcar tokens a evidencias, que el ticket conserve solamente el ID token necesario para logout; verificar tamaño de cookie y ausencia de tokens en WebAssembly o almacenamiento JavaScript.
- [x] F19. Ejecutar un cierre válido y comprobar eliminación local, logout en Auth0, callback permitido y retorno a `/`; una ruta protegida debe solicitar nuevo ingreso.
- [x] F20. Provocar un fallo de proveedor durante el cierre en el entorno aislado y comprobar que la cookie local quede eliminada; registrar la diferencia entre fallo manejable por SolarUY y navegación que permanece en el proveedor inaccesible.
- [x] F21. Provocar indisponibilidad del proveedor y comprobar que impide nuevos ingresos sin fallback local, mientras una cookie previa válida conserva su vigencia hasta expirar.
- [x] F22. Cambiar el rol de una cuenta de prueba, comprobar el comportamiento de su cookie vigente y verificar que cerrar e ingresar nuevamente refleje la asignación actualizada; restaurar el estado de prueba al terminar.

### 6.4. Regresión, documentación y evidencias

- [x] F23. Repetir con identidad externa los casos de campos obligatorios, nombre de 100 y 101 caracteres, normalización y duplicados, conservando errores funcionales y datos del formulario.
- [x] F24. Verificar que los errores MySQL del destino aislado mantengan el mensaje genérico, sin éxito falso ni exposición de SQL o credenciales.
- [x] F25. Recorrer inicio, acceso denegado y cierre con teclado, comprobando etiquetas, mensajes y navegación completa sin dejar la interfaz con estado de sesión desactualizado.
- [x] F26. Revisar respuestas, URLs, recursos públicos y evidencias para descartar secretos, códigos de autorización, tokens y detalles libres del proveedor.
- [x] F27. Seguir `configuracion.md` desde una configuración de aceptación para comprobar que permite preparar el administrador, iniciar y verificar los roles sin editar código.
- [x] F28. Registrar los resultados completos de aceptación real y las comprobaciones que sigan pendientes; limpiar solo recursos y datos de prueba identificados, conservando las evidencias sin secretos.

## 7. Validación final y cierre

- [x] R01. Ejecutar compilación, pruebas de Domain y ArchUnitNET tras el último cambio de código; corregir fallos en el código y repetir únicamente lo afectado por nuevas correcciones.
- [x] R02. Revisar dependencias y cambios finales para confirmar las fronteras de capas, ausencia de paquetes de autenticación privados en el cliente y continuidad del esquema MySQL.
- [x] R03. Comparar el inventario P02 y comprobar que los diagramas y documentos fuente no hayan cambiado durante la implementación.
- [x] R04. Revisar el conjunto final de archivos para excluir secretos, configuración local, evidencias ignoradas y cambios preexistentes ajenos a la tarea.
- [x] R05. Completar la matriz de trazabilidad siguiente con resultados y referencias de evidencia; dejar pendientes los casos no ejecutados aunque compile la solución.
- [x] R06. Actualizar las casillas y el registro de ejecución y entregar el enlace a `configuracion.md`, controles ejecutados y limitaciones reales; declarar el requerimiento completo únicamente con los criterios externos verificados.

## Comandos de control

Ejecutar desde la raíz al finalizar cada capa y tras cambios de código según las reglas:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

La restauración y ejecución para aceptación, una vez preparados los datos privados y HTTPS:

```powershell
dotnet restore SolarUY.slnx
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

No se requiere generar ni aplicar nuevas migraciones por la integración OIDC. Si el destino de aceptación necesita el esquema existente, seguir la guía MySQL de `req002` sobre ese destino aislado. Los comandos de este plan corresponden a implementación y aceptación; no se ejecutan como parte de la generación del documento.

## Matriz de trazabilidad

| Criterio o decisión | Tareas principales | Resultado y evidencia |
| --- | --- | --- |
| CA1: acceso OpenID Connect real | E01–E03, I01–I12, I25, V01–V04, F01–F02 | Universal Login, callback, recarga y reinicio comprobados con Auth0. E01 conserva pendiente la separación de producción. |
| CA1: Administrador y Operador | A01–A05, E04, E06–E08, I14–I17, V05–V09, F04–F08 | Administrador y ambos roles crean parques; Operador y sin roles denegados por GET/POST. F07 parcial por omisión solicitada. |
| CA2: administrador anterior al uso | E05, E08–E10, C03, F03–F04 | Usuario confirmó asignación anterior al primer ingreso; login y alta reales con ID y 0 MW contrastados en MySQL. |
| Adaptación del acceso | I18, I25–I33, V01–V14, C08–C11, F01, F17 | Contraseñas locales retiradas, SSR conservado; nombre antiguo no autentica y se elimina. proxy-cookie-anterior.json. |
| Retornos y protocolo | I12–I13, I22–I23, I26–I27, V03–V04, F09–F15 | Retornos reales seguros, cancelación, callback repetido, CSRF y 429 con callback legítimo comprobados. retornos-reales.json y proxy-*.json. |
| Sesión y cierre | I18–I24, I28–I30, C06, F16–F22 | Solo ID token, cookie 3479 bytes, vencimiento real 30 min sin renovación; logout Auth0 y fallo local controlado. sesion-resumen.json, caducidad-antes.json, proxy-comprobar-caducidad.json. |
| Secretos y configuración | P07, E09, I03–I08, I23–I24, V12, F01, F26, R04 | Configuración privada real conservada, arranque sin usuarios locales. Revisión de 58 evidencias y 57 fuentes sin secretos ni valores de protocolo en evidencias (privacidad-final.json). |
| Alta y MySQL | D01–D04, A02–A06, V07–V08, F04–F08, F23–F24 | Tres parques sintéticos contrastados, límites/duplicados/obligatorios y error MySQL correctos; esquema temporal eliminado, base habitual intacta. |
| Guía operativa | C01–C12, F27 | Usuario preparó proveedor y cuentas siguiendo guía; acceso y roles verificados sin editar código por cuenta. configuracion.md. |
| Arquitectura y calidad | P03, D04, A06, I34, V14, R01–R03 | Build final correcto, Domain 21/21, arquitectura 17/17; cierre-*.log. Seis hashes intactos en integridad-final.json. |
| Aceptación y seguimiento | F01–F28, R05–R06 | 28 controles HTTP locales más aceptación externa documentada en aceptacion.md. E01/F07 pendientes explícitos; recursos propios retirados (limpieza-final.json). |

## Registro de ejecución

Estado inicial (antes de implementar): plan generado; todas las tareas de implementación y aceptación están pendientes. No se han ejecutado los comandos de control, configurado Auth0, modificado código de aplicación ni comprobado credenciales como parte de esta planificación.

Durante la implementación registrar por etapa: tareas completadas, fecha, entorno, controles y resultados, referencias de evidencia sin secretos, verificaciones pendientes y causas concretas. No marcar como exitoso un escenario externo que no se haya podido ejecutar.

### 2026-10-20 — Preparación y Domain
- Preexistente: req003 completa sin seguimiento en Git; no había cambios en archivos versionados. Inventario SHA256 y estado en artifacts/req003.
- SDK 10.0.400, runtime 10.0.11. Perfil HTTPS confirmado; certificado confiable pendiente de comprobación fuera del entorno restringido.
- MySQL privado existente preservado; no se ha identificado un destino aislado. El usuario confirma que no dispone de Auth0: E01–E10 y aceptación externa pendientes.
- Compilación base correcta (12 advertencias NU1903 preexistentes), Domain 21/21 y arquitectura 17/17. Logs base-*.log. El primer intento restringido no completó; se repitió fuera de la restricción, sin modificar pruebas.
- Domain revisado sin cambios; esta misma salida valida D04. Application revisada sin cambios, control de salida a continuación.


### 2026-10-20 — Application
- Sin cambios de código: contrato, autorización previa al repositorio y reglas del alta conservados.
- application-build.log correcto, application-domain.log 21/21, application-quality.log 17/17.
- Comprobación fuera de la restricción confirma certificado localhost confiable. P05 pendiente: MySQL existente sin destino aislado autorizado.


### 2026-10-20 — Infrastructure
- OIDC 10.0.12 e IdentityModel 8.19.2 restaurados solo en servidor. Sin cambios de esquema, migraciones ni cargador MySQL.
- infrastructure-build.log: correcto, sin advertencias C# nuevas; persisten NU1903 de línea base. Domain 21/21 y arquitectura 17/17 en infrastructure-*.log.
- Se retiran las credenciales locales sin modificar appsettings.Local.json privado. Cookie nueva, sesión de 30 minutos, solo ID token protegido, challenge y logout explícitos, antifalsificación, límite y errores genéricos.
- Data Protection: nombre común SolarUY; en producción conservar claves fuera del contenedor efímero, cifradas en reposo con ACL de la cuenta de servicio y respaldo protegido. Varias instancias comparten anillo y nombre de aplicación. No cambiar el identificador al reiniciar. El aprovisionamiento del almacenamiento pertenece al despliegue; la guía lo explicará.
- Pendientes E01–E10: el usuario confirma ausencia de Auth0. No se concede aceptación externa por compilación.


### 2026-10-20 — Presentation
- Inicio externo SSR, ReturnUrl oculto, mensajes internos fijos y nombre/roles visibles; alta y servicio preservados. Sin serialización de tokens o autenticación en WASM.
- presentation-build.log correcto; presentation-domain.log 21/21; presentation-quality.log 17/17. Solo NU1903 preexistentes.
- V13 verificado por configuración y manejo explícito de POST 400; la caducidad real y el recorrido autenticado de teclado siguen pendientes en F16/F25.

### 2026-10-20 — Documentación y revisión final
- configuracion.md incluye Auth0, Action completa, administrador, precedencia, secretos, HTTPS, Data Protection, sesión/cierre y diagnóstico. aceptacion.md detalla F01–F28 con evidencias seguras y pendientes reales.
- README y guías operativas req002 actualizados; hashes y arneses locales anteriores identificados como históricos. No quedan validadores, registros, formulario de contraseñas ni referencias activas en el código servidor/cliente.
- La aceptación HTTP detectó que la respuesta 429 vacía se reejecutaba como POST en /not-found y terminaba en 400. Se agregó cuerpo fijo al rechazo, preservando 429. El ID token de logout se transmite en Parameters transitorio para no duplicarlo en state.
- Tras las correcciones: final-build.log correcto, final-domain.log 21/21 y final-quality.log 17/17; sin advertencias nuevas. Tests estructurales intactos. Aceptación HTTP final en curso.
- R02–R04: paquete OIDC ausente del cliente y sus assets, Domain/Application/persistencia/migraciones intactos; seis hashes de diagramas y documentos fuente coinciden (integridad-final.json). Solo cambios propios de código/documentación; configuración privada y artifacts ignorados. No se copió ningún secreto.

### 2026-10-20 — Resultado local y pendientes externos
- Recorrido HTTP final: 28/28 correctos, certificado HTTPS confiable, configuración aislada sin Autenticacion:Usuarios y sin acceso físico a MySQL. Evidencia: artifacts/req003/controles-locales.json y servidor-local.log. El proceso propio terminó; no se crearon parques ni recursos externos.
- F13 completo: POST sin token y con token inválido devuelve 400 sin challenge. F01, F12, F15 y F26 tienen controles locales parciales, pero permanecen sin marcar porque incluyen verificaciones reales adicionales.
- P05 sigue pendiente del destino MySQL aislado. E01–E10 y F01–F12/F14–F28 siguen pendientes conforme al detalle de aceptacion.md: faltan tenant, aplicación, secretos, administrador y cuentas de prueba. No se declara cumplido CA1 ni CA2.
- Registro y matriz de trazabilidad actualizados. Entrega local lista para configurar siguiendo configuracion.md; requisitos y specs originales conservados. La configuración privada existente no contiene OIDC y el arranque normal requerirá completarla según la guía.

### 2026-10-20 — Reanudación con Auth0 real
- Usuario confirma configuración OIDC, administrador asignado antes del primer ingreso y preparación de cuentas dedicadas Operador y sin roles. La aplicación Auth0 es la prevista para el sistema; no se alterarán sus ajustes globales ni identidades habituales para provocar fallos.
- Metadatos HTTPS reales correctos: issuer, Authorization Code, S256 y end_session_endpoint (proveedor-metadata.json). Universal Login y callback completados en navegador lateral con Administrador; navegación/recarga y reinicio conservan identidad. Claim de roles activo con namespace configurado, sin fallback local.
- Base MySQL nueva exclusiva de aceptación y migración existente aplicadas por gestionar_aceptacion_real.ps1; estado y origen registrados en entorno-real.json, base habitual preservada. Servidor usa configuración vacía más variables privadas, sin Autenticacion:Usuarios.
- Diagnóstico opcional de cookie incorporado en Infrastructure con inyección por constructor; registra únicamente atributos, cantidad/tamaño, duración y nombres de tokens a nivel Debug. Compilación correcta, Domain 21/21 y arquitectura 17/17 (continuacion-*.log). El primer build falló por el servidor propio abierto, se detuvo y se repitió correctamente.
- Cancelación real del consentimiento de Auth0 muestra mensaje cancelado y conserva únicamente la sesión previa; reintento manual completa el ingreso. No se cambió configuración del proveedor.

### 2026-10-20 — Alta y regresión con Administrador externo
- F04: Universal Login Administrador permite crear parque; ID 7ed7105a-9c3b-48ef-9f17-4cc62eb28586 y datos normalizados coinciden en UI/MySQL, confirmación 0 MW (parques-aceptacion.json).
- F23: nombre y ubicación de espacios rechazados por el servidor; duplicado por capitalización rechazado; 101 caracteres rechazados conservando datos; 100 aceptados. La base aislada contiene exactamente dos registros sintéticos.
- F24: trigger de rechazo temporal únicamente en esquema aislado. La UI muestra mensaje genérico, conserva formulario y no confirma éxito ni expone SQL. Conteo se mantiene en dos. Trigger retirado al terminar.
- F18: diagnóstico servidor de cookie real: 1 cookie, 3479 bytes, HttpOnly/Secure/SameSite Lax, no persistente, sin deslizamiento, duración 30 minutos, único token id_token y eliminación de cookie antigua. No se registran valores ni identidad. Recursos de cliente revisados sin tokens; expiración real aún pendiente en F16.
- Aceptación visual ejecutada con recursos del perfil Development/HTTPS; el arnés usa configuración vacía más variables privadas y base aislada, sin copiar secretos a evidencias.

### 2026-10-20 — HTTP autenticado y callback legítimo
- ProxyAceptacion.cs es un arnés separado compilado únicamente en artifacts, sin nuevos endpoints ni bypass en SolarUY. Reutiliza el certificado en el almacén de Windows sin exportar claves y mantiene cookies/callback exclusivamente en memoria. Solo guarda métricas y booleanos.
- F14: POST de cierre con sesión Administrador real, sin token e inválido: 400; GET protegido antes/después: 200; no se emite cookie nueva (proxy-csrf-cierre.json).
- F09/F15: retorno local al alta conservado en un ingreso real. Durante consentimiento pendiente se superó el límite y se obtuvieron respuestas 429; Accept completó callback legítimo y volvió al alta con Administrador dentro de la misma ventana (proxy-limite-inicio.json).
- sesion-resumen.json inspecciona ticket real sin identidad ni valores: emitida 22:54:10 UTC, vence 23:24:10 UTC, 30 minutos, no persistente, AllowRefresh false, solo id_token. La expiración real se comprobará sin modificar el reloj ni la duración.
- Revisión automática rechazó exportar el certificado con clave privada a PEM. Esa operación no se ejecutó; el arnés alternativo usa directamente el almacén del sistema y no genera copias de claves.

### 2026-10-20 — Retornos, protección de protocolo y cierre real
- F10: seis retornos (HTTPS externo, //otro-host, separador invertido, porcentaje malformado, /cuenta/iniciar-sesion y /signin-oidc) completaron ingreso Auth0 real y terminaron en / con sesión válida, sin redirección abierta ni ciclos.
- F12: callback genuino consumido capturado únicamente en memoria del proxy. Repetición sin correlación, state alterado y código ya canjeado: los tres redirigen al error interno y no emiten cookie nueva (proxy-callback-repetido.json). Ningún código ni token se escribe en evidencias.
- F17: ticket real válido aceptado con nombre actual e ignorado al presentarlo bajo nombre anterior; adaptación por nombre inspeccionada y borrado de cookie anterior observado en sign-in. No se fabricó una identidad (proxy-cookie-anterior.json).
- E03/F19: POST de cierre válido ejecutado con teclado; Auth0 completa end_session, callback permitido y retorno /. Sesión local eliminada. Abrir el alta pide nuevo ingreso y Auth0 vuelve a solicitar credenciales, comprobando cierre del SSO de este proveedor.
- Nuevo recorrido comienza anónimo en el alta protegida y conserva el retorno local; pendiente completar ingreso de Operador en el navegador.

### 2026-10-20 — Operador, ambos roles y corrección de claims
- F05/F08: Operador real ve su estado, no ve el enlace y GET/POST directos al alta se deniegan. Base aislada mantiene dos registros. Inspección del servicio confirma rechazos antes de consultas/persistencia (proxy-denegacion-directa.json).
- Se corrigió LeerRoles para distinguir ClaimValueTypes.String de JsonClaimValueTypes.JsonArray: un rol desconocido escrito como JSON ya no puede convertirse en Administrador. No se cambian validaciones criptográficas, contratos ni Domain. Compilación correcta, Domain 21/21 y arquitectura 17/17 en claims-*.log.
- Cambio de roles realizado manualmente por el usuario solo en la cuenta de prueba: la cookie Operador siguió sin alta; una nueva autenticación recibió ambos roles y permitió registrar el parque de ambos roles (ID d177dd16-165f-4868-ba9a-ed692c122987, 0 MW). Captura alta-ambos-roles.jpg excluye identidad. F06 completo; pendiente terminar restauración y cierre/reingreso de F22.

### 2026-10-20 — Restauración de roles y navegación por teclado
- F22: se retiró Administrador de la cuenta dedicada; su cookie vigente mantuvo ambos roles hasta cerrar. Cierre OIDC y nuevo ingreso manual produjeron únicamente Operador y acceso denegado al alta. Usuario confirma que la cuenta quedó restaurada a Operador.
- F25: formularios con etiquetas asociadas, acceso denegado → Volver al inicio y cierre recorridos con Enter; estado actualizado por navegación completa y recarga. Inicio externo y formularios SSR se comprobaron con sesión real; no se guardan credenciales.
- Continúa la aceptación con la cuenta sin roles preparada por el usuario. No se altera la Action global ni se usa Management API.

### 2026-10-20 — Cierre de aceptación real
- E10: responsable es el usuario que configuró Auth0 y accedió con su cuenta existente; credenciales ingresadas únicamente en Universal Login. Futuras entregas mediante invitación/restablecimiento del proveedor según configuracion.md.
- F07 parcial: cuenta sin roles válida, enlace oculto y GET/POST del alta denegados (sin-roles-denegacion.json). El usuario pidió omitir desconocidos, capitalización y texto malformado; no se crearon esos roles ni se alteró la Action global. Casilla sin marcar.
- E01 permanece sin marcar: se usó la aplicación prevista para el sistema; no se certificó un recurso separado de producción. No se provocaron fallos en el proveedor ni se modificaron usuarios habituales.
- F16: cookie genuina emitida 22:54:10 UTC, vencimiento 23:24:10 UTC. A las 23:22 devolvió 200 sin renovación; a las 23:24:56 devolvió 302 al nuevo ingreso, sin renovación. No se cambió reloj ni duración (caducidad-antes.json y proxy-comprobar-caducidad.json).
- F20/F21: Authority inaccesible solo en el proceso aislado impidió nuevos ingresos mientras conservó la sesión previa. Cierre eliminó cookie local y mostró aviso controlado. Fallo al preparar logout comprobado; una caída después de navegar al proveedor puede detener allí el navegador, según guía. Configuración normal restaurada y nueva autenticación sin roles completada; finalmente logout normal dejó sesión cerrada.
- F26: revisión de fuentes, respuestas, recursos y evidencias sin secretos ni valores de protocolo persistidos. Las URLs sensibles y cookies del arnés se mantuvieron solo en memoria; proxy finalizado para liberar esa memoria. privacidad-final.json.
- F27: el usuario siguió la preparación del proveedor, configuró Action y cuentas e ingresó como Administrador/Operador/sin roles. Se comprobaron alta y permisos reales sin cambios de código por usuario.
- F28: tres parques sintéticos y su esquema exclusivo eliminados, servidor y proxy propios detenidos mediante PID y hora de inicio. Base habitual, archivo privado y cuentas Auth0 conservados; Operador restaurado. Evidencias depuradas conservadas en artifacts/req003.
- Control final después del último cambio: cierre-build.log correcto, cierre-domain.log 21/21, cierre-quality.log 17/17; solo 12 advertencias NU1903 preexistentes. Sin cambios en tests ni migraciones. Inventario: seis hashes coinciden.
- La aceptación integral no se declara completa: quedan E01 y F07. Las demás tareas tienen evidencia local o externa registrada; ya no se consideran pendientes por ausencia de Auth0.
