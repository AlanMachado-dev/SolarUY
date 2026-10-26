# Comprobaciones funcionales auxiliares mod002

Ejecutar desde la raíz. Se leen las credenciales de la configuración existente sin imprimirlas. Las escrituras solo se permiten en bases con prefijo `solaruy_mod002_`; la base habitual y `appsettings.Local.json` se conservan.

```powershell
dotnet run --project arnes/prompting/tests/mod002/VerificarMod002.csproj
dotnet run --project arnes/prompting/tests/mod002/VerificarMod002.csproj -- infra
dotnet run --project arnes/prompting/tests/mod002/VerificarMod002.csproj -- web
```

El primer comando crea y registra un destino nuevo en `artifacts/mod002/destino-aislado.json`. Ejecutar `infra` sobre ese destino nuevo: si se repite, crear otro destino primero. Comprueba migración compatible, columnas y colaciones reales, índices completos, consultas globales, conflictos de ambos tipos, reversión del grafo, limpieza del contexto y recuperación. Seis bases auxiliares comprueban duplicados exactos, recortados y longitudes incompatibles para ambos equipos sin alterar datos ni esquema. El modo `migraciones` permite repetir únicamente esas seis verificaciones. Las evidencias no contienen series ni credenciales.

`web` inicia un host auxiliar enlazado solo a `127.0.0.1:8192` con los componentes SSR y servicios reales. Comprueba formularios GET/POST, redirecciones, errores de campo, datos renderizados y permisos usando cookies auxiliares. Una barrera antes del guardado fuerza dos altas HTTP a completar ambas consultas previas; MySQL decide el ganador y se registra únicamente el código 1062. También comprueba un fallo de carga real ocultando y restaurando temporalmente una tabla del destino aislado. `web preview` mantiene el host disponible para inspección visual después de los controles. Detenerlo al terminar. No se modifica la autenticación de producción ni se prueban credenciales externas.

La migración debe ejecutarse sin escritores concurrentes, dado que MySQL confirma DDL implícitamente. Su comprobación previa utiliza una tabla temporal de sesión y un CHECK con nombre descriptivo; ningún ALTER se ejecuta si los datos son incompatibles. También bloquea series vacías o sin el recorte canónico de Domain, conservando los valores para revisión manual. No normaliza ni trunca datos históricos automáticamente.

Estas comprobaciones usan MySQL real y los componentes del sistema. Las identidades auxiliares, cuando se utilicen, no acreditan aceptación con el proveedor externo OIDC. Los resultados y limitaciones se registran en `artifacts/mod002/resultados.md` y no deben confundirse con verificaciones externas reales.
