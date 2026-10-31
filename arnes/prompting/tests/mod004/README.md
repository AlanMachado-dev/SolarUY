# Verificación funcional de mod004

Auxiliar de integración con MySQL, servicios, repositorios y componentes SSR reales.
Complementa compilación, Domain y ArchUnitNET; no incorpora suites unitarias de
Application, Infrastructure ni Presentation. Sus identidades locales no acreditan
aceptación con cuentas externas OIDC.

Ejecutar desde la raíz. Crear `artifacts/mod004/destino-aislado.json` con una propiedad
`Base` que contenga un nombre nuevo `solaruy_mod004_<12 caracteres hexadecimales>`.
La base no debe existir. El auxiliar lee la configuración privada solo en memoria,
sustituye únicamente el nombre de base y comprueba que difiera del habitual.
No imprime conexiones, credenciales, tokens, cookies ni HTML protegido.

```powershell
dotnet run --project arnes/prompting/tests/mod004/VerificarSeguridadDetalleNodo.csproj
```

Crea la base, aplica migraciones y prepara dos parques con tres nodos, paneles
ordenables y datos opcionales ausentes. Comprueba ambos roles, ambos roles juntos,
usuario sin roles permitidos y anónimo. Observa llamadas a repositorios reales
para verificar el rechazo previo a persistencia. Por HTTP comprueba los datos,
acciones, rutas, altas y rechazos GET/POST con antiforgery válido, ausencia de
escrituras rechazadas, y un fallo real de lectura renombrando temporalmente la
tabla de paneles aislada y restaurándola en `finally`.

Las evidencias depuradas se guardan en `artifacts/mod004/`, excluido de Git.
El modo `repetir` permite continuar sobre esas fixtures tras una interrupción
anterior al alta administrativa final; una nueva ejecución completa necesita
un destino nuevo o retirar primero el destino propio.

```powershell
dotnet run --project arnes/prompting/tests/mod004/VerificarSeguridadDetalleNodo.csproj -- preview
```

Mantiene un host auxiliar en `http://127.0.0.1:8194`. Sus rutas
`/_auxiliar/sesion/Operador`, `/Administrador`, `/Ambos` y `/SinRol` usan sesiones
efímeras locales, sin modificar usuarios ni autenticación de SolarUY. El host
utiliza los componentes, estilos y servicios reales con la base aislada preparada.
Permite comprobar clic en nombre, datos y espacio libre, Tab/Enter y foco visible,
regreso al parque, estados vacíos, acciones por rol y escritorio/móvil.
Estos endpoints existen exclusivamente en este auxiliar.

Detener el host propio antes de retirar únicamente la base registrada:

```powershell
dotnet run --project arnes/prompting/tests/mod004/VerificarSeguridadDetalleNodo.csproj -- limpiar
```

No utilizar estos resultados como aceptación externa OIDC. Para esa aceptación,
iniciar SolarUY original contra un destino aislado y completar el acceso con
cuentas reales; no reutilizar identidades auxiliares ni registrar sus sesiones.
