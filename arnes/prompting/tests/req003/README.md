# Verificación funcional auxiliar de req003

Este auxiliar usa los servicios, repositorio, mapeo MySQL y componentes reales. Las identidades de su contexto local **no son ingresos OIDC**. No sustituye la aceptación con Administrador, Operador y usuario sin roles del proveedor externo, ni la navegación en una sesión SSR autenticada.

Desde la raíz, después de compilar:

```powershell
dotnet run --project arnes/prompting/tests/req003/VerificarConsultas.csproj
```

Lee la conexión por los mecanismos existentes, sin imprimirla ni almacenarla. Necesita un servidor de pruebas con permiso para crear una base nueva; se puede indicar mediante `ConnectionStrings__SolarUy` en el entorno. No escribir credenciales en comandos guardados ni evidencias. Cada ejecución genera un destino `solaruy_req003_aceptacion_<12 caracteres>`; crea el esquema aplicando la migración existente únicamente allí. No consulta ni modifica las tablas de la base habitual.

Comprueba lista vacía, alta autorizada, orden, lectura sin seguimiento, correspondencia de datos entre listado y detalle, capacidad de dominio, acceso a las consultas con las tres identidades locales, rechazo del alta sin Administrador, IDs inexistentes, propagación de cancelación y errores. Para comprobar los errores renombra temporalmente una tabla exclusivamente dentro del destino recién creado y la restaura en `finally`.

Genera HTML con los componentes reales en `artifacts/req003/`. El modo de vista previa sirve exclusivamente esos documentos y las hojas de estilo del proyecto:

```powershell
dotnet run --project arnes/prompting/tests/req003/VerificarConsultas.csproj --no-build -- preview
```

Abrir `http://localhost:8183/parques-solares`. Permite comprobar la superficie del enlace por fila, Tab, Enter, foco, datos y enlaces de vuelta. También están disponibles `/vacio`, `/error-listado`, `/error-detalle` y el detalle con GUID vacío. La vista previa indica explícitamente que no hay una sesión OIDC. No implementa ni altera la autenticación de SolarUY.

El auxiliar conserva la base generada para inspección. En la ejecución de implementación se utilizó una instancia MySQL independiente, ligada a `127.0.0.1:13306`, con directorio `artifacts/req003/mysql-aislado`; se detuvo y se retiró su directorio temporal al cerrar las comprobaciones. No se modificó `appsettings.Local.json`.

Resultados de esta ejecución y limitaciones: `artifacts/req003/resultados.md`, excluido de Git. Las verificaciones pendientes permanecen sin marcar en `tasks.md`.
