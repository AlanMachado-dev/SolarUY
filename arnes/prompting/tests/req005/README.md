# Comprobación funcional de req005

Auxiliar de integración con MySQL, entidades, repositorios, servicios y componentes SSR reales. Complementa los controles de Domain y ArchUnitNET; no es una suite unitaria de Application, Infrastructure ni Presentation. Las identidades locales **no acreditan aceptación OIDC externa**.

Ejecutar desde la raíz:

```powershell
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj -- preview
```

Cada ejecución crea una base nueva `solaruy_req005_<12 caracteres>`, aplica migraciones y prepara tres parques: dos con nodos y uno vacío. Incluye coordenadas cero, endpoint ausente, textos extensos y marcado que debe mostrarse codificado. Obtiene la conexión de la configuración privada sin imprimirla y sustituye exclusivamente la base; conserva `SolarUY/appsettings.Local.json` y el destino habitual.

El host auxiliar escucha solo en `http://127.0.0.1:8195`. Usa cookies propias y claves efímeras. `/_auxiliar/sesion/Administrador` y `/_auxiliar/sesion/Operador` permiten revisar los componentes con identidades de prueba. Estas rutas no se incorporan a SolarUY. Con `preview`, queda activo hasta Ctrl+C.

Comprueba autorización antes de persistencia, rol exacto, cancelación, grafo sin seguimiento, estados inexistentes, mapeo, datos persistidos, endpoint codificado, secciones semánticas, enlaces y botón reservado, rutas, errores, diagnóstico depurado, recuperación, consultas sin escrituras ni solicitudes al sensor y regresión del alta HTTP con antifalsificación. Las evidencias depuradas quedan en `artifacts/req005/`, excluido de Git; no se guarda HTML protegido, credenciales, tokens ni cookies.

Para comprobar SolarUY real contra la base aislada, iniciar la aplicación con su conexión suministrada por entorno, sin editar la configuración privada, y completar OIDC con cuentas reales. Antes de cualquier escritura o fallo, confirmar que el listado contiene los parques `req005`. La aceptación real realizada se registra separadamente en `artifacts/req005/resultados.md`, sin identidades personales. Las capturas de sesiones reales conservan solo el área de contenido.

Los siguientes modos operan exclusivamente sobre el destino registrado en `artifacts/req005/destino-aislado.json`, comprobando su prefijo y que difiera de la base habitual:

```powershell
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj -- estado-antes
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj -- activar-fallo
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj -- restaurar
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj -- estado-despues
```

`estado-antes` guarda solo una huella de los datos. `activar-fallo` valida las fixtures y renombra temporalmente la tabla de nodos aislada; restaurarla **inmediatamente** después de observar el error con `restaurar`, incluso si la inspección falla. Tras restaurar, «Recargar» debe recuperar el detalle. `estado-despues` exige que los datos sigan siendo iguales; realizar esta comparación antes de probar altas.

Detener los servidores propios antes de retirar recursos:

```powershell
dotnet run --project arnes/prompting/tests/req005/VerificarDetalleNodo.csproj -- limpiar
```

`limpiar` busca únicamente bases con el patrón exacto de req005, excluye el destino habitual y exige los tres parques auxiliares y nombres de nodos reconocidos antes de retirar cada base. Conserva las evidencias y registra los destinos retirados. También retira destinos de ejecuciones del auxiliar interrumpidas después de preparar las fixtures. No reutiliza datos habituales ni modifica autenticación o configuración de producción.
