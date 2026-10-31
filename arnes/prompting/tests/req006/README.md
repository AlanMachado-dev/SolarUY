# Verificación de req006

Ejecutar desde la raíz de SolarUY. Los auxiliares leen la configuración privada existente en memoria, cambian únicamente el nombre de base al destino validado `solaruy_req006_<12 caracteres>` de `artifacts/req006/destino-aislado.json` y nunca escriben en la base habitual. No modificar `appsettings.Local.json` ni guardar conexiones en evidencias.

Estos son auxiliares funcionales, no nuevas suites unitarias de Application, Infrastructure o Presentation. No sustituyen los tres controles obligatorios del repositorio.

## MySQL y SSR con identidades auxiliares

```powershell
dotnet run --project arnes/prompting/tests/req006/VerificarAltaPanel.csproj
dotnet run --project arnes/prompting/tests/req006/VerificarAltaPanel.csproj -- ssr
dotnet run --project arnes/prompting/tests/req006/VerificarAltaPanel.csproj -- final
```

Antes del primer comando, crear `artifacts/req006/destino-aislado.json` con una propiedad `Base` y un nombre nuevo exclusivo que cumpla el patrón anterior; la base no debe existir. El primer comando aplica la migración previa, prepara dos parques y tres nodos con equipos y después aplica la migración de paneles. Verifica conservación, autorización previa a persistencia, validaciones, cargas, unicidad concurrente, aislamiento, cantidades/capacidad, fallos reales de escritura, recuperación, desaparición concurrente del nodo y cancelación.

`ssr` reutiliza ese destino, levanta temporalmente los componentes y servicios reales en `http://127.0.0.1:8196`, usa exclusivamente cookies auxiliares propias y prueba permisos, antifalsificación, conversiones con cultura es-UY, campos manipulados, errores, redirección después de POST, recarga y regresión. `ssr preview` conserva ese host para inspección con navegador; sus endpoints `/_auxiliar/sesion/Administrador` y `/_auxiliar/sesion/Operador` no existen en SolarUY. Estas identidades no acreditan OIDC externo.

`final` comprueba el motor InnoDB y las restricciones físicas, colación binaria, caracteres suplementarios, distinción de acentos y rollback real ante un fallo posterior al INSERT y anterior al commit. Requiere el destino preparado y agrega datos identificados de prueba.

## Aplicación y sesión OIDC reales

```powershell
dotnet run --project arnes/prompting/tests/req006/VerificarAltaPanel.csproj -- real
dotnet run --project arnes/prompting/tests/req006/oidc/VerificarAceptacion.csproj
```

`real` comprueba que el puerto 7178 está libre e inicia la aplicación original en `https://localhost:7178`, con su autenticación OIDC intacta y la conexión aislada suministrada por entorno únicamente al proceso hijo. No detiene procesos ajenos. El usuario inicia sesión manualmente en el navegador de la app.

El segundo comando ofrece un puente en `https://localhost:8197`. Debe abrirse en el mismo navegador y host donde el usuario ingresó a SolarUY. Valida su propio antiforgery y reenvía la sesión recibida exclusivamente al destino fijo `https://localhost:7178`; cookies, tokens y HTML protegido permanecen en memoria. No emite identidades ni cambia permisos. Usar «Verificar Administrador» con la cuenta real correspondiente y «Verificar usuario sin Administrador» después del cambio de cuenta realizado por el usuario.

La aceptación verifica alta, validación y entradas conservadas, antiforgery, manipulación del POST, duplicados, nombre compartido entre nodos, rutas inválidas, anónimo, concurrencia, regresión de alta de nodos, listados, totales y fallos reales con recuperación. Los fallos se producen únicamente en el destino aislado: renombrado transitorio de la tabla de paneles para carga y trigger de rechazo para escritura; ambos se revierten en `finally`. La navegación con teclado, Cancelar, foco y diseño se comprueban adicionalmente en el navegador real.

Los archivos `*.pid` identifican exclusivamente los procesos propios de verificación. Al concluir, detener esos procesos, retirar las bases temporales identificadas que ya no se necesiten y conservar únicamente las evidencias depuradas en `artifacts/req006/`, excluido de Git. No guardar HTML protegido, valores de cookies, credenciales ni excepciones completas.

El puente admite `-- limpiar-anteriores` para retirar solo los destinos descartados registrados en `destinos-anteriores.txt`, conservando el actual; `-- limpiar-todos` incluye también el destino actual y se usa únicamente después de detener los hosts y finalizar la aceptación. Cada nombre debe satisfacer el patrón exclusivo de req006 y ser distinto de la base habitual. La limpieza queda registrada sin datos de conexión.
