# mod002 — Plan de implementación: Cambios Variados

## Base y forma de seguimiento

Fuente de diseño: [specs.md](specs.md), nombre existente en esta carpeta; la referencia a `spec.md` se interpreta como ese documento. Alcance: los cuatro criterios de [requirements.md](requirements.md).

Las casillas reflejan las verificaciones ejecutadas. Evidencias y controles por etapa: `artifacts/mod002/resultados.md`. La aceptación funcional usa HTTP SSR y MySQL reales con identidades auxiliares locales; no acredita un ingreso OIDC externo. La migración se probó únicamente en bases aisladas y no se aplicó a la base habitual.

Orden de ejecución: preparación → Domain → Application A → Infrastructure → Application B → Presentation → aceptación final. Application se divide en dos etapas para incorporar cada nueva firma del repositorio junto con su implementación real, sin dejar la solución sin compilar ni agregar implementaciones ficticias. No avanzar de etapa hasta aprobar compilación, Domain y ArchUnitNET; después de cualquier corrección, repetir los controles afectados antes de avanzar.

## 0. Preparación

- [x] P01. Revisar `AGENTS.md`, las siete reglas compartidas y los diagramas de `.agents/Images`, sin modificarlos.
- [x] P02. Registrar los cambios preexistentes del árbol de trabajo para distinguirlos de mod002 y conservarlos.
- [x] P03. Registrar las decisiones de `specs.md` que guían la implementación: unicidad global por tipo, recorte externo, distinción de mayúsculas/acentos y máximo propuesto de 255 caracteres.
- [x] P04. Identificar un destino MySQL aislado para migraciones, escrituras y pruebas de errores, conservando `SolarUY/appsettings.Local.json` y la base habitual del usuario.
- [x] P05. Ejecutar `dotnet build SolarUY.slnx` desde la raíz y registrar el resultado inicial.
- [x] P06. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y registrar el resultado inicial.
- [x] P07. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y registrar el resultado inicial de ArchUnitNET.
- [x] P08. Resolver fallos que impidan continuar y distinguir advertencias preexistentes de errores nuevos, sin alterar los tests estructurales.

## 1. Domain

### Validación de series y pruebas xUnit

- [x] D01. Agregar pruebas para que `Inversor.Crear` acepte una serie recortada de 255 caracteres y rechace una de 256 con el campo `NumeroSerieInversor`.
- [x] D02. Implementar el límite en `Inversor.Crear` sobre el valor recortado, conservando obligatoriedad, encapsulación y relaciones.
- [x] D03. Agregar pruebas para que `Sensor.Crear` acepte una serie recortada de 255 caracteres y rechace una de 256 con el campo `NumeroSerieSensor`.
- [x] D04. Implementar el límite en `Sensor.Crear` sobre el valor recortado, conservando obligatoriedad y endpoint opcional.
- [x] D05. Verificar mediante pruebas de ambos equipos el rechazo de series nulas, vacías y formadas solo por espacios.
- [x] D06. Agregar pruebas de ambos equipos que comprueben el recorte externo y la conservación de espacios internos, mayúsculas y acentos.
- [x] D07. Verificar mediante las pruebas existentes que las relaciones y colecciones protegidas, `CantidadPaneles` y `CapacidadTotalEstimadaMw` conservan su comportamiento.
- [x] D08. Revisar que Domain siga usando C# puro y que no se le agreguen consultas de unicidad global ni dependencias de Application o Infrastructure.

### Cierre de Domain

- [x] D09. Ejecutar `dotnet build SolarUY.slnx` y corregir errores.
- [x] D10. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y aprobar todas las pruebas.
- [x] D11. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y aprobar ArchUnitNET.
- [x] D12. Registrar los resultados y habilitar Application A únicamente si los tres controles pasan.

## 2. Application

### Etapa A — Detalle y contratos de resultados

Ejecutar después de Domain. Las firmas nuevas de consulta se incorporan en I03/I04 junto con las implementaciones correspondientes; sus consumidores se completan en la etapa B.

- [x] A01. Agregar `IReadOnlyList<NodoDto> Nodos` a `ResultadoDetalleParqueSolarDto`, conservando `ParqueSolarDto` sin colecciones.
- [x] A02. Ajustar las construcciones del resultado de detalle para devolver una colección vacía en autenticación requerida y parque inexistente.
- [x] A03. Mapear en `ParqueSolarService.ObtenerPorIdAsync` cada nodo del parque a los DTO existentes de nodo, inversor y sensor, con todos sus datos.
- [x] A04. Ordenar los nodos por nombre y luego por ID antes de devolverlos; devolver una colección vacía para parques sin nodos.
- [x] A05. Agregar `NumeroSerieInversorDuplicado` y `NumeroSerieSensorDuplicado` a `CodigoErrorAltaNodo`.
- [x] A06. Agregar `ConflictoNumeroSerieInversor` y `ConflictoNumeroSerieSensor` a `ResultadoGuardadoNodo`.
- [x] A07. Preparar en `NodoService` el error de inversor duplicado con el mensaje especificado y el campo `NumeroSerieInversor`, sin revelar el parque del equipo existente.
- [x] A08. Preparar en `NodoService` el error de sensor duplicado con el mensaje especificado y el campo `NumeroSerieSensor`, sin revelar el parque del equipo existente.
- [x] A09. Sustituir la interpretación binaria actual del guardado por el tratamiento explícito de guardado exitoso, parque inexistente y ambos conflictos; ningún resultado de conflicto debe producir éxito.
- [x] A10. Revisar que las lecturas sigan requiriendo autenticación, las altas sigan requiriendo administrador y Application dependa únicamente de Domain.
- [x] A11. Ejecutar `dotnet build SolarUY.slnx` y corregir errores.
- [x] A12. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y aprobar todas las pruebas.
- [x] A13. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y aprobar ArchUnitNET.
- [x] A14. Registrar los resultados y habilitar Infrastructure únicamente si los tres controles pasan.

### Etapa B — Comprobaciones previas al guardado

Ejecutar solamente después de cerrar Infrastructure y disponer de las consultas reales de I03/I04.

- [x] A15. Consultar en `NodoService.CrearAsync` la existencia global de la serie del inversor validada y recortada por Domain, antes de guardar.
- [x] A16. Consultar en el mismo flujo la existencia global de la serie del sensor validada y recortada por Domain, antes de guardar.
- [x] A17. Acumular ambos errores de campo si las dos series ya existen y terminar sin invocar el guardado cuando haya duplicados.
- [x] A18. Revisar que un conflicto detectado durante el guardado concurrente utilice los mismos mensajes y campos de la comprobación previa.
- [x] A19. Revisar responsabilidad única, inyección por constructor, propagación de `CancellationToken` y ausencia de detalles SQL, EF Core o Blazor en los servicios.
- [x] A20. Ejecutar `dotnet build SolarUY.slnx` y corregir errores.
- [x] A21. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y aprobar todas las pruebas.
- [x] A22. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y aprobar ArchUnitNET.
- [x] A23. Registrar los resultados y habilitar Presentation únicamente si los tres controles pasan.

## 3. Infrastructure

Ejecutar después de Application A y antes de Application B. Las consultas y sus firmas se agregan como cambios atómicos de compatibilidad del contrato existente; no se usan stubs ni métodos que devuelvan resultados ficticios.

### Lecturas, comparación e índices

- [x] I01. Verificar que `ParqueSolarRepository.ObtenerPorIdAsync` mantenga la carga sin seguimiento de `Nodos → Inversor → Sensor` y que el listado general no incorpore esa carga.
- [x] I02. Identificar y verificar en el MySQL aislado una comparación binaria sin padding compatible con la versión utilizada y con la semántica de series de `specs.md`.
- [x] I03. Agregar a `INodoRepository` la firma `ExisteNumeroSerieInversorAsync` e implementar en `NodoRepository` su consulta real con `AnyAsync`, sin filtrar por parque y con `CancellationToken`.
- [x] I04. Agregar a `INodoRepository` la firma `ExisteNumeroSerieSensorAsync` e implementar en `NodoRepository` su consulta real con `AnyAsync`, sin filtrar por parque y con `CancellationToken`.
- [x] I05. Mapear `Inversor.NumeroSerie` como `varchar(255)` obligatorio con la comparación elegida y el índice único completo `UX_Inversores_NumeroSerie`.
- [x] I06. Mapear `Sensor.NumeroSerie` como `varchar(255)` obligatorio con la misma semántica y el índice único completo `UX_Sensores_NumeroSerie`.
- [x] I07. Verificar que consultas e índices usen la misma igualdad y que ningún índice se limite a un prefijo o incluya el parque como parte de su clave.

### Guardado y traducción de conflictos

- [x] I08. Identificar de manera precisa la violación del índice `UX_Inversores_NumeroSerie` y traducirla a `ConflictoNumeroSerieInversor`.
- [x] I09. Identificar de manera precisa la violación del índice `UX_Sensores_NumeroSerie` y traducirla a `ConflictoNumeroSerieSensor`.
- [x] I10. Conservar el reconocimiento de parque eliminado y la propagación de fallos técnicos ajenos a los índices identificados.
- [x] I11. Revisar que nodo, inversor y sensor permanezcan en una misma transacción, con reversión completa y limpieza del seguimiento ante conflictos.

### Migración segura y comprobaciones con MySQL

- [x] I12. Preparar la comprobación previa de longitudes y duplicados por tipo con el recorte y la comparación definidos, sin registrar series ni datos sensibles en las evidencias.
- [x] I13. Incorporar un bloqueo previo a cualquier alteración si se detectan datos incompatibles; no truncar, renombrar ni eliminar equipos automáticamente.
- [x] I14. Generar una migración nueva para columnas e índices y actualizar el snapshot, sin reescribir migraciones anteriores.
- [x] I15. Revisar el código y el SQL de la migración para comprobar que solo afecta las columnas e índices previstos y que el control previo ocurre antes de las alteraciones.
- [x] I16. Aplicar la migración a una base aislada con datos compatibles y verificar conservación de registros, relaciones y valores.
- [x] I17. Verificar en un destino aislado con series duplicadas que la aplicación de la migración se detiene antes de alterar esquema o datos.
- [x] I18. Verificar en un destino aislado con series mayores de 255 caracteres que la aplicación de la migración se detiene sin truncar ni alterar datos.
- [x] I19. Comprobar contra MySQL real que los dos índices rechazan duplicados incluso al guardar sin la consulta previa de Application.
- [x] I20. Comprobar que un conflicto de cada tipo revierte los tres registros del alta y permite una operación posterior válida sin reutilizar entidades rechazadas.
- [x] I21. Verificar que un fallo técnico distinto de duplicidad no se traduzca a un error de serie ni exponga mensajes SQL al usuario.

### Cierre de Infrastructure

- [x] I22. Ejecutar `dotnet build SolarUY.slnx` y corregir errores.
- [x] I23. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y aprobar todas las pruebas.
- [x] I24. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y aprobar ArchUnitNET.
- [x] I25. Registrar resultados de controles y migración; volver a Application B únicamente si los tres controles pasan.

## 4. Presentation

Ejecutar después de Application B.

### Alta de parque y alerta en el detalle

- [x] V01. Inyectar `NavigationManager` en `AltaParqueSolar.razor`.
- [x] V02. Navegar a `/parques-solares/{id}?creado=true` únicamente cuando Application confirme el alta, usando el ID de su respuesta.
- [x] V03. Retirar la confirmación local de alta satisfactoria y conservar formulario, datos ingresados y errores cuando el alta falle.
- [x] V04. Recibir `creado` mediante `SupplyParameterFromQuery` en `DetalleParqueSolar.razor`, manteniendo el flujo de servidor actual.
- [x] V05. Mostrar la alerta «El parque solar se creó correctamente.» con `role="status"` únicamente cuando el indicador esté activo y el detalle se cargue correctamente.
- [x] V06. Conservar el acceso «+ Agregar nodo» para administradores en el detalle del parque creado.

### Nodos y errores de series

- [x] V07. Renderizar la sección «Nodos» a partir de la colección devuelta por Application, respetando su orden.
- [x] V08. Mostrar ID, nombre, latitud, longitud y cantidad de paneles de cada nodo, con formato cultural en las coordenadas.
- [x] V09. Mostrar ID, nombre y número de serie del inversor de cada nodo.
- [x] V10. Mostrar ID, nombre, serie y endpoint del sensor, usando «Sin endpoint» si no tiene valor.
- [x] V11. Mostrar «El parque solar todavía no tiene nodos.» cuando la colección esté vacía.
- [x] V12. Organizar los datos con encabezados accesibles y un diseño adaptable a pantallas pequeñas.
- [x] V13. Conservar la consulta actualizada al visitar el detalle y el enlace de retorno de `AltaNodo.razor`, sin introducir una nueva redirección de alta de nodo.
- [x] V14. Integrar los dos códigos de duplicidad con el `ValidationMessageStore` existente de `AltaNodo.razor`, asociados a sus respectivos campos y conservando el formulario para corregir y reenviar.
- [x] V15. Revisar que la UI solo capture eventos, invoque Application y muestre resultados, sin decidir unicidad ni acceder a Infrastructure.

### Cierre de Presentation

- [x] V16. Ejecutar `dotnet build SolarUY.slnx` y corregir errores.
- [x] V17. Ejecutar `dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y aprobar todas las pruebas.
- [x] V18. Ejecutar `dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y aprobar ArchUnitNET.
- [x] V19. Registrar los resultados y habilitar la aceptación funcional únicamente si los tres controles pasan.

## 5. Aceptación funcional y revisión final

Realizar las escrituras y pruebas de fallos exclusivamente sobre el destino aislado. Si se requieren scripts auxiliares, guardarlos en `arnes/prompting/tests/mod002/`; son comprobaciones funcionales, no nuevas pruebas unitarias de Application, Infrastructure o Presentation. Una inspección o simulación no sustituye la comprobación del flujo real.

### Criterio 1 — Alta y navegación

- [x] F01. Crear un parque como administrador y comprobar que la URL final corresponde al ID devuelto y que se muestra la alerta de éxito en el detalle.
- [x] F02. Comprobar que «+ Agregar nodo» abre el alta para ese mismo parque.
- [x] F03. Enviar un alta de parque inválida y verificar que se conserva el formulario con errores, sin redirección de éxito.
- [x] F04. Recargar el detalle después del alta y comprobar que no se crea otro parque.
- [x] F05. Abrir el detalle sin `creado=true` y comprobar que no aparece la alerta; abrir un ID inexistente con ese indicador y comprobar que tampoco aparece.

### Criterio 2 — Datos de nodos

- [x] F06. Consultar un parque sin nodos y verificar el mensaje de colección vacía.
- [x] F07. Crear un nodo y volver por el enlace existente al detalle; cotejar todos los datos de nodo, inversor y sensor con lo guardado.
- [x] F08. Recargar el detalle y comprobar que el nodo sigue visible; consultar otro parque y comprobar que no contiene ese nodo.
- [x] F09. Verificar un sensor sin endpoint y otro con endpoint, junto con el formato de coordenadas y la visualización en una pantalla pequeña.

### Criterios 3 y 4 — Unicidad y concurrencia

- [x] F10. Repetir una serie de inversor dentro del mismo parque, con serie de sensor nueva, y comprobar el error del inversor y la ausencia de altas parciales.
- [x] F11. Repetir una serie de inversor en otro parque y comprobar el mismo rechazo global.
- [x] F12. Repetir una serie de sensor dentro del mismo parque, con serie de inversor nueva, y comprobar el error del sensor y la ausencia de altas parciales.
- [x] F13. Repetir una serie de sensor en otro parque y comprobar el mismo rechazo global.
- [x] F14. Enviar ambas series ya existentes y comprobar que la validación previa informa los dos campos; corregirlas y reenviar con éxito.
- [x] F15. Repetir series con espacios externos y comprobar que el recorte no permite eludir la unicidad de ningún tipo de equipo.
- [x] F16. Usar series que difieran solo en mayúsculas o acentos y comprobar que se aceptan como distintas para ambos tipos de equipo.
- [x] F17. Usar el mismo texto en un inversor y un sensor y comprobar que se permite por tratarse de tipos independientes.
- [x] F18. Verificar desde el formulario que se aceptan series recortadas de 255 caracteres y se rechazan las de 256, con mensajes en los campos correctos.
- [x] F19. Ejecutar dos altas concurrentes con la misma serie de inversor y sensores distintos; comprobar una sola confirmación, un error de inversor y ningún registro huérfano.
- [x] F20. Ejecutar dos altas concurrentes con la misma serie de sensor e inversores distintos; comprobar una sola confirmación, un error de sensor y ningún registro huérfano.
- [x] F21. Correlacionar la evidencia de las carreras con el rechazo del índice único, para no confundir un rechazo de la consulta previa con una validación de la protección concurrente.

### Regresiones y cierre

- [x] F22. Verificar que un usuario anónimo no accede al detalle y que un usuario autenticado sin rol administrador puede leerlo pero no registrar parques o nodos.
- [x] F23. Verificar que se conservan el listado de parques, la unicidad del nombre del parque y los estados de parque inexistente y fallo de carga.
- [x] F24. Revisar el diff final para confirmar nomenclatura, SOLID, límites de capas y ausencia de cambios en diagramas, documentos de `arnes/prompting/requerimientos/`, configuración privada y tests estructurales.
- [x] F25. Registrar en `artifacts/` evidencias depuradas de los cuatro criterios, resultados de los controles y limitaciones o verificaciones pendientes, sin secretos ni valores de cookies.
- [x] F26. Si la aceptación exigió cambios de código, repetir compilación, pruebas de Domain y ArchUnitNET antes del cierre; si no hubo cambios, referenciar los controles aprobados de la última etapa.
- [x] F27. Revisar las casillas contra las evidencias y comunicar el estado de implementación, sin marcar como verificadas las tareas que sigan pendientes.
