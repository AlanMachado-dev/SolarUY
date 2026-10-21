# Aceptación OIDC — 20/10/2026

Universal Login, callback y logout de Auth0 comprobados con cuentas reales. Administrador y ambos roles permitieron registrar parques; Operador y la cuenta sin roles recibieron denegación GET/POST. El usuario confirmó que Administrador estaba asignado antes del primer ingreso. Las credenciales se ingresaron exclusivamente en Auth0.

La aplicación Auth0 es la prevista para el sistema, según su responsable (el usuario de esta sesión). No se verificó una aplicación separada de producción: **E01 permanece pendiente**. Se usaron cuentas dedicadas, sin modificar Actions globales, permisos de API ni usuarios habituales para provocar fallos. La cuenta Operador recuperó su asignación original. El acceso inicial usa las credenciales privadas existentes del proveedor; futuras entregas mediante invitación o restablecimiento de Auth0, nunca contraseñas en documentos.

**F07 permanece parcial por decisión del usuario:** cuenta sin roles comprobada; variantes desconocidas, malformadas y con diferente capitalización omitidas. La inspección de código no sustituye esas pruebas reales. Los criterios centrales de acceso y administrador tienen evidencia; la aceptación integral conserva esos dos pendientes.

## Entorno y controles

- SDK .NET 10.0.400, runtime 10.0.11; OIDC 10.0.12 e IdentityModel 8.19.2 solo en servidor.
- Compilación correcta, Domain **21/21**, arquitectura **17/17**, HTTP local **28/28**. Persisten advertencias NU1903 preexistentes de WebAssembly.Server 10.0.12.
- MySQL 8.4.3: esquema nuevo exclusivo solaruy_req003_aceptacion_f91949a86884, migración existente y tres parques sintéticos contrastados por ID. Base habitual y configuración privada preservadas.
- Navegador HTTPS localhost:7178, proxy separado hacia Kestrel HTTPS en 7443 validando TLS. Sin nuevos endpoints ni identidades fabricadas en SolarUY.
- Evidencias depuradas en artifacts/req003, excluidas de Git. Cookies y callback consumido solo en memoria; no se guardan tokens, códigos, state, nonce, contraseñas, HAR ni credenciales de conexión.

## Resultado por caso

| Caso | Resultado y evidencia en artifacts/req003 |
| --- | --- |
| F01 | Correcto. Arranque real con variables privadas sin usuarios locales. Siete variantes incompletas rechazadas de forma segura (controles-locales.json). |
| F02 | Correcto. Universal Login Administrador, callback, cookie nueva, navegación, recarga y reinicio. |
| F03 | Correcto. Responsable confirmó cuenta y rol anteriores al primer ingreso. Sin promoción automática. |
| F04 | Correcto. Alta, ID y 0 MW visibles; ID y datos contrastados en MySQL (parques-aceptacion.json). |
| F05 | Correcto. Operador ve rol, no ve alta; GET/POST denegados y conteo sin cambios. |
| F06 | Correcto. Ambos roles habilitan alta; alta-ambos-roles.jpg excluye identidad. Registro contrastado con MySQL. |
| F07 | Parcial. Sin roles: sesión válida y GET/POST denegados (sin-roles-denegacion.json). Variantes inválidas omitidas por decisión del usuario. |
| F08 | Correcto. Inspección de rechazos del servicio antes del repositorio, peticiones denegadas y ausencia de escrituras. |
| F09 | Correcto. Alta anónima conserva retorno: Administrador vuelve al alta, Operador recibe denegación en el destino. |
| F10 | Correcto. Seis ingresos con HTTPS externo, //host, separador invertido, porcentaje inválido, login y callback terminaron en / (retornos-reales.json). |
| F11 | Correcto. Decline real muestra cancelación sin crear sesión; conserva solo la previa. Reintento manual Accept funciona. |
| F12 | Correcto. Callback consumido sin correlación, state alterado y código repetido rechazados sin cookie nueva (proxy-callback-repetido.json). |
| F13 | Correcto. POST inicio sin antifalsificación o con token inválido: 400 sin challenge (controles-locales.json). |
| F14 | Correcto. Cierre autenticado sin token o con token inválido: 400 y sesión conservada (proxy-csrf-cierre.json). |
| F15 | Correcto. Exceso de POST: 429; callback legítimo completado durante la misma ventana (proxy-limite-inicio.json y retorno visible al alta). |
| F16 | Atributos HttpOnly/Secure/Lax, no persistente, 30 minutos y sin renovación. Cookie emitida 22:54:10 UTC, vence 23:24:10 UTC; medición anterior HTTP 200 sin renovación (caducidad-antes.json); a las 23:24:56 devolvió HTTP 302 y pidió nuevo ingreso, sin renovación (proxy-comprobar-caducidad.json). |
| F17 | Correcto. Ticket real con nombre actual autentica, nombre antiguo no; sign-in elimina cookie antigua (proxy-cookie-anterior.json y diagnóstico seguro). |
| F18 | Correcto. Solo id_token en ticket protegido. Una cookie, 3479 bytes de Set-Cookie; sin tokens en cliente. sesion-resumen.json registra atributos y nombres, sin valores. |
| F19 | Correcto. Cierre válido borra cookie, completa end_session de Auth0, callback y /. Alta pide ingreso; Auth0 solicita credenciales. |
| F20 | Correcto. Authority inaccesible solo en proceso aislado: cierre elimina sesión y muestra aviso genérico. Se provocó fallo al preparar logout; una caída posterior a navegar al proveedor puede dejar allí el navegador. No se bloqueó Auth0 globalmente. |
| F21 | Correcto. Proveedor inaccesible impide nuevos ingresos sin fallback; cookie previa sigue válida. Configuración restaurada. Duración local fija comprobada por F16. |
| F22 | Correcto. Concesión/retirada manual de Administrador en Operador dedicado: cookie conserva roles hasta logout; nuevo ingreso refleja cambios. Asignación original restaurada. |
| F23 | Correcto. Espacios obligatorios, duplicado por capitalización, 100/101 caracteres y normalización con identidad real; errores conservan formulario. |
| F24 | Correcto. Trigger temporal solo en esquema aislado: error genérico, formulario conservado, sin SQL, falso éxito ni nueva fila. Trigger retirado. |
| F25 | Correcto. Etiquetas asociadas, Enter en acceso denegado y cierre; navegación completa/recarga mantiene estado. Credenciales ingresadas manualmente por usuario. |
| F26 | Correcto. Respuestas, recursos y URLs revisados; 58 evidencias y 57 fuentes sin secretos, ni valores de protocolo en evidencias (privacidad-final.json). |
| F27 | Correcto. Usuario preparó configuración, Action y cuentas siguiendo la guía; ingreso Administrador, alta y Operador sin editar código por cuenta. |
| F28 | Correcto. Resultados y pendientes registrados; limpieza completada de procesos/esquema propios en limpieza-final.json y entorno-real.json. Cuentas conservadas sin roles temporales. |

## Repetir las verificaciones

Preparar Auth0 con [configuracion.md](configuracion.md). No repetir variantes F07 omitidas sin nueva indicación del usuario. Usar aplicación aislada de producción para cerrar E01.

Desde la raíz:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
python arnes/prompting/req003_implementacion_openID/verificar_controles_locales.py
```

El recorrido Python usa valores ficticios, no conecta MySQL ni fabrica identidades. Requiere puerto 7178 libre y certificado confiable. Sus 28 controles no sustituyen Auth0 real.

`gestionar_aceptacion_real.ps1 Preparar` crea esquema nuevo con marcador y migra usando la migración existente. `Iniciar -Puerto 7443` usa configuración vacía y variables privadas; `Estado` consulta solo ese esquema. `ActivarFalloMySql` y `DesactivarFalloMySql` operan únicamente allí. `Iniciar -Puerto 7443 -ProveedorNoDisponible` cambia Authority solo en proceso hijo; repetir sin esa opción restaura Auth0. `Limpiar` detiene el servidor propio y elimina únicamente el esquema identificado.

`ProxyAceptacion.cs` se compila como ejecutable independiente Microsoft.NET.Sdk.Web/net10.0 dentro de artifacts/req003/proxy, enlazando ese archivo como Program.cs. Requiere SOLARUY_ACEPTACION_RAIZ con la raíz absoluta y certificado vigente en CurrentUser/My. Usa directamente el almacén sin exportar claves. Escucha 7178 y conserva Host hacia HTTPS 7443. Iniciarlo oculto, registrar PID/hora de inicio y detener solo ese proceso al finalizar.

El control local proxy-control.json recibe `{ "id": "identificador-unico", "accion": "estado" }`. Acciones: csrf-cierre, cookie-anterior, callback-repetido, limite-inicio, comprobar-caducidad y denegacion-directa. Solo usar con cuentas dedicadas y sesiones reales en memoria. Los resultados son booleanos y estados. Para caducidad, mantener el proceso 30 minutos reales sin cambiar reloj ni duración. Para repetición se usa el callback real ya consumido únicamente en memoria. No habilitar logs de cabeceras, cuerpos ni URLs de autenticación.

## Conservación y límites

Diagramas y documentos fuente se comparan con inventario SHA256. Domain, Application, esquema, migraciones y tests estructurales sin cambios. Sin pruebas unitarias nuevas en capas externas. Los arneses históricos de contraseñas no validan OIDC.

Las cuentas dedicadas se conservaron; no se eliminaron usuarios Auth0. No hay revocación remota inmediata de cookies ante cambios de rol: cerrar e ingresar obtiene la asignación nueva. El SSO externo puede sobrevivir a la caducidad local.

