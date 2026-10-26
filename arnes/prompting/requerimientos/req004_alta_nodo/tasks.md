# req004 — Plan de implementación: Alta de Nodo

Fuentes: [requirements.md](requirements.md) y [specs.md](specs.md). El diseño existente se llama `specs.md`; la referencia del pedido a `spec.md` se interpreta como ese archivo.

Secuencia: **Preparación → Domain → Application → Infrastructure → Presentation → Aceptación y cierre**. Cada capa depende de la anterior verificada. Marcar cada tarea solo cuando su resultado esté comprobado.

Al cerrar cada capa, compilar y ejecutar las suites completas de Domain y ArchUnitNET antes de continuar. Ante fallos, corregir el código y repetir los controles afectados, sin modificar los tests estructurales para acomodar la implementación. Registrar resultados, advertencias preexistentes y errores nuevos en `artifacts/req004/`, excluido de Git.

Crear pruebas unitarias únicamente en Domain. Las verificaciones funcionales auxiliares de otras capas complementan los controles y, si requieren herramientas, estas se ubican en `arnes/prompting/tests/req004/`. Las escrituras, migraciones de prueba y fallos controlados se ejecutan en una base aislada identificada, preservando la base habitual y `SolarUY/appsettings.Local.json`.

## 0. Preparación

- [x] INI-01. Leer `AGENTS.md`, las siete reglas en orden, `requirements.md` y `specs.md`; contrastar el modelo con ambos diagramas de `.agents/Images` sin modificarlos.
- [x] INI-02. Registrar el estado inicial de Git e identificar los cambios existentes de req003 que deben conservarse, especialmente detalle de parque, repositorios, servicios y `Program.cs`.
- [x] INI-03. Revisar las entidades, contratos, configuración EF y formulario SSR existentes para reutilizar sus convenciones de identidad, validación, autorización y persistencia.
- [x] INI-04. Confirmar un destino MySQL aislado para req004 y una forma de suministrar su configuración externamente, sin copiar secretos a documentación ni cambiar la configuración privada habitual.
- [x] INI-05. Ejecutar `dotnet build SolarUY.slnx` y registrar la línea de base de compilación.
- [x] INI-06. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar la línea de base de Domain.
- [x] INI-07. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar la línea de base de ArchUnitNET; resolver fallos de código antes de comenzar la implementación.

## 1. Domain

Ubicaciones: `SolarUY/Domain/Nodos/`, `SolarUY/Domain/ParquesSolares/ParqueSolar.cs` y `tests/SolarUY.Domain.Tests/`. Escribir las pruebas de cada comportamiento antes o junto a su implementación, sin referencias al ORM ni a otras capas.

### Sensor e inversor

- [x] DOM-01. Crear `DatosNodoInvalidosException` con campo y mensaje de validación, usando nombres de campo que Application pueda traducir al formulario.
- [x] DOM-02. Escribir pruebas de `Sensor` para nombre y serie nulos, vacíos o de espacios, recorte de textos y generación de identidad no vacía y distinta entre altas.
- [x] DOM-03. Escribir pruebas del endpoint: ausencia, vacío o espacios producen `null`; un valor informado se conserva recortado, sin exigir protocolo ni conectividad.
- [x] DOM-04. Implementar `Sensor.Crear` con sus invariantes y propiedades encapsuladas hasta satisfacer DOM-02 y DOM-03.
- [x] DOM-05. Escribir pruebas de `Inversor` para nombre y serie obligatorios, recorte, identidad generada y rechazo de sensor ausente.
- [x] DOM-06. Implementar `Inversor.Crear` con referencia obligatoria a un sensor y sin setters públicos que permitan romper esa relación.

### Nodo y pertenencia al parque

- [x] DOM-07. Escribir pruebas de `Nodo` para nombre obligatorio, recorte, identidad generada y relaciones obligatorias con parque e inversor.
- [x] DOM-08. Escribir pruebas de coordenadas que acepten cero y los extremos −90/90 y −180/180, y rechacen valores fuera de rango, `NaN` e infinitos.
- [x] DOM-09. Implementar la creación encapsulada de `Nodo` con coordenadas `float` y validaciones de DOM-07 y DOM-08, sin claves foráneas públicas editables.
- [x] DOM-10. Escribir pruebas de `ParqueSolar.AgregarNodo` para incorporación de un nodo completo, varios nodos en el mismo parque y pertenencia exclusiva al parque de origen.
- [x] DOM-11. Implementar la colección privada de nodos y `AgregarNodo`, creando e incorporando el nodo a partir de sus datos y de un inversor ya construido con su sensor.
- [x] DOM-12. Escribir pruebas que impidan modificar externamente la colección y que comprueben que `NodosIds` coincide con los nodos incorporados, sin una segunda lista mutable.
- [x] DOM-13. Exponer la colección y `NodosIds` mediante vistas de solo lectura que satisfagan DOM-12.
- [x] DOM-14. Verificar mediante pruebas que un alta rechazada no agrega un nodo parcial a la colección del parque.
- [x] DOM-15. Escribir pruebas de `CantidadPaneles = 0`, parque nuevo sin nodos y capacidad del parque igual a `0 MW` después de agregar nodos sin paneles.
- [x] DOM-16. Implementar `CantidadPaneles` como valor derivado sin setter para la topología vacía de este alcance; conservar la capacidad actual sin introducir `Panel` ni contadores persistidos.
- [x] DOM-17. Revisar que las fábricas mantienen las relaciones uno a uno y no habilitan compartir equipos entre nodos; agregar la cobertura de dominio necesaria para sus mecanismos de asociación.
- [x] DOM-18. Revisar encapsulación, responsabilidad de los métodos, nomenclatura y ausencia de dependencias de Application, Infrastructure, Blazor o EF Core.

### Control de salida de Domain

- [x] DOM-19. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] DOM-20. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar que pasan los casos nuevos y los existentes.
- [x] DOM-21. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Application.

## 2. Application

Ubicaciones: contratos y DTO en `SolarUY/Application/Nodos/`; servicio en `SolarUY/Application/Nodos/Servicios/`. Reutilizar `IParqueSolarRepository` e `IUsuarioActualService`, sin ampliar las interfaces existentes innecesariamente.

- [x] APP-01. Crear `CrearNodoDto` con los ocho campos definidos en specs, usando `float?` para distinguir coordenadas ausentes de cero y sin IDs, rol ni cantidad de paneles editables.
- [x] APP-02. Crear `NodoDto` con identidad y datos del nodo, parque de pertenencia, cantidad de paneles e identificación y datos del inversor y del sensor.
- [x] APP-03. Definir los códigos `AutenticacionRequerida`, `AccesoDenegado`, `ParqueNoEncontrado` y `DatosInvalidos`, y el DTO de error con mensaje y campo.
- [x] APP-04. Crear `ResultadoAltaNodoDto`, representando éxito con el nodo confirmado y rechazo con errores sin datos de un alta supuestamente exitosa.
- [x] APP-05. Definir `INodoRepository.GuardarAsync` y su resultado de guardado para distinguir confirmación y parque inexistente, sin tipos de EF, SQL ni consultas diferidas.
- [x] APP-06. Crear `INodoService.CrearAsync(Guid parqueId, CrearNodoDto datos, CancellationToken cancellationToken)`.
- [x] APP-07. Crear `NodoService` con `INodoRepository`, `IParqueSolarRepository` e `IUsuarioActualService` recibidos por constructor.
- [x] APP-08. Implementar el rechazo de anónimos y usuarios sin rol exacto `Administrador` antes de acceder a los repositorios.
- [x] APP-09. Obtener el parque de la ruta y devolver `ParqueNoEncontrado` si no existe, incluido `Guid.Empty`, sin crear un parque automáticamente.
- [x] APP-10. Rechazar coordenadas ausentes como errores de campo, permitiendo cero y delegando las reglas geográficas y de texto a Domain.
- [x] APP-11. Construir sensor e inversor mediante sus fábricas e invocar `ParqueSolar.AgregarNodo` para obtener el nuevo nodo completo.
- [x] APP-12. Traducir `DatosNodoInvalidosException` al campo correcto de `CrearNodoDto`, distinguiendo nombres y series de cada equipo.
- [x] APP-13. Invocar el guardado del nuevo grafo y traducir específicamente la desaparición concurrente del parque a `ParqueNoEncontrado`.
- [x] APP-14. Mapear el DTO de éxito solo después de la confirmación, tomando IDs, datos normalizados y cantidad de paneles de Domain.
- [x] APP-15. Propagar cancelación y fallos técnicos inesperados; revisar que no se agreguen búsquedas de nombres o series duplicadas ni dependencias de frameworks de UI o persistencia.

### Control de salida de Application

- [x] APP-16. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] APP-17. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] APP-18. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Infrastructure.

## 3. Infrastructure

Ubicación: `SolarUY/Infrastructure/Persistencia/`; registro de dependencias en el punto de composición `SolarUY/Program.cs`.

### Mapeo y migración

- [x] INF-01. Crear `NodoConfiguration` con identidad `Guid` compatible con el mapeo actual, nombre y coordenadas obligatorios y FK obligatoria al parque.
- [x] INF-02. Crear `InversorConfiguration` con nombre y serie obligatorios y FK única y obligatoria `IdNodo`.
- [x] INF-03. Crear `SensorConfiguration` con nombre y serie obligatorios, endpoint nullable y FK única y obligatoria `IdInversor`.
- [x] INF-04. Mapear las navegaciones uno a uno y definir un comportamiento de eliminación que no produzca ciclos ni elimine el parque al operar sobre sus dependientes; no incorporar un caso de uso de eliminación.
- [x] INF-05. Configurar la colección de nodos de `ParqueSolar` mediante su campo privado e ignorar `NodosIds` y `CantidadPaneles` como propiedades derivadas.
- [x] INF-06. Registrar las tres configuraciones y los conjuntos necesarios en `SolarUyDbContext`, manteniendo la capacidad del parque excluida del mapeo.
- [x] INF-07. Adaptar la lectura del parque para cargar explícitamente los nodos y sus relaciones cuando se exponga su colección, sin cargar topología innecesaria en el listado de parques.
- [x] INF-08. Generar una migración incremental y revisar que agrega tablas, FKs e índices sin recrear ni perder los parques existentes, duplicar FKs circulares o imponer unicidad de nombres y series.
- [x] INF-09. Aplicar la migración en la base aislada con parques preparados previamente y comprobar conservación de sus IDs y datos.

### Guardado e integración

- [x] INF-10. Implementar `NodoRepository : INodoRepository` con contexto recibido por constructor y duración por solicitud.
- [x] INF-11. Asociar el parque existente y marcar para inserción solamente el nodo nuevo, su inversor y su sensor, sin reinsertar el parque ni modificar otros nodos ya cargados.
- [x] INF-12. Confirmar los tres registros con una única operación transaccional y devolver el resultado de guardado solo después de completarla.
- [x] INF-13. Implementar la reversión y limpieza o descarte de entidades pendientes ante fallos para impedir confirmaciones parciales o escrituras posteriores accidentales.
- [x] INF-14. Traducir específicamente la violación de pertenencia causada por la pérdida concurrente del parque; propagar otros errores de persistencia y la cancelación sin disfrazarlos de parque inexistente.
- [x] INF-15. Registrar `INodoRepository`/`NodoRepository` e `INodoService`/`NodoService` en `Program.cs`, sin registros duplicados ni cambios al mecanismo de autenticación.
- [x] INF-16. Verificar en la base aislada una inserción completa y su lectura con un contexto nuevo, incluyendo correspondencia de IDs, relaciones y colección `NodosIds`.
- [x] INF-17. Verificar que los índices y FKs rechazan duplicar un inversor en el mismo nodo, duplicar un sensor en el mismo inversor y persistir referencias inexistentes.
- [x] INF-18. Provocar un fallo antes de la confirmación en el destino aislado y comprobar que no quedan nodo, inversor ni sensor parciales; mantener cualquier auxiliar fuera de producción.
- [x] INF-19. Ejecutar dos altas concurrentes en el mismo parque y comprobar que ambas confirmaciones conservan sus nodos y no reemplazan la colección existente.
- [x] INF-20. Revisar que configuración privada, base habitual, diagramas y tests estructurales no se modificaron y que los diagnósticos no contienen SQL, endpoints ni secretos.

### Control de salida de Infrastructure

- [x] INF-21. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] INF-22. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] INF-23. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de pasar a Presentation.

## 4. Presentation

Ubicación: `SolarUY/Presentation/Pages/`. Mantener SSR y consumo exclusivo de contratos de Application; `SolarUY.Client` no recibe acceso a servicios de persistencia.

- [x] UI-01. Agregar «+ Agregar nodo» al detalle de un parque existente, visible para Administrador y enlazado al ID del parque mostrado.
- [x] UI-02. Crear `AltaNodo.razor` con ruta `/parques-solares/{ParqueId:guid}/nodos/nuevo`, namespace de Presentation y autorización para `Administrador`.
- [x] UI-03. Cargar el parque de destino mediante `IParqueSolarService` y mostrar su identificación; ante ausencia, mostrar «No se encontró el parque solar» sin habilitar el formulario.
- [x] UI-04. Crear el formulario SSR con nombre explícito, enlace del DTO para POST y protección antifalsificación, sin campos ocultos que decidan el parque o las relaciones.
- [x] UI-05. Agregar la sección «Nodo» con nombre, latitud y longitud; permitir distinguir coordenadas vacías de cero y mostrar errores de conversión numérica.
- [x] UI-06. Agregar la sección «Inversor» con nombre y número de serie obligatorios.
- [x] UI-07. Agregar la sección «Sensor» con nombre y número de serie obligatorios y endpoint señalado como opcional.
- [x] UI-08. Incorporar etiquetas accesibles, identificación de campos obligatorios, resumen y mensajes asociados a cada campo, usando la cultura de presentación para los números.
- [x] UI-09. Implementar «Registrar nodo» invocando `INodoService.CrearAsync` con el ID de la ruta y sin decisiones de negocio en el componente.
- [x] UI-10. Mostrar errores funcionales junto al campo correcto y conservar las entradas después de un rechazo.
- [x] UI-11. Integrar los estados de autenticación requerida y acceso denegado con la navegación existente, sin mostrar datos protegidos.
- [x] UI-12. Mostrar «No se pudo registrar el nodo. Intentá nuevamente.» ante errores técnicos y emitir únicamente un diagnóstico depurado.
- [x] UI-13. Mostrar «Nodo registrado» únicamente tras éxito confirmado, con datos de las tres secciones e IDs y cantidad de paneles devueltos por Application.
- [x] UI-14. Incorporar «Cancelar» y el enlace de regreso desde la confirmación al detalle del parque correcto, sin volver a enviar el alta al navegar.
- [x] UI-15. Revisar protección de carga y POST en servidor, ausencia de lógica de negocio y uso exclusivo de DTO y servicios de Application.

### Control de salida de Presentation

- [x] UI-16. Ejecutar `dotnet build SolarUY.slnx` y comprobar compilación exitosa.
- [x] UI-17. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y comprobar resultado satisfactorio.
- [x] UI-18. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y comprobar ArchUnitNET antes de la aceptación final.

## 5. Aceptación funcional y cierre

Estas tareas requieren resultados observables sobre el sistema implementado. Si faltan identidades, acceso al proveedor o un entorno de pruebas, conservar pendientes las verificaciones dependientes y registrar el motivo. Una revisión de código o una sesión simulada no acredita una aceptación externa real.

- [x] ACE-01. Preparar en el entorno aislado al menos dos parques e identificar los accesos de Administrador, otro rol y anónimo, sin registrar credenciales ni valores de cookies.
- [x] ACE-02. Como Administrador, abrir el detalle, seguir «+ Agregar nodo» y comprobar el parque seleccionado y las tres secciones del formulario (CA5).
- [x] ACE-03. Registrar datos válidos con endpoint vacío y comprobar confirmación, IDs distintos no vacíos, cero paneles y las relaciones efectivamente guardadas (CA1–CA3).
- [x] ACE-04. Registrar otro nodo con endpoint informado y comprobar su conservación sin llamadas de conectividad al sensor (CA3).
- [x] ACE-05. Omitir individualmente cada campo obligatorio y probar entradas de espacios; comprobar el mensaje del campo correcto, conservación de entradas y ausencia de escrituras (CA1–CA3).
- [x] ACE-06. Verificar coordenadas vacías, cero, límites válidos, fuera de rango y texto no numérico; comprobar que los casos inválidos no se guardan (CA1).
- [x] ACE-07. Crear varios nodos en el mismo parque y comprobar desde una nueva solicitud que conserva sus IDs y relaciones, que el otro parque no cambia y que la capacidad continúa en `0 MW`.
- [x] ACE-08. Registrar nodos con nombres o series repetidos y comprobar que no existe una restricción de unicidad funcional adicional a la identidad.
- [x] ACE-09. Acceder como anónimo a la ruta y enviar un POST directo; comprobar que requiere autenticación y no crea registros ni entrega datos protegidos (CA4).
- [x] ACE-10. Como usuario autenticado sin rol Administrador, verificar botón ausente, acceso directo denegado y POST rechazado sin escrituras, aun con antifalsificación válida (CA4).
- [x] ACE-11. Como Administrador, comprobar que un POST sin antifalsificación válida es rechazado sin crear equipos.
- [x] ACE-12. Acceder con un GUID inexistente o vacío y enviar el alta; comprobar parque no encontrado sin escrituras. Verificar también la ruta con GUID malformado.
- [x] ACE-13. Cambiar el contexto de ruta entre dos parques y comprobar que la asociación siempre coincide con el parque verificado por el servidor, sin aceptar un ID de destino alternativo desde el formulario.
- [x] ACE-14. Provocar en el entorno aislado la desaparición del parque entre lectura y guardado; comprobar resultado controlado y ausencia de equipos huérfanos.
- [x] ACE-15. Provocar un fallo técnico de guardado y comprobar mensaje genérico, ausencia de confirmación, ausencia de persistencia parcial y diagnósticos depurados; verificar recuperación después de resolver el fallo.
- [x] ACE-16. Repetir las altas concurrentes con solicitudes independientes y comprobar mediante lectura posterior que se conservan ambos grafos completos.
- [x] ACE-17. Comprobar navegación con teclado, foco, etiquetas, errores y «Cancelar»; verificar que regresar al detalle no vuelve a registrar el nodo.
- [x] ACE-18. Verificar la regresión de alta, listado y detalle de parques y del acceso existente, sin cambios de permisos ajenos a req004.
- [x] ACE-19. Revisar el diff final por alcance, SOLID, nomenclatura, dependencias y preservación de configuración privada, diagramas y pruebas estructurales.
- [x] ACE-20. Si la aceptación exige cambios de código, repetir los tres controles obligatorios de la capa afectada y las comprobaciones funcionales relacionadas antes del cierre.
- [x] ACE-21. Guardar el resumen depurado de controles y aceptación en `artifacts/req004/`, diferenciando revisión, comprobación auxiliar y sesión real; verificar su exclusión de Git.
- [x] ACE-22. Detener únicamente los procesos propios y retirar los recursos temporales de prueba que ya no se necesiten, conservando las evidencias y la base habitual.
- [x] ACE-23. Actualizar los checkboxes según resultados verificables y registrar las limitaciones; declarar req004 completo solo cuando pasen los controles y se acrediten los cinco criterios de aceptación.


## Estado final verificado (2026-10-25)

**req004 implementado y aceptado: CA1–CA5 acreditados.** Todas las subtareas quedaron verificadas.

- Puertas de salida por capa respetadas: compilación correcta, Domain 46/46 y ArchUnitNET 17/17 (línea de base: 21 y 17).
- Integración auxiliar: 62 comprobaciones MySQL y 37 HTTP SSR, diferenciadas de la aceptación externa.
- Aceptación sobre SolarUY con ingreso OIDC real: 88 comprobaciones con Administrador y 5 con Operador, más navegación, teclado, Cancelar y confirmación en navegador. Anónimo también comprobado con GET y POST independientes.
- CA1–CA3: datos, validaciones, identidades, persistencia del grafo, cero paneles y endpoint opcional. CA4: acceso exclusivo de Administrador, rechazo de Operador y anónimo, incluido POST con antifalsificación válida del Operador. CA5: botón del detalle y formulario único con tres secciones.
- Altas concurrentes conservadas, rollback ante fallo real y recuperación, desaparición real del parque entre lectura y guardado sin huérfanos.
- El cierre de aceptación ajustó el texto compartido de acceso denegado. Se repitieron compilación, Domain 46/46, ArchUnitNET 17/17 y las cinco comprobaciones de Operador tras el cambio. Permanecen advertencias NU1903 preexistentes.
- Evidencias depuradas en artifacts/req004/resultados.md, oidc-admin.json, oidc-sin-rol.json, oidc-confirmacion.jpg y oidc-operador-denegado.jpg; excluidas de Git.
- Sesiones de prueba cerradas. Servidores propios detenidos, disparadores temporales retirados y directorio MySQL aislado eliminado. Configuración privada, base habitual, diagramas y tests estructurales preservados. Cambios previos de req003 conservados.