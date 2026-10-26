# Comprobaciones auxiliares de req004

Estos auxiliares complementan los controles de Domain y ArchUnitNET. Usan MySQL, entidades, servicios, repositorios y componentes reales. Las identidades locales y las cookies propias del host auxiliar **no acreditan aceptación con OIDC real**.

Desde la raíz, suministrar la conexión del servidor de pruebas mediante `ConnectionStrings__SolarUy` en el entorno. No escribir credenciales en comandos guardados ni evidencias. Se conserva `SolarUY/appsettings.Local.json`.

```powershell
dotnet run --project arnes/prompting/tests/req004/VerificarAltaNodo.csproj
dotnet run --project arnes/prompting/tests/req004/VerificarAltaNodo.csproj -- ssr
```

El primer comando crea una base nueva `solaruy_req004_<12 caracteres>`, aplica la migración inicial, prepara dos parques y aplica la migración incremental. Comprueba conservación de datos, autorización antes de repositorios, campos obligatorios, normalización, coordenadas, grafos e identidades recargadas, ausencia de unicidad funcional, índices y FKs, fallo antes del commit, limpieza del contexto, recuperación, concurrencia, desaparición del parque y cancelación. Solo escribe el nombre de la base en `artifacts/req004/destino-aislado.json`.

El segundo comando exige ese destino generado y lo reutiliza. Inicia temporalmente un host en `http://127.0.0.1:8184` con los componentes SSR y servicios reales, autenticación por cookies exclusivamente auxiliar y registros de diagnóstico desactivados. Comprueba GET/POST por rol, antifalsificación, tres secciones, conservación de entradas, campos vacíos, conversiones numéricas, guardado, destino de ruta frente a IDs adicionales del POST, parque inexistente, GUID malformado y recuperación de fallos. Al no renderizarse formulario para un parque inexistente, SSR rechaza su POST con 400; el servicio devuelve `ParqueNoEncontrado` ante la misma situación.

Para inspección con navegador:

```powershell
dotnet run --project arnes/prompting/tests/req004/VerificarAltaNodo.csproj -- ssr preview
```

El host permanece activo hasta Ctrl+C. `/_auxiliar/sesion/Administrador` y `/_auxiliar/sesion/SinRol` crean únicamente sesiones locales de prueba; estos endpoints no existen en SolarUY. La configuración y autenticación de producción no cambian. Las escrituras y los fallos controlados se limitan a la base generada. El auxiliar conserva la base para inspección; cualquier retiro posterior debe dirigirse exclusivamente al destino registrado.

Resultados de la ejecución de implementación: `artifacts/req004/resultados.md`. No guardar HTML con tokens de antifalsificación ni valores de cookies. Las evidencias contienen únicamente criterios, resultados, nombres de destinos y capturas depuradas.

## Aceptación con OIDC real

Con SolarUY real activo en `https://localhost:7178`, configurado por entorno hacia la base aislada registrada:

```powershell
dotnet run --project arnes/prompting/tests/req004/VerificarAltaNodo.csproj -- oidc
```

Abrir `https://localhost:8186/` en el mismo navegador y host donde el usuario completó el ingreso OIDC. El auxiliar escucha únicamente en localhost, valida su propio formulario antifalsificación y reenvía las cookies recibidas exclusivamente al servidor fijo de SolarUY. Cookies, tokens y HTML protegido permanecen en memoria; las evidencias contienen solo criterios y resultados. No emite identidades ni modifica la autenticación del sistema.

«Verificar Administrador» prepara parques propios mediante el formulario real, ejecuta los POST y comprueba MySQL con contextos nuevos. Incluye campos ausentes/vacíos/espacios, coordenadas, antifalsificación, manipulación del destino, concurrencia, fallo transaccional, recuperación y desaparición real del parque. Para esta última, un disparador temporal detiene el INSERT mediante un bloqueo nombrado mientras otra conexión elimina el parque; al liberar el bloqueo se verifica la respuesta y ausencia de huérfanos. Los disparadores se retiran en `finally`.

Después de cerrar la sesión real e ingresar con la cuenta sin Administrador, «Verificar usuario sin rol Administrador» comprueba ausencia del botón, denegación de GET y POST aun con token antifalsificación de esa identidad, y ausencia de escrituras. Los resultados quedan en `oidc-admin.json` y `oidc-sin-rol.json`, dentro de artifacts. La navegación, foco, Cancelar y confirmación se comprueban además sobre el navegador de SolarUY.
