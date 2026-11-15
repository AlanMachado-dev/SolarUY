# req012 — Plan de implementación de mantenimiento de panel solar

Fuentes de alcance [requirements.md](requirements.md) y [specs.md](specs.md). Implementar las capas en el orden indicado. Marcar cada casilla después de verificar el resultado y completar los tres controles al cierre de cada capa.

## 1. Domain

Implementar cada cambio de comportamiento junto con su prueba xUnit en `tests/SolarUY.Domain.Tests`.

- [x] Crear `Mantenimiento` con un `Guid` autogenerado, navegación a `Panel`, campos obligatorios y ciclo encapsulado. Probar la creación y el rechazo de tipo, responsable o fecha inválidos.
- [x] Incorporar el historial de mantenimientos en `Panel`. Programar a futuro conservando el estado efectivo y rechazar una segunda programación mientras haya otra sin finalizar. Probar ambas reglas.
- [x] Implementar el inicio al alcanzar la fecha programada, incluido el inicio inmediato si la fecha ya llegó al confirmar. Probar instantes anteriores, iguales y posteriores a la fecha programada y el reintento del inicio.
- [x] Conservar `EnFalla` o `Desactivado` cuando comienza el mantenimiento bajo uno de esos estados. Probar que el mantenimiento queda en curso en ambos casos.
- [x] Implementar la simulación de falla y ajustar la desactivación. Probar ambos órdenes de alternancia entre falla y desactivación, la repetición de cada orden y la conservación del mantenimiento en curso.
- [x] Implementar el arreglo desde `EnFalla` y la activación desde `Desactivado`. Probar la recuperación con mantenimiento pendiente, en curso y finalizado, y el rechazo de una orden desactualizada que intenta levantar el estado incorrecto.
- [x] Finalizar por ID un mantenimiento en curso y registrar la fecha UTC recibida del servidor y las observaciones opcionales. Probar el cierre con y sin observaciones, el rechazo del cierre anticipado y el reintento sobre el mismo ID.
- [x] Restaurar `Operativo` al finalizar desde `EnMantenimiento` y conservar `EnFalla` o `Desactivado` si corresponde. Probar las tres salidas.
- [x] Derivar `Panel.UltimoMantenimiento` de la fecha realizada más reciente. Probar un panel nuevo, uno con mantenimiento abierto y uno con varios mantenimientos finalizados.
- [x] Incorporar la regla de admisión de telemetría operativa. Probar que los tres estados de pausa rechazan muestras del sensor y que el dominio admite los registros de control de 0 W que representan los cambios de estado.

### Controles de salida de Domain

- [x] Ejecutar `dotnet build SolarUY.slnx`.
- [x] Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] Ejecutar ArchUnitNET con `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.

## 2. Application

- [x] Definir los contratos para programar y finalizar. Incluir tipo, fecha UTC y responsable en la programación, e ID de mantenimiento y observaciones opcionales en el cierre.
- [x] Definir las órdenes explícitas de falla, arreglo, desactivación y activación en el contrato de control del panel.
- [x] Ampliar los contratos de persistencia para ejecutar las órdenes, buscar programaciones vencidas y aplicar el inicio de un mantenimiento. Mantener compilados los consumidores existentes hasta migrarlos en su capa.
- [x] Extender `ControlPanelService` para que valide autenticación y rol antes de modificar datos. Permitir falla, arreglo, desactivación y activación a Administrador, y programación y cierre a Administrador y Operador.
- [x] Implementar el caso de uso interno que procesa las programaciones vencidas mediante los contratos de persistencia.
- [x] Usar `TimeProvider` como reloj de servidor para el ciclo de mantenimiento y el registro de telemetría, con instantes en UTC.
- [x] Extender el DTO de monitoreo con estado efectivo, potencia actual, datos del mantenimiento y revisión de control. Representar por separado la última medición y el estado confirmado.
- [x] Resolver en Application las acciones disponibles para el usuario a partir del rol, el estado de control y el ciclo del mantenimiento.
- [x] Definir la comparación de actualizaciones por revisión de control y luego por fecha e ID de medición.
- [x] Publicar por `INotificacionMedicion` el estado confirmado después del guardado, incluso si el cambio afecta solo al mantenimiento.
- [x] Agregar a `MonitoreoService` la consulta del permiso de transmisión y de la revisión de control vigente.
- [x] Actualizar el contrato de registro del sensor para recibir potencia y revisión de control. Construir las lecturas aceptadas con estado `Operativo` fijado por el servidor.
- [x] Representar como resultados propios la pausa y la revisión desactualizada, y distinguirlos de un error de persistencia.

### Controles de salida de Application

- [x] Ejecutar `dotnet build SolarUY.slnx`.
- [x] Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] Ejecutar ArchUnitNET con `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.

## 3. Infrastructure

- [x] Mapear `Mantenimiento` en EF Core con clave única, relación obligatoria a `Panel`, fechas UTC, instante de inicio procesado y campos opcionales hasta el cierre.
- [x] Incorporar el índice para consultar programaciones vencidas y mapear la revisión de control persistida del panel.
- [x] Generar y revisar la migración de MySQL, incluida la inicialización de la revisión de los paneles existentes.
- [x] Proyectar la fecha realizada más reciente en la consulta que alimenta el detalle del nodo.
- [x] Cargar el panel y su mantenimiento abierto bajo el bloqueo de fila de MySQL que comparten las órdenes, el procesamiento de vencimientos y el guardado de muestras.
- [x] Revalidar y aplicar un inicio pendiente bajo el bloqueo antes de resolver una orden o admitir una muestra. Confirmar ese inicio aunque la orden no cambie el panel o la muestra se rechace por la pausa resultante.
- [x] Validar el estado efectivo y la revisión de control antes de guardar cada muestra bajo el mismo bloqueo. Usar el instante de admisión de servidor para ordenar la lectura respecto de los cambios de control.
- [x] Incrementar la revisión del panel con cada cambio confirmado de control o mantenimiento. Conservar la revisión si se repite una orden sin cambios.
- [x] Persistir atómicamente el mantenimiento, el estado efectivo, la revisión y el registro de control de 0 W cuando cambia el estado. Usar la fecha programada para el corte de un mantenimiento que ya estaba programado al vencer y respetar el orden de las mediciones.
- [x] Leer el estado confirmado y su revisión de forma coherente con la última medición para las consultas y notificaciones del monitoreo.
- [x] Ejecutar el caso de uso de vencimientos al arrancar y periódicamente, con alcance a paneles sin sesiones abiertas. Asegurar que el bloqueo y la revalidación permitan reintentos y ejecución concurrente por varias instancias.
- [x] Adaptar `SincronizacionMedicionesService` para propagar cambios que solo incrementen la revisión de control e incluir los paneles con sensores conectados.
- [x] Registrar el procesamiento periódico y los servicios y repositorios asociados en la composición de la aplicación.

### Controles de salida de Infrastructure

- [x] Ejecutar `dotnet build SolarUY.slnx`.
- [x] Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] Ejecutar ArchUnitNET con `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.

## 4. Presentation

- [x] Actualizar `LecturaEnVivo.razor` para renderizar el estado confirmado y aplicar la comparación de actualizaciones de Application.
- [x] Mostrar las acciones habilitadas por Application con los textos «Simular Falla», «Simular Arreglo», «Desactivar Panel» y «Activar Panel» según corresponda.
- [x] Agregar el modal de «Programar Mantenimiento» con tipo, fecha programada y responsable. Capturar fecha y hora en `America/Montevideo` y convertir a UTC al enviar.
- [x] Mostrar la fecha confirmada en hora de Uruguay mientras el mantenimiento está pendiente.
- [x] Mostrar «Finalizar Mantenimiento» mientras el mantenimiento esté en curso, inclusive cuando el estado efectivo sea `EnFalla` o `Desactivado`.
- [x] Agregar el modal de cierre con observaciones opcionales y enviar el ID del mantenimiento confirmado.
- [x] Bloquear nuevos envíos mientras se procesa una orden y mostrar el resultado confirmado o el error. Mostrar 0 W durante la pausa.
- [x] Asociar en `MonitoreoHub` las conexiones de sensor al panel correspondiente y enviarles la confirmación de pausa, también cuando se origine en otra instancia. Retirar la asociación al desconectar.
- [x] Exponer la consulta de control autenticada para Administrador que usa el sensor con SignalR detenido.
- [x] Actualizar `SensorMonitoreo.razor` para consultar el control al cargar, reconectar y antes de enviar. Adjuntar la revisión vigente a la muestra.
- [x] Detener la conexión de telemetría al recibir la pausa. Consultar periódicamente por la vía autenticada y reconectar cuando el servidor habilite la transmisión.
- [x] Volver a consultar en el mismo ciclo cuando el servidor rechace una muestra por pausa o cambio de revisión. Aplicar la decisión confirmada antes de volver a transmitir.
- [x] Quitar la selección aleatoria de estado en `InversorSimulado` y conservar únicamente la potencia aleatoria para lecturas operativas.
- [x] Retirar los contratos transitorios que hayan quedado sin uso después de migrar los consumidores de monitoreo.

### Controles de salida de Presentation

- [x] Ejecutar `dotnet build SolarUY.slnx`.
- [x] Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] Ejecutar ArchUnitNET con `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj`.

## Validación funcional

- [x] Aplicar la migración en una base MySQL aislada con paneles y mediciones previas. Comprobar que siguen disponibles las lecturas históricas y las acciones del panel.
- [x] Verificar en el navegador que el inversor cambia la potencia sin cambiar el estado por sí solo.
- [x] Programar a futuro en el navegador. Comprobar campos obligatorios, ID autogenerado y fecha local confirmada. Al llegar el horario, verificar `EnMantenimiento`, 0 W, conexión de telemetría detenida y ausencia de nuevas muestras del sensor.
- [x] Finalizar desde el monitoreo en condiciones normales. Verificar la fecha realizada de servidor, las observaciones cuando se ingresen, el último mantenimiento en el detalle y la reanudación de lecturas en `Operativo`.
- [x] Simular una falla y luego un arreglo sin mantenimiento activo. Verificar el cambio de botón, 0 W durante la falla y la reconexión al arreglar.
- [x] Alternar falla y desactivación en ambos órdenes. Verificar que prevalece la última orden confirmada, que los botones reflejan ese estado y que una orden inversa de una vista desactualizada no lo modifica.
- [x] Simular falla y arreglo sobre un mantenimiento en curso. Verificar el regreso a `EnMantenimiento`, con la conexión detenida y sin nuevas muestras.
- [x] Desactivar y activar sobre un mantenimiento en curso. Verificar el regreso a `EnMantenimiento`, con la conexión detenida y sin nuevas muestras.
- [x] Finalizar mientras el panel está en falla o desactivado. Verificar que la pausa continúa hasta aplicar la orden de arreglo o activación correspondiente.
- [x] Verificar en el navegador con sesiones autenticadas que Administrador puede ejecutar todas las acciones y Operador puede programar y finalizar. Comprobar el rechazo en servidor de órdenes fuera del rol y de peticiones sin autenticar.
- [x] Hacer vencer una programación sin sesiones abiertas y repetir el escenario con un reinicio durante la espera. Confirmar que el inicio queda persistido una sola vez y se refleja al reabrir el monitoreo.
- [x] Ejecutar con dos instancias y cruzar un vencimiento con órdenes y muestras simultáneas. Confirmar que se descartan las muestras con revisión anterior, que no se duplica el inicio y que ambas sesiones reciben los cambios confirmados.
- [x] Enviar después de la reactivación una muestra obtenida antes de la pausa. Verificar que el servidor rechaza la revisión anterior aunque el panel esté operativo.
- [x] Provocar un fallo de escritura en la base aislada y comprobar la reversión del cambio completo y el estado confirmado en pantalla.
- [x] Verificar en el historial los cortes y las reanudaciones de 0 W y comprobar que el intervalo de pausa no suma energía.

## Registro de resultados

Ejecutar los controles desde la raíz de SolarUY. Ante un fallo, corregir el código conforme a las reglas y volver a ejecutar los controles, preservando las pruebas estructurales. Registrar los resultados por capa y distinguir advertencias preexistentes de errores nuevos.

Mantener `SolarUY/appsettings.Local.json` y la base habitual del usuario. Si se requieren herramientas de prueba auxiliares, guardarlas en `arnes/prompting/tests/req012/`. Guardar evidencias depuradas en `artifacts/` y registrar la aceptación en navegador cuando se complete con sesiones reales.

- Domain: compilación correcta, 143 pruebas de dominio y 17 de ArchUnitNET aprobadas. Se observaron los avisos NU1903 de WebAssembly.Server ya presentes en la compilación inicial.

- Application: compilación correcta, 143 pruebas de dominio y 17 de ArchUnitNET aprobadas. Mismos avisos NU1903 preexistentes.

- Infrastructure: compilación correcta, 143 pruebas de dominio y 17 de ArchUnitNET aprobadas. Mismos avisos NU1903 preexistentes. Migración generada y revisada.

- Presentation: compilación correcta, 143 pruebas de dominio y 17 de ArchUnitNET aprobadas. Mismos avisos NU1903 preexistentes.

- Control final tras retirar los contratos transitorios y sus casos de dominio reemplazados: compilación correcta, 135 pruebas de dominio y 17 de ArchUnitNET aprobadas. También compilaron los arneses consumidores actualizados de mod005, req007, req008, req009 y req011.
- Validación funcional en MySQL 8.4 aislado (`solaruy_req012_validacion`, puerto local 39312) y Chromium contra dos instancias locales. Evidencias depuradas en `artifacts/req012/mysql.txt`, `navegador.txt` y `reinicio.txt`. El arnés se ejecuta desde la raíz con `arnes/prompting/tests/req012/verificar.ps1`.
- Aceptación OIDC real del 14/11/2026 en el panel aislado. Administrador confirmó las seis acciones en el navegador. Operador programó y finalizó, sin acceso a las acciones administrativas. El caso de uso devolvió `AccesoDenegado` para las cuatro órdenes administrativas con la sesión de Operador y para las seis órdenes sin sesión, sin cambios en el control del panel. La consulta de telemetría sin sesión devolvió HTTP 401 y el monitoreo redirigió al inicio de sesión. Evidencias depuradas en `artifacts/req012/oidc-administrador.json`, `oidc-operador.json` y `oidc-anonimo.json`. El entorno se puede iniciar desde la raíz con `arnes/prompting/tests/req012/verificar.ps1 -Oidc` y la configuración privada de aceptación existente.
