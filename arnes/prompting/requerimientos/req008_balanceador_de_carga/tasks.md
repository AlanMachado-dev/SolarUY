# req008 — Plan de implementación del balanceo de carga

Seguir [requirements.md](requirements.md) y [specs.md](specs.md). Las rutas y comandos se interpretan desde la raíz del repositorio. Marcar cada casilla únicamente después de verificar su resultado y registrar evidencia cuando corresponda.

Implementar en orden Domain → Application → Infrastructure → Presentation. Al cerrar cada capa, ejecutar los tres controles indicados y corregir los errores antes de avanzar. Mantener los tests estructurales sin modificaciones; registrar las advertencias preexistentes separadas de errores nuevos. No generar pruebas unitarias de Application, Infrastructure o Presentation.

El arranque habitual con `docker compose up -d` debe usar la base actual de `SolarUY/appsettings.Local.json`. Las verificaciones que escriban datos o provoquen fallos deben ejecutarse sobre una base aislada identificada. Conservar el archivo privado, los datos habituales y los diagramas de `.agents/Images`.

## 0. Preparación

- [x] PRE01. Revisar las reglas compartidas y los tres diagramas, confirmando el alcance de dos instancias, Nginx y MySQL compartido.
- [x] PRE02. Revisar el estado de Git para identificar cambios ajenos antes de modificar archivos.
- [x] PRE03. Comprobar la disponibilidad de .NET 10, Docker Desktop con contenedores Linux y Docker Compose.
- [x] PRE04. Identificar los archivos y las opciones existentes de autenticación, persistencia y monitoreo que se reutilizarán, sin registrar configuración privada.
- [x] PRE05. Ejecutar `dotnet build SolarUY.slnx` y registrar el resultado inicial.
- [x] PRE06. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado inicial.
- [x] PRE07. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado inicial de ArchUnitNET.

## 1. Domain

- [x] DOM01. Confirmar que `Medicion`, `Panel` y sus invariantes actuales cubren el requerimiento sin cambios de comportamiento ni relaciones.
- [x] DOM02. Revisar que el diseño de balanceo y sincronización no introduzca dependencias técnicas en Domain; conservar sus entidades y pruebas actuales.

### Control de cierre de Domain

- [x] DOM03. Ejecutar `dotnet build SolarUY.slnx`.
- [x] DOM04. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] DOM05. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar los tres resultados antes de continuar.

## 2. Application

- [x] APP01. Crear `IPanelesMonitoreados` en `SolarUY/Application/Monitoreo` con una operación que devuelva una instantánea de identificadores de paneles observados localmente.
- [x] APP02. Definir que la instantánea excluya paneles sin suscriptores y no exponga colecciones mutables internas, usuarios, callbacks ni tipos de UI o persistencia.
- [x] APP03. Revisar `MonitoreoService.ConsultarAsync`, `LecturaPanelDto.EsPosteriorA` y el repositorio para reutilizar el orden por fecha UTC e identidad durante la sincronización.
- [x] APP04. Revisar que `RegistrarAsync` conserve sus resultados actuales y publique mediante `INotificacionMedicion` únicamente tras confirmar el guardado.
- [x] APP05. Validar que los contratos mantengan la dependencia exclusiva hacia Domain y no introduzcan MediatR ni referencias técnicas externas.

### Control de cierre de Application

- [x] APP06. Ejecutar `dotnet build SolarUY.slnx`.
- [x] APP07. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] APP08. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar los tres resultados antes de continuar.

## 3. Infrastructure

### Configuración privada y conexión existente

- [x] INF01. Incorporar opciones de ejecución para identificar la instancia, configurar el proxy confiable, ubicar las claves compartidas y ajustar el intervalo de sincronización.
- [x] INF02. Validar las opciones del entorno balanceado al iniciar, devolviendo mensajes sin secretos cuando falte configuración obligatoria.
- [x] INF03. Mantener la carga de `appsettings.Local.json` desde el directorio de contenido y la precedencia de las fuentes privadas de ejecución.
- [x] INF04. Implementar la adaptación opcional de loopback al host accesible desde Docker, preservando puerto, base, credenciales y opciones de la conexión original sin modificar el archivo privado.
- [x] INF05. Mantener el arranque sin creación, borrado o copia de bases ni ejecución concurrente de migraciones.

### Autenticación y tráfico reenviado

- [x] INF06. Configurar la persistencia de claves de Data Protection en el directorio compartido cuando se ejecute el entorno balanceado.
- [x] INF07. Configurar la protección de esas claves con el certificado X.509 privado común a las dos instancias.
- [x] INF08. Conservar `SetApplicationName("SolarUY")`, el esquema de autenticación y las opciones actuales de `SolarUY.Sesion.Oidc`.
- [x] INF09. Configurar las cabeceras reenviadas aceptando únicamente la IP reservada de Nginx y un salto de proxy.
- [x] INF10. Restringir los hosts públicos admitidos y conservar el puerto público en la reconstrucción del origen.
- [x] INF11. Conectar el procesamiento de cabeceras en `Program.cs` antes de redirección HTTPS, autenticación, limitación de intentos y antiforgery.
- [x] INF12. Revisar que los callbacks y el cierre de sesión conserven OIDC, PKCE, nonce, correlación y validación de tokens existentes.

### Disponibilidad técnica

- [x] INF13. Implementar un comprobador de disponibilidad que consulte MySQL con un contexto breve, cancelación y tiempo de espera acotado.
- [x] INF14. Publicar `/health/ready` desde el arranque, devolviendo `200` o `503` sin detalles internos.
- [x] INF15. Asegurar que el sondeo HTTP interno pueda obtener el resultado de salud sin redirección HTTPS ni autenticación.

### Sincronización de mediciones

- [x] INF16. Crear el servicio hospedado de sincronización dependiendo de `IPanelesMonitoreados` e `INotificacionMedicion`, sin referencias a Presentation.
- [x] INF17. Implementar el ciclo periódico configurable, con valor inicial de un segundo y sin ejecuciones solapadas.
- [x] INF18. Obtener la instantánea de paneles observados al comenzar cada ciclo y omitir consultas si está vacía.
- [x] INF19. Resolver `MonitoreoService` en un ámbito breve para consultar las últimas lecturas de los paneles observados.
- [x] INF20. Mantener por panel la última lectura difundida y publicar únicamente lecturas posteriores según el orden existente.
- [x] INF21. Retirar el seguimiento de los paneles que dejen de tener suscriptores.
- [x] INF22. Manejar fallos de consulta sin terminar el servicio, con códigos de diagnóstico depurados y reintento en el siguiente ciclo.
- [x] INF23. Implementar la cancelación del ciclo y liberación de ámbitos al detener la aplicación.

El registro final del servicio hospedado se realizará en PRES04, después de implementar el adaptador de `IPanelesMonitoreados`. Hasta entonces, conservar el arranque existente sin activar un servicio con dependencias sin resolver.

### Imagen y Docker Compose en la raíz

- [x] INF24. Crear `.dockerignore` en la raíz excluyendo configuración local privada, certificados, claves, evidencias y resultados de compilación.
- [x] INF25. Crear el `Dockerfile` en la raíz con publicación Release de SolarUY y su cliente mediante una etapa SDK .NET 10.
- [x] INF26. Configurar la etapa de ejecución ASP.NET Core .NET 10 con directorio de contenido definido y escucha HTTP en el puerto interno 8080.
- [x] INF27. Crear `docker-compose.yml` en la raíz con una única imagen `solaruy` reutilizada por `app1` y `app2` e identificadores de instancia distintos.
- [x] INF28. Definir la red privada y reservar una IP configurable para Nginx que coincida con el proxy confiable del servidor.
- [x] INF29. Montar `SolarUY/appsettings.Local.json` en solo lectura en ambas aplicaciones; conectar ambas a la base actual sin añadir un servicio MySQL al arranque habitual.
- [x] INF30. Configurar el volumen persistente compartido de Data Protection y el montaje privado del certificado de protección para ambas instancias.
- [x] INF31. Añadir el servicio Nginx con montaje de `nginx.conf` y certificados TLS en solo lectura, usando versiones de imagen fijadas y compatibles.
- [x] INF32. Publicar `https://localhost:8080` hacia el puerto 443 de Nginx y los puertos de diagnóstico 4000 y 4001 ligados solo a loopback.
- [x] INF33. Configurar healthchecks con una herramienta disponible en la imagen y hacer que Nginx espere aplicaciones saludables mediante `depends_on`.

### Configuración de Nginx

- [x] INF34. Crear `nginx.conf` en la raíz con el upstream `backend` dirigido a `app1:8080` y `app2:8080`.
- [x] INF35. Configurar `zone`, resolución por DNS de Docker y actualización de direcciones de los destinos con una versión que soporte `resolve`.
- [x] INF36. Implementar el `map` de afinidad que reutilice `SolarUY_Afinidad` o tome `$request_id` para la primera solicitud.
- [x] INF37. Configurar `hash $clave_afinidad consistent` y emitir la cookie inicial con el mismo valor usado para seleccionar el destino, conservando las cookies de autenticación del servidor.
- [x] INF38. Aplicar a la cookie de afinidad `HttpOnly`, `Secure`, `SameSite=Lax` y ruta `/`, sin renovarla en cada respuesta.
- [x] INF39. Configurar `max_fails=1`, `fail_timeout=2s`, timeout de conexión de un segundo y reintentos limitados a los dos destinos para los errores previstos.
- [x] INF40. Mantener deshabilitado `non_idempotent` y evitar reenvíos automáticos de escrituras ya enviadas e invocaciones de hub.
- [x] INF41. Configurar el origen reenviado, reemplazando las cabeceras aportadas por el cliente y conservando host, puerto, esquema e IP de origen.
- [x] INF42. Configurar HTTP/1.1, el `map` de Upgrade/Connection, buffering y timeouts de 3600 segundos para los flujos interactivos.
- [x] INF43. Bloquear `/health/ready` en el acceso público y conservar el proxy de recursos estáticos, negociación, hubs y callbacks OIDC.
- [x] INF44. Configurar logs de Nginx sin query strings, tokens o cookies y respuestas de disponibilidad sin detalles internos.
- [x] INF45. Validar la configuración de Compose sin volcar valores privados a evidencias y comprobar la sintaxis con `nginx -t`.
- [x] INF46. Ejecutar `docker compose build` desde la raíz y verificar que la imagen no incluya el archivo local privado ni los certificados de ejecución.

### Control de cierre de Infrastructure

- [x] INF47. Ejecutar `dotnet build SolarUY.slnx`.
- [x] INF48. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] INF49. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar los tres resultados antes de continuar.

## 4. Presentation e integración del arranque

- [x] PRES01. Hacer que `NotificacionMedicion` implemente `IPanelesMonitoreados` devolviendo una instantánea segura ante concurrencia de los paneles con suscripciones activas.
- [x] PRES02. Conservar la autorización de suscripciones para Administrador y Operador y la entrega exclusiva al panel solicitado.
- [x] PRES03. Registrar `IPanelesMonitoreados` e `INotificacionMedicion` sobre la misma instancia singleton de `NotificacionMedicion` en `Program.cs`.
- [x] PRES04. Registrar y activar el servicio hospedado de sincronización una vez resueltas sus dependencias, sin introducir accesos de componentes a Infrastructure.
- [x] PRES05. Conservar la suscripción anterior a la consulta inicial y la reconciliación mediante `EsPosteriorA` en `LecturaEnVivo.razor`.
- [x] PRES06. Verificar que la suscripción se libere al salir o cambiar de panel, sin dejar paneles observados innecesariamente.
- [x] PRES07. Incorporar el aviso de desconexión del circuito Blazor y la acción de recarga cuando la reconexión no pueda recuperar el circuito.
- [x] PRES08. Mantener los mensajes y reintentos del sensor ante interrupciones, la URL relativa del hub y la periodicidad actual del simulador.
- [x] PRES09. Conservar las restricciones del hub para que únicamente Administrador pueda registrar mediciones.
- [x] PRES10. Añadir la cabecera técnica `X-SolarUY-Instancia` desde el pipeline del servidor, sobrescribiendo cualquier valor aportado por el cliente y sin mostrarla como dato de negocio.
- [x] PRES11. Revisar los componentes modificados para asegurar que solo capturen eventos, invoquen Application y muestren datos, sin agregar reglas de negocio.

### Control de cierre de Presentation

- [x] PRES12. Ejecutar `dotnet build SolarUY.slnx`.
- [x] PRES13. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
- [x] PRES14. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar los tres resultados antes de la aceptación funcional.

## 5. Preparación de la aceptación funcional

- [x] VAL01. Crear en `arnes/prompting/tests/req008/` la configuración explícita de aceptación con una base aislada compartida por las dos instancias de prueba y un proyecto Compose identificable.
- [x] VAL02. Incorporar una comprobación del destino aislado que detenga las herramientas antes de escribir si detectan la conexión habitual o un destino no identificado.
- [x] VAL03. Preparar las migraciones existentes en el destino aislado como paso único anterior al arranque, sin agregar cambios de esquema para req008.
- [x] VAL04. Preparar datos aislados de parques, nodos y al menos dos paneles para comprobar persistencia y separación del monitoreo.
- [x] VAL05. Preparar certificados privados y permisos de lectura y escritura de los volúmenes requeridos, sin incorporarlos a Git o evidencias.
- [x] VAL06. Configurar en el proveedor OIDC existente los callbacks y retornos de `https://localhost:8080`, conservando los accesos ya utilizados por el proyecto.
- [x] VAL07. Disponer de sesiones reales de Administrador y Operador para las comprobaciones de autorización, sin guardar credenciales en las herramientas.
- [x] VAL08. Crear una herramienta HTTP o plan JMeter que genere sesiones independientes, conserve sus cookies y registre únicamente identificadores de instancia, estados y tiempos depurados.
- [x] VAL09. Levantar el entorno de aceptación desde la raíz con su configuración explícita y comprobar que ambas instancias estén saludables.

## 6. Criterios de aceptación

### C1. Distribución y afinidad

- [x] C1.1. Crear varias sesiones independientes desde la misma IP y comprobar mediante las cabeceras que ambas instancias atiendan tráfico.
- [x] C1.2. Repetir solicitudes con cada sesión y comprobar un destino estable mientras ambas instancias estén disponibles.
- [x] C1.3. Verificar que la primera solicitud y las negociaciones posteriores de Blazor y SignalR conserven la misma afinidad.

### C2. Autenticación y autorización

- [x] C2.1. Completar el inicio de sesión OIDC real con cada rol desde el origen público y comprobar que no existan redirecciones a puertos internos.
- [x] C2.2. Conservar la cookie autenticada y renovar únicamente la afinidad, sin circuitos activos, hasta comprobar que ambas instancias reconozcan la misma sesión.
- [x] C2.3. Verificar un formulario con antiforgery emitido por una instancia y enviado a la otra usando la misma sesión.
- [x] C2.4. Completar el cierre de sesión real desde el origen público y comprobar que la sesión cerrada deje de permitir acceso protegido.
- [x] C2.5. Comprobar que ambos roles consulten monitoreo, Operador no pueda registrar mediciones y una sesión anónima no pueda monitorear ni publicar.
- [x] C2.6. Verificar las opciones de cookie y el vencimiento de 30 minutos sin renovación deslizante, sin registrar sus valores privados.

### C3. Operaciones y persistencia

- [x] C3.1. Ejecutar las altas existentes de parque, nodo y panel sobre la base aislada a través del balanceador.
- [x] C3.2. Consultar desde B datos guardados por A y repetir en sentido contrario, confirmando las instancias con la cabecera.
- [x] C3.3. Verificar que los permisos de las operaciones existentes se mantengan y que recursos estáticos y navegación funcionen por Nginx.

### C4. Monitoreo entre instancias

- [x] C4.1. Abrir el mismo panel con sesiones autorizadas conectadas a instancias distintas y verificar la lectura inicial persistida.
- [x] C4.2. Registrar una medición en A y comprobar su actualización en la pantalla conectada a B sin recarga en hasta 3 segundos desde el guardado confirmado.
- [x] C4.3. Repetir la comprobación de latencia en sentido B → A.
- [x] C4.4. Verificar que una medición de otro panel no actualice la pantalla observada.
- [x] C4.5. Verificar que una lectura duplicada o anterior no reemplace la más reciente.
- [x] C4.6. Verificar un panel sin historial y confirmar que conserve su estado y la indicación de ausencia de mediciones.
- [x] C4.7. Provocar una escritura fallida en el destino aislado y comprobar que no se difunda una medición no guardada.
- [x] C4.8. Interrumpir y restablecer la consulta de sincronización en el entorno aislado y comprobar que se recupere la última lectura persistida.
- [x] C4.9. Cerrar las pantallas de un panel y comprobar que el sincronizador deje de consultarlo al no tener suscriptores.
- [x] C4.10. Mantener conexiones sin interacción del usuario y comprobar que los keep-alive eviten cierres prematuros; verificar también un transporte alternativo admitido de SignalR.

### C5. Instancia fuera de servicio

- [x] C5.1. Detener `app1` en el proyecto de aceptación y generar tráfico para observar la detección pasiva, registrando los posibles fallos iniciales.
- [x] C5.2. Comprobar que nuevas solicitudes y sesiones puedan utilizar `app2`, incluso con afinidad previa a la instancia detenida.
- [x] C5.3. Verificar el aviso de interrupción y la recuperación mediante reconexión o recarga, manteniendo la sesión autenticada y la última lectura persistida.
- [x] C5.4. Confirmar que una escritura ya enviada no se reproduzca automáticamente en la otra instancia.
- [x] C5.5. Detener también `app2` en el entorno aislado y comprobar el error de disponibilidad `502/504` sin información interna.

### C6. Reincorporación

- [x] C6.1. Restablecer las instancias de aceptación conservando claves y configuración, y mantener tráfico hasta comprobar la reincorporación tras `fail_timeout`.
- [x] C6.2. Verificar que la instancia recuperada reciba nuevas sesiones y que las conexiones WebSocket abiertas en la otra continúen funcionando.
- [x] C6.3. Comprobar la recuperación por recarga de sesiones que hubieran cambiado de destino durante la caída.
- [x] C6.4. Recrear una instancia de prueba y comprobar que Nginx resuelva su dirección vigente sin exigir una IP fija para la aplicación.

### C7. Reproducibilidad y uso habitual

- [x] C7.1. Comprobar mediante revisión de configuración que el Compose predeterminado monta el archivo local y apunta a la base actual, sin crear un MySQL alternativo ni ejecutar migraciones automáticas.
- [x] C7.2. Comprobar el arranque habitual desde la raíz y la conectividad de ambas instancias con la base actual mediante sondeos de solo lectura; ejecutar los escenarios de escritura y fallo únicamente en aceptación aislada.
- [x] C7.3. Verificar que `SolarUY/appsettings.Local.json` conserve su contenido original y que ningún archivo privado haya sido incluido en Git, la imagen o los logs.
- [x] C7.4. Guardar en `artifacts/req008/` resultados depurados por criterio, distinguiendo aceptación real, comprobaciones auxiliares y pendientes externos.

## 7. Documentación y cierre

- [x] DOC01. Crear `arnes/prompting/tests/req008/configuracion.md` con los prerrequisitos y la preparación privada de certificados y permisos.
- [x] DOC02. Documentar el uso de la base actual y la adaptación de acceso desde Docker sin modificar `SolarUY/appsettings.Local.json`.
- [x] DOC03. Documentar los comandos habituales desde la raíz: construcción, arranque, logs, detención de una instancia, reincorporación y apagado sin eliminar volúmenes.
- [x] DOC04. Documentar por separado la preparación y selección explícita de la base y el proyecto Compose de aceptación aislada.
- [x] DOC05. Documentar cómo reproducir C1 a C7, incluidas las sesiones de ambos roles, los callbacks OIDC reales y los tiempos medidos de monitoreo.
- [x] DOC06. Explicar en la guía la detección pasiva, el alcance de `fail_timeout`, la afinidad y los casos que requieren recargar el circuito Blazor.
- [x] DOC07. Documentar el apagado y la limpieza de recursos propios de aceptación sin eliminar volúmenes ni datos habituales.
- [x] DOC08. Revisar nomenclatura, responsabilidades, dependencias entre capas y ausencia de reglas de negocio nuevas en componentes Blazor.
- [x] DOC09. Ejecutar los tres controles finales: `dotnet build SolarUY.slnx`, pruebas de Domain y pruebas de `CodeQuality.Tests` con las rutas indicadas en los cierres de capa.
- [x] DOC10. Revisar el diff final, ejecutar `git diff --check` y confirmar que los diagramas y tests estructurales permanezcan intactos.
- [x] DOC11. Actualizar las casillas verificadas y registrar los controles y aceptaciones pendientes; no marcar como realizada una aceptación externa mediante una simulación o inspección.

## Registro de ejecución

- Los controles de cierre de las cuatro capas pasaron: compilación, 76 pruebas de Domain y 17 de ArchUnitNET. Se utilizó Release después del control inicial para evitar archivos Debug bloqueados por una aplicación abierta. Las advertencias NU1903 de la dependencia WebAssembly del servidor ya estaban presentes al comenzar.
- El despliegue habitual utiliza la base actual y el archivo privado original montado en solo lectura. Ambas instancias respondieron `200` a sus sondeos de disponibilidad; el hash del archivo se conserva. En este equipo `.env` selecciona el puerto 8443 porque otro proceso ocupa 8080; el Compose mantiene 8080 como valor predeterminado configurable.
- Los escenarios funcionales se ejecutaron contra contenedores y MySQL reales en `solaruy-req008-aceptacion`, usando sesiones sintéticas privadas. Incluyeron formularios y altas, afinidad, actualización entre instancias, escrituras fallidas, Long Polling, interrupción de MySQL, caída y recreación de servidores y recuperación de circuitos. El escenario específico C5.4 interrumpió un POST ya recibido por app1 y verificó que no fuera reproducido en app2.
- Se configuraron y guardaron en Auth0 los callbacks de inicio y cierre equivalentes en 8443 (habitual) y 9443 (aceptación aislada), conservando las entradas anteriores de 7178. El proveedor completó el acceso real de Administrador y Operador por el origen público, sin puertos internos. El entorno habitual permaneció encendido durante la aceptación OIDC aislada, con diagnóstico en 14000/14001.
- C2 se verificó con sesiones emitidas por Auth0 real. Para ambos roles se renovó únicamente `SolarUY_Afinidad`, sin circuitos activos, usando un auxiliar exclusivo de Nginx de aceptación que no lee ni modifica la cookie autenticada. Se seleccionó cada destino aislado y se contrastaron los resultados con sus registros depurados. La misma sesión siguió autorizada en app1 y app2. Un formulario emitido por app1 se guardó correctamente en app2 sobre la base aislada.
- Ambos roles consultaron monitoreo. Administrador confirmó un envío del sensor; la negociación de publicación devolvió 403 con Operador y 401 sin sesión. El cierre OIDC real de cada rol dejó de permitir el acceso protegido. El diagnóstico acotado, autorizado expresamente por el usuario, confirmó sobre una emisión real HttpOnly, Secure, SameSite=Lax, sesión no persistente, duración de 30 minutos y ausencia de renovación deslizante. Se registraron únicamente metadatos, sin valores de cookies, tokens ni identidad; no se simuló el transcurso de 30 minutos.
- Evidencia OIDC real: `artifacts/req008/oidc-real.json`, `oidc-metadatos-sesion.txt`, capturas de permisos y callbacks. Las comprobaciones con sesiones sintéticas permanecen identificadas por separado.
- Cierre final: 138 subtareas verificadas y ninguna pendiente. Se repitieron compilación, 76 pruebas de Domain y 17 de ArchUnitNET con resultado satisfactorio; logs `artifacts/req008/cierre-oidc-*.log`. Se retiró el diagnóstico temporal, se apagó exclusivamente el proyecto de aceptación conservando sus volúmenes y se restableció el acceso habitual como Administrador en 8443. Ambas instancias habituales siguen saludables; el hash de `appsettings.Local.json` coincide con el original.
- Evidencias depuradas: `artifacts/req008/funcional.json`, `no-reintento.json`, `base-habitual-solo-lectura.json`, capturas y logs de controles por capa. El material privado de certificados y sesiones se conserva fuera de esas evidencias y fuera de Git.
