# mod004 — Plan de implementación: Seguridad del detalle de nodo

Fuente: [specs.md](specs.md) y [requirements.md](requirements.md). Objetivo: permitir la consulta completa del detalle a `Operador` y `Administrador`, conservando el alta de paneles exclusiva de `Administrador`.

Esta fase crea únicamente este plan; todas las tareas están pendientes. Durante la implementación, marcar cada casilla al verificarla. La solicitud expresa autoriza crear `tasks.md` y actualizar su seguimiento en esta carpeta; mantener intactos los demás documentos de requerimientos. No modificar diagramas ni cambios preexistentes ajenos a mod004.

Ejecutar en orden: preparación → Domain → Application → Infrastructure → Presentation → validación funcional. Domain e Infrastructure requieren revisión, sin cambios previstos. En cada capa modificada, completar compilación, Domain y ArchUnitNET antes de continuar; corregir los fallos en el código, sin modificar los tests estructurales. No generar pruebas unitarias de Application, Infrastructure ni Presentation.

## 0. Preparación y referencia inicial

- [x] PRE01. Leer `AGENTS.md`, las siete reglas de `.agents/rules` en su orden y los documentos fuente de mod004; consultar los diagramas de `.agents/Images` sin modificarlos.
- [x] PRE02. Registrar el estado inicial de Git para distinguir los cambios existentes de los que corresponden a mod004.
- [x] PRE03. Identificar un destino de pruebas aislado para preparar datos, intentar altas y provocar fallos; conservar `SolarUY/appsettings.Local.json` y la base habitual del usuario.
- [x] PRE04. Ejecutar desde la raíz `dotnet build SolarUY.slnx` y registrar el resultado inicial.
- [x] PRE05. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado inicial.
- [x] PRE06. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado inicial de ArchUnitNET.
- [x] PRE07. Resolver los errores que impidan establecer una base válida antes de implementar; distinguir advertencias preexistentes de errores nuevos y guardar el registro depurado en `artifacts/mod004/`.

## 1. Domain — Revisión sin cambios

- [x] DOM01. Confirmar que `Nodo`, `ParqueSolar`, `Inversor`, `Sensor` y `Panel` ya proporcionan los datos necesarios mediante sus relaciones actuales.
- [x] DOM02. Confirmar que la ampliación de roles no requiere modificar entidades, invariantes ni comportamiento de Domain; dejar la autorización en Application. No agregar pruebas unitarias nuevas de dominio al no cambiar su comportamiento.

## 2. Application — Ampliar exclusivamente la consulta

Archivo de implementación: `SolarUY/Application/Nodos/Servicios/NodoService.cs`.

- [x] APP01. Cambiar la condición de roles de `ObtenerPorIdAsync` para aceptar `Administrador` **o** `Operador` mediante el `IUsuarioActualService` existente.
- [x] APP02. Revisar que la autenticación siga evaluándose primero y que ambos rechazos ocurran antes de invocar `INodoRepository`, sin devolver nodo ni paneles.
- [x] APP03. Revisar que se conserven `Guid.Empty`, `NoEncontrado`, consulta por ID y propagación de fallos técnicos.
- [x] APP04. Revisar que ambos roles autorizados reciban el mismo mapeo de `Nodo` y `Paneles`, con su orden actual y sin escrituras durante la consulta.
- [x] APP05. Revisar que `NodoService.CrearAsync` y `PanelService.CrearAsync` mantengan autenticación y rol `Administrador` antes de acceder a persistencia.
- [x] APP06. Revisar el cambio para confirmar reutilización de interfaces y DTO actuales, inyección por constructor y ausencia de dependencias de ASP.NET, Blazor o Infrastructure en Application.

### Control obligatorio de Application

- [x] APP07. Ejecutar `dotnet build SolarUY.slnx` desde la raíz.
- [x] APP08. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] APP09. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` para validar ArchUnitNET.
- [x] APP10. Registrar los tres resultados en `artifacts/mod004/`; si falla algún control, corregir el código y repetir los controles afectados antes de pasar a Infrastructure.

## 3. Infrastructure — Reutilización sin cambios

- [x] INF01. Revisar que `UsuarioActualService` permita consultar los roles existentes sin cambiar autenticación, usuarios ni aprovisionamiento.
- [x] INF02. Revisar que `NodoRepository.ObtenerPorIdAsync` conserve `AsNoTracking()` y la carga de `Paneles`, `Parque` e `Inversor.Sensor`.
- [x] INF03. Confirmar que mod004 no introduce cambios en repositorios, mapeos, migraciones, esquema, configuración ni registros de dependencias de `Program.cs`.

No hay implementación prevista en esta capa. Si un cambio resultara necesario dentro del alcance aprobado, ejecutar y registrar los tres controles obligatorios antes de avanzar a Presentation.

## 4. Presentation — Navegación y acciones por rol

### Acceso desde el parque

- [x] PREST01. En `SolarUY/Presentation/Pages/DetalleParqueSolar.razor`, ampliar únicamente el `AuthorizeView` del enlace de cada nodo a `Roles="Administrador,Operador"`.
- [x] PREST02. Revisar que se conserven ruta, `stretched-link`, nombre accesible y foco visible del enlace; mantener el nombre como texto para usuarios sin ninguno de los dos roles.
- [x] PREST03. Revisar que «+ Agregar nodo» conserve su `AuthorizeView` exclusivo para `Administrador`.

### Detalle del nodo

- [x] PREST04. En `SolarUY/Presentation/Pages/DetalleNodo.razor`, ampliar el atributo a `[Authorize(Roles = "Administrador,Operador")]` sin cambiar la ruta `/nodos/{Id:guid}`.
- [x] PREST05. Encapsular únicamente «+ Agregar Panel Solar» en `AuthorizeView Roles="Administrador"`, conservando su destino y funcionamiento para ese rol.
- [x] PREST06. Revisar que las secciones de nodo, inversor, sensor y paneles, sus formatos y mensajes vacíos sigan mostrando los DTO existentes para ambos roles.
- [x] PREST07. Revisar que «Volver al parque» continúe usando `ParqueId` y que el cambio de nodo limpie el resultado anterior.
- [x] PREST08. Revisar la conservación de los flujos de inicio de sesión, acceso denegado, nodo inexistente, recarga y fallo técnico, sin exponer datos ante un rechazo.
- [x] PREST09. Revisar que `SolarUY/Presentation/Pages/AltaPanelSolar.razor` conserve `[Authorize(Roles = "Administrador")]` y que Presentation solo invoque Application.

### Control obligatorio de Presentation

- [x] PREST10. Ejecutar `dotnet build SolarUY.slnx` desde la raíz.
- [x] PREST11. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] PREST12. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` para validar ArchUnitNET.
- [x] PREST13. Registrar los tres resultados en `artifacts/mod004/`; corregir cualquier fallo en el código y repetir los controles afectados antes de iniciar la aceptación funcional.

## 5. Validación funcional y de seguridad

Usar el destino aislado identificado en PRE03. Preparar allí usuarios o sesiones de Operador, Administrador, ambos roles y autenticado sin esos roles; incluir sesión anónima. Preparar nodos de distintos parques, con y sin paneles, y los casos de datos opcionales. No guardar credenciales, tokens ni cookies en documentos o evidencias. Si se necesitan herramientas auxiliares, ubicarlas en `arnes/prompting/tests/mod004/`, separadas de esta documentación.

### CA1 — Navegación de Operador

- [x] VAL01. Como Operador, abrir desde el parque distintos nodos mediante clic en nombre, datos y espacio libre de cada tarjeta; comprobar que cada enlace abre el nodo seleccionado.
- [x] VAL02. Como Operador, recorrer las tarjetas con Tab y abrir un nodo con Enter; comprobar nombre accesible y foco visible.

### CA2 — Información completa

- [x] VAL03. Como Operador, consultar un nodo por enlace y por URL directa; comparar con Administrador el nombre, coordenadas y cantidad de paneles mostrados.
- [x] VAL04. Comparar entre ambos roles nombre y serie de inversor y sensor; verificar endpoint informado y «Sin endpoint asociado» cuando falte, sin solicitudes al sensor.
- [x] VAL05. Comparar entre ambos roles los paneles mostrados y su orden, incluyendo nombre, modelo, potencia, fecha de instalación, estado, generación y último mantenimiento.
- [x] VAL06. Verificar como Operador los mensajes de nodo sin paneles, «Sin mediciones» y «Sin mantenimientos» con los datos correspondientes.
- [x] VAL07. Entrar directamente al detalle de nodos de distintos parques y comprobar que «Volver al parque» lleva al parque real de cada nodo.

### CA3 — Acción exclusiva de Administrador

- [x] VAL08. Como Operador sin rol administrativo, comprobar que «+ Agregar Panel Solar» no esté renderizado en el detalle.
- [x] VAL09. Como Administrador, comprobar que el enlace esté disponible y abra el alta del nodo correcto.
- [x] VAL10. Con ambos roles, comprobar acceso al detalle y disponibilidad del alta de paneles como Administrador.

### Controles de autorización y regresión

- [x] VAL11. Sin autenticación, abrir el detalle por URL y comprobar el flujo de inicio de sesión sin exposición de datos.
- [x] VAL12. Como autenticado sin ninguno de los dos roles, comprobar nombre sin enlace en el parque y acceso denegado al detalle por URL directa.
- [x] VAL13. Mediante una comprobación funcional auxiliar del caso de uso, verificar que `ObtenerPorIdAsync` rechace anónimos y usuarios sin roles autorizados antes de invocar el repositorio, con nodo nulo y paneles vacíos. Registrar este resultado como comprobación del servicio, separado de la aceptación de interfaz.
- [x] VAL14. Como Operador, intentar abrir `/nodos/{Id:guid}/paneles/nuevo` y comprobar que no se obtenga el formulario.
- [x] VAL15. Como Operador, intentar un envío directo al alta en el destino aislado y comprobar rechazo y ausencia de paneles creados.
- [x] VAL16. Mediante una comprobación funcional auxiliar de `PanelService.CrearAsync`, verificar `AccesoDenegado` para Operador antes de cualquier acceso al repositorio; registrar el resultado separado de la comprobación HTTP.
- [x] VAL17. Como Administrador, registrar un panel en el destino aislado y comprobar el retorno al detalle con el panel creado.
- [x] VAL18. Como Operador, comprobar que «+ Agregar nodo» siga ausente y que el alta de nodos por URL directa continúe denegada; como Administrador, comprobar que conserve su acceso.
- [x] VAL19. Con un rol autorizado, comprobar `Guid.Empty`, GUID inexistente y ruta mal formada, conservando los estados y enlaces de recuperación existentes.
- [x] VAL20. Provocar un fallo técnico únicamente en el entorno aislado; verificar mensaje de error, recarga del detalle y diagnóstico depurado, sin presentar el fallo como nodo inexistente.
- [x] VAL21. Navegar entre distintos nodos y comprobar que no se muestre información del nodo anterior.
- [x] VAL22. Verificar el flujo parque → nodo → parque en escritorio y móvil, sin IDs visibles ni pérdida de datos o acciones existentes.

## 6. Revisión y cierre

- [x] FIN01. Revisar el diff de mod004 para confirmar que los cambios funcionales se limitan a `NodoService.cs`, `DetalleParqueSolar.razor` y `DetalleNodo.razor`, preservando cambios ajenos, diagramas, configuración privada y tests estructurales.
- [x] FIN02. Revisar los resultados obligatorios de la última versión modificada; si hubo correcciones posteriores a los controles de una capa, volver a ejecutar compilación, Domain y ArchUnitNET para esa versión.
- [x] FIN03. Guardar en `artifacts/mod004/` evidencias depuradas de CA1–CA3, seguridad y regresión, identificando el destino aislado y distinguiendo inspección, comprobación auxiliar y ejecución real de la interfaz.
- [x] FIN04. Actualizar las casillas únicamente con trabajo verificado y entregar un resumen de cambios, resultados de controles y cualquier validación pendiente; no declarar aceptación externa a partir de una inspección o simulación.

## Registro de ejecución de mod004

Implementación y controles por capa completados el 31/10/2026. Las validaciones funcionales se ejecutaron con servicios, MySQL y HTTP reales, y navegador de escritorio/móvil, mediante identidades auxiliares locales en el destino aislado solaruy_mod004_8ab04c22784e. El host se detuvo y la base temporal se retiró al terminar. No se ejecutó ni se declara aceptación con cuentas externas OIDC.

Evidencias depuradas y distinción de comprobaciones: artifacts/mod004/resultados.md, funcional.txt, registros de controles y capturas. Auxiliar reproducible: arnes/prompting/tests/mod004/.
