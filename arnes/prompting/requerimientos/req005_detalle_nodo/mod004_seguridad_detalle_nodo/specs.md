# mod004 — Especificaciones técnicas: Seguridad del detalle de nodo

## Objetivo y alcance

Permitir que los usuarios autenticados con rol `Operador` accedan desde el detalle de un parque solar al detalle de sus nodos y consulten la misma información existente de nodo, inversor, sensor y paneles solares. El alta de paneles continúa siendo exclusiva del rol `Administrador`.

Fuente: [requirements.md](requirements.md). Esta modificación amplía la restricción de consulta de req005; toma como base la implementación actual, que ya incluye la consulta y el alta de paneles. Respeta `AGENTS.md`, las siete reglas compartidas y los diagramas de `.agents/Images`.

La entrega consiste únicamente en este `specs.md`, solicitado expresamente en esta carpeta. La regla del requerimiento que prohíbe modificar `arnes/prompting/requerimientos/` se conserva para la implementación posterior; no se modifican los documentos existentes. No se diseñan acciones futuras del operador.

## 1. Domain

- Reutilizar `Nodo`, `Inversor`, `Sensor`, `Panel` y `ParqueSolar`, con sus relaciones e invariantes actuales. No se requieren cambios de comportamiento ni nuevas entidades.
- La ampliación de permisos de consulta se resuelve en Application; Domain permanece independiente de roles, autenticación, Blazor y persistencia.
- Al no cambiar comportamiento de Domain, esta modificación no requiere nuevas pruebas unitarias de dominio.

## 2. Application

### Autorización de consulta

Modificar exclusivamente la comprobación de roles de `NodoService.ObtenerPorIdAsync`, en `SolarUY.Application.Nodos.Servicios`, reutilizando `IUsuarioActualService` inyectado por constructor:

1. Si el usuario no está autenticado, devolver `AutenticacionRequerida` sin consultar el repositorio.
2. Autorizar cuando pertenezca a `Administrador` **o** a `Operador`. Si no pertenece a ninguno, devolver `AccesoDenegado` sin consultar persistencia ni revelar la existencia del nodo.
3. Conservar el tratamiento de `Guid.Empty`, nodo inexistente y consulta por ID mediante `INodoRepository`.
4. Devolver el mismo `ResultadoDetalleNodoDto` para ambos roles autorizados, incluyendo `Nodo` y `Paneles`, con el mapeo y orden actuales. Los rechazos no deben incluir información del nodo ni sus paneles.

Reutilizar sin ampliar `INodoService`, `INodoRepository`, `CodigoConsultaNodo`, `ResultadoDetalleNodoDto`, `NodoDto` y `PanelDto`. Application solo depende de Domain y no utiliza tipos de autorización de ASP.NET ni referencias a Infrastructure. No incorporar MediatR ni servicios nuevos para este cambio.

### Permisos de escritura

Conservar la validación de autenticación y rol `Administrador` en `PanelService.CrearAsync`, anterior a cualquier consulta o escritura. Un usuario con solo rol `Operador` recibe `AccesoDenegado` al intentar registrar un panel, aunque invoque el caso de uso sin pasar por la interfaz. También se conserva la restricción administrativa de `NodoService.CrearAsync`.

| Usuario | Consultar detalle y paneles | Registrar panel solar |
| --- | --- | --- |
| Administrador autenticado | Permitido | Permitido |
| Operador autenticado | Permitido | Denegado |
| Autenticado sin ninguno de esos roles | Denegado | Denegado |
| Sin autenticación | Requiere iniciar sesión | Requiere iniciar sesión |

Si un usuario tiene ambos roles, conserva las capacidades de Administrador; la presencia de `Operador` no restringe sus permisos administrativos.

## 3. Infrastructure

- Reutilizar `UsuarioActualService` y los roles existentes, sin modificar la autenticación ni el aprovisionamiento de usuarios.
- Mantener `NodoRepository.ObtenerPorIdAsync`: consulta de solo lectura con `AsNoTracking()` y carga de `Paneles`, `Parque` e `Inversor.Sensor`. Los permisos se comprueban en Application antes de invocarlo.
- No se requieren cambios de repositorios, esquema, migraciones, configuración ni registros de dependencias en `Program.cs`.
- Conservar `SolarUY/appsettings.Local.json` y la base habitual del usuario. Las verificaciones que necesiten preparar datos, intentar escrituras o provocar fallos deben usar un destino aislado identificado.

## 4. Presentation

### Acceso desde el parque solar

En `SolarUY/Presentation/Pages/DetalleParqueSolar.razor`, ampliar a `Roles="Administrador,Operador"` el `AuthorizeView` que contiene el enlace de cada nodo. Esta lista expresa autorización por cualquiera de los dos roles.

- Conservar la ruta `/nodos/{nodo.ID:guid}`, el enlace real con `stretched-link`, su nombre accesible y el foco visible. Operador debe poder abrir el nodo seleccionado haciendo clic en cualquier parte de su tarjeta, o usando Tab y Enter.
- Para usuarios sin ninguno de esos roles, conservar el nombre del nodo como texto sin enlace.
- Mantener el botón «+ Agregar nodo» dentro de su autorización exclusiva para Administrador.

### Detalle del nodo

En `SolarUY/Presentation/Pages/DetalleNodo.razor`, conservar `/nodos/{Id:guid}` y ampliar el atributo a `[Authorize(Roles = "Administrador,Operador")]`, protegiendo también la entrada directa por URL.

Ambos roles reciben la misma consulta de `INodoService.ObtenerPorIdAsync` y visualizan los campos existentes:

| Sección | Información visible |
| --- | --- |
| Nodo | Nombre, latitud, longitud y cantidad de paneles. |
| Inversor | Nombre y número de serie. |
| Sensor | Nombre, número de serie y endpoint, o «Sin endpoint asociado». |
| Paneles solares | Nombre, modelo, potencia nominal, fecha de instalación, estado, generación actual y último mantenimiento; conservar «Sin mediciones» y «Sin mantenimientos» cuando corresponda. |

Conservar el mensaje «Este nodo todavía no tiene paneles solares» para la colección vacía, el formato actual de valores y la ausencia de IDs como contenido visible. El endpoint se muestra como texto, sin conectarse al sensor. «Volver al parque» continúa usando el `ParqueId` devuelto por Application.

Encapsular únicamente el enlace «+ Agregar Panel Solar» en un `AuthorizeView Roles="Administrador"`. Para Operador sin rol administrativo, el enlace no debe renderizarse; para Administrador conserva su destino `/nodos/{nodo.ID:guid}/paneles/nuevo` y su funcionamiento actual.

Reutilizar la autorización declarativa de Blazor para presentar las acciones. Los componentes solo invocan Application y muestran DTO; no acceden a repositorios ni sustituyen la autorización del caso de uso.

### Protección y estados existentes

- Conservar `[Authorize(Roles = "Administrador")]` en `AltaPanelSolar.razor`; Operador no debe obtener el formulario por URL directa ni registrar paneles mediante envío directo. Ocultar el enlace complementa estas protecciones.
- Mantener los flujos actuales de inicio de sesión y acceso denegado. Ante un rechazo de Application, limpiar el resultado y no renderizar datos del detalle.
- Conservar los estados de nodo inexistente, ruta inválida y fallo técnico, junto con la recarga y el regreso al listado cuando correspondan. Limpiar los datos anteriores al cambiar de nodo.
- Registrar solo diagnósticos depurados, sin secretos, cookies, tokens ni datos de conexión.

### Verificación prevista de la implementación

| Criterio | Comprobación y resultado esperado |
| --- | --- |
| CA1: navegación de Operador | Desde un parque con varios nodos, clic en nombre, datos y espacio libre de cada tarjeta abre el nodo correcto; Tab y Enter ofrecen el mismo acceso con foco visible. |
| CA2: información completa | Con Operador, verificar entrada desde el parque y por URL directa. Comparar con Administrador los datos de nodo, inversor, sensor y paneles; cubrir nodos con y sin paneles, endpoint ausente y valores sin mediciones ni mantenimientos. El regreso abre el parque real del nodo. |
| CA3: acción administrativa | Operador no recibe el enlace «+ Agregar Panel Solar». Administrador lo conserva; un usuario con ambos roles también lo conserva. |
| Seguridad de lectura | Anónimo sigue el flujo de inicio de sesión; autenticado sin ninguno de los dos roles recibe acceso denegado. Application rechaza antes de consultar persistencia y no devuelve datos. |
| Seguridad de escritura | Operador no obtiene el formulario por URL ni registra paneles por envío directo; `PanelService.CrearAsync` rechaza antes de acceder a persistencia. Verificar ausencia de escrituras en el destino aislado. Administrador conserva el alta existente. |
| Regresión | Conservar alta de nodos exclusiva para Administrador, estados vacíos, nodo inexistente y manejo de errores. Verificar presentación en escritorio y móvil. |

Al finalizar cada capa de implementación modificada y antes de avanzar a la siguiente, ejecutar desde la raíz:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Corregir fallos en el código sin modificar tests estructurales. No generar pruebas unitarias de Application, Infrastructure ni Presentation. Si se necesitan auxiliares funcionales, ubicarlos en `arnes/prompting/tests/mod004/`; guardar evidencias depuradas en `artifacts/mod004/`, excluido de Git. Registrar solo verificaciones realmente ejecutadas, distinguiendo inspecciones o simulaciones de aceptación externa real.

La planificación de tareas y la implementación corresponden a fases posteriores.
