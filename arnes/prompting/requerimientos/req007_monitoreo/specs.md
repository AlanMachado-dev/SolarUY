# REQ007 — Monitoreo en tiempo real de paneles

El flujo sigue `Diagrama_WebAssembly.png`. El sensor en WebAssembly consulta al inversor simulado cada 30 segundos y envía la lectura al servidor por SignalR. El servidor la guarda y actualiza las vistas de monitoreo suscritas al panel.

## Domain

- Incorporar `Medicion` como entidad vinculada a un `Panel` existente, con identidad, fecha UTC, potencia instantánea en W y `EstadoPanel`. Cada panel admite cero o más mediciones. Usar `GeneracionActualW` para la potencia solicitada en REQ007. El atributo `EnergiaGenerada` del diagrama corresponde al dato de la medición, cuya unidad en este requerimiento es W.
- Encapsular la creación de la medición y validar que la potencia sea finita y no negativa, que el estado pertenezca al enumerado y que la fecha sea UTC.
- Cuando exista una medición, su estado y potencia representan la lectura actual del panel. Sin historial, conservar el estado del panel y presentar la generación como no disponible.

## Application

- Incorporar `MonitoreoService` para consultar la última medición de un panel y registrar una lectura. Distinguir panel inexistente de panel sin mediciones. Devolver DTO con el identificador del panel, la fecha, la generación y el estado listos para mostrar.
- Para registrar, resolver el panel por su identificador, asignar la fecha UTC del servidor, construir la medición con el comportamiento de Domain y persistir mediante `IMedicionRepository`. Publicar la lectura confirmada mediante un puerto de notificación solo después de guardar. Un fallo de persistencia debe devolver un error al sensor sin emitir una actualización.
- Seleccionar la última lectura por fecha UTC descendente, con la identidad como desempate estable. Aplicar el mismo orden al procesar actualizaciones para evitar que una lectura atrasada reemplace una más reciente.

## Infrastructure

- Implementar `MedicionRepository` con EF Core y registrar las dependencias en el arranque. Incorporar una migración para la tabla `Medicion` con clave foránea obligatoria a `Panel` e índice para consultar la última medición por panel. Los paneles existentes comienzan con historial vacío.
- Ejecutar las operaciones de persistencia en ámbitos breves por operación, de modo que el circuito de Blazor y las llamadas de SignalR no compartan un contexto de datos de larga duración.

## Presentation

- En `DetalleNodo.razor`, agregar el botón **Monitoreo** para cada panel. La página interactiva de servidor consulta la última medición al abrirse y muestra estado, potencia en W y fecha. Si falta historial, muestra el estado registrado y una indicación de que aún no hay mediciones.
- Autorizar en servidor el acceso a la página y a la suscripción para **Administrador** y **Operador**. Limitar la publicación de lecturas del simulador a **Administrador** como decisión de diseño para separar consulta e ingesta. Validar la autorización y la existencia del panel en el servidor antes de registrar la lectura.
- El hub de SignalR recibe lecturas y delega el registro a Application. Un adaptador del puerto de notificación entrega las lecturas confirmadas a la vista interactiva, mediante suscripciones por panel en el servidor. Suscribir antes de cargar la lectura inicial y reconciliar ambas fuentes con el orden de Application. Liberar la suscripción al abandonar la página.
- En `SolarUY.Client`, alojar un componente de sensor con un `InversorSimulado` que produzca estados válidos y potencias coherentes con la potencia nominal del panel. Como alcance de la simulación, habilitar el sensor para el panel abierto en una sesión de Administrador. Mantener un ciclo de 30 segundos sin envíos solapados, iniciarlo cuando el componente WebAssembly esté activo y cancelarlo al salir o cambiar de panel. Tras una desconexión, retomar el envío al recuperar la conexión.

## Verificación

- Cubrir con pruebas unitarias de Domain la creación y el rechazo de lecturas inválidas. Ejecutar los controles de compilación, Domain y ArchUnitNET al completar cada capa.
- Verificar con datos aislados que ambos roles consulten una lectura persistida al entrar y reciban actualizaciones del panel seleccionado, que un panel sin historial se muestre correctamente y que una escritura fallida no se refleje en pantalla. Comprobar que Operador no puede publicar y que una sesión sin autorización no puede consultar ni publicar.
