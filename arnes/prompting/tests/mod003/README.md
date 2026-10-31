# Comprobación funcional auxiliar mod003

Ejecutar desde la raíz. Usa los componentes SSR, servicios y MySQL reales, con sesiones locales auxiliares para Administrador y Operador. No es una prueba unitaria ni acredita aceptación con el proveedor OIDC externo.

```powershell
dotnet run --project arnes/prompting/tests/mod003/VerificarMod003.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/estudio/NET/SolarUY/artifacts/mod003/build
```

Agregar `-- preview` mantiene el host disponible en `http://127.0.0.1:8193` para revisar escritorio y móvil. La ruta `/_auxiliar/sesion/Administrador` o `/_auxiliar/sesion/Operador` abre una sesión de prueba propia del host; estas rutas no se agregan a la aplicación. Detener con Ctrl+C al terminar.

Cada ejecución crea una base nueva `solaruy_mod003_<sufijo>`, aplica las migraciones existentes y guarda allí los datos auxiliares. La conexión se obtiene de la configuración privada sin imprimirla; la base habitual y `SolarUY/appsettings.Local.json` se conservan. Las claves de las sesiones son efímeras. El host solo escucha en loopback.

Comprueba listado vacío, tres columnas sin IDs visibles, enlaces al detalle, dos nodos con inversor y sensor, conservación de campos y formatos, asociaciones accesibles, permisos, parque inexistente, alta HTTP con antifalsificación y mensaje de éxito. Un interceptor provoca un fallo de lectura controlado para comprobar errores y recarga; no representa una caída real del servidor MySQL.

Las evidencias depuradas se guardan en `artifacts/mod003/`: destino aislado, resultados funcionales, tabla renderizada, registros de controles y capturas de escritorio/móvil. La base auxiliar se conserva para revisión. No se guardan contraseñas, cookies ni tokens.
