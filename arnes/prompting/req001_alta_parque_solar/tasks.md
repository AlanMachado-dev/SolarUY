# Plan de implementación: Alta de Parque Solar

Base del plan: [specs.md](./specs.md) y [requirements.md](./requirements.md). El archivo de diseño existente se llama `specs.md`.

Las cuatro capas se implementan como carpetas y namespaces dentro de `SolarUY/SolarUY.csproj`. El proyecto `SolarUY.Client` conserva su función actual; el alta se ejecuta mediante Blazor SSR en el servidor. Los proyectos de pruebas son auxiliares y no separan las capas de la aplicación en distintos `.csproj`.

Cada checkbox representa una tarea pendiente. Marcarlo solamente cuando su resultado esté implementado o verificado. Conservar evidencia de los controles en el apartado final de este documento.

## 0. Preparación y decisiones pendientes

Estas decisiones preceden a las tareas que dependen de ellas; no requieren desarrollar funcionalidades ajenas al alta.

- [x] P01. Confirmar la equivalencia de nombres propuesta: Trim, Unicode Form C y mayúsculas invariantes, conservando acentos y espacios interiores.
- [x] P02. Confirmar que ubicación se representa mediante un único texto obligatorio de localidad / dirección.
- [x] P03. Elegir el proveedor de EF Core para el entorno de implementación y registrar la decisión.
- [x] P04. Definir la comparación de la clave indexada en el proveedor elegido para que coincida con la equivalencia de nombres del dominio.
- [x] P05. Definir los límites de texto requeridos por el almacenamiento y el índice; acordar cualquier restricción funcional adicional antes de introducirla.
- [x] P06. Definir el mecanismo de autenticación del servidor y la fuente confiable del rol exacto `Administrador`.
- [x] P07. Definir el procedimiento de desarrollo para disponer de un usuario administrador y otro autenticado sin ese rol.
- [x] P08. Registrar las decisiones confirmadas en `specs.md` y reflejar en `requirements.md` únicamente los cambios funcionales acordados.
- [x] P09. Verificar la disponibilidad del SDK de .NET 10 para compilar la solución.
- [x] P10. Ejecutar una compilación inicial de `SolarUY.slnx` y registrar los errores o advertencias preexistentes.
- [x] P11. Crear las carpetas `Domain/ParquesSolares`, `Application/ParquesSolares`, `Infrastructure/Persistencia`, `Infrastructure/Seguridad` y `Presentation/Pages` dentro de `SolarUY`.
- [x] P12. Definir la correspondencia entre esas carpetas y los namespaces `SolarUY.Domain`, `SolarUY.Application`, `SolarUY.Infrastructure` y `SolarUY.Presentation`.

Resultado: estructura modular y decisiones suficientes para diseñar el almacenamiento y la seguridad, sin crear nuevos proyectos de capas.

## 1. Preparar la validación arquitectónica y de dominio

El arnés actual todavía no evalúa SolarUY: `ArchitectureSetup.cs` carga `System.Object`, `CodeQuality.Tests` apunta a `net8.0` y no figura en `SolarUY.slnx`.

También existe un conflicto concreto: `ApplicationServices_ShouldEndWithService` selecciona todas las clases de Application, no solo servicios. Al conectar la aplicación, los DTO previstos en el diseño quedan alcanzados por esa regla. No renombrar DTO como servicios ni trasladarlos de capa para eludir el control.

La regla [6_flujo_trabajo_ia.md](../../../.agents/rules/6_flujo_trabajo_ia.md) exige: “solicita su autorización explícita antes de editar cualquier archivo de pruebas de calidad”. Las tareas Q03 a Q06 que editen `CodeQuality.Tests` dependen de esa autorización. Este plan no modifica esos archivos ni supone que dicha autorización ya fue otorgada.

- [x] Q01. Revisar las reglas existentes de capas, repositorios, dominio y nomenclatura contra la organización por namespaces de un único ensamblado.
- [x] Q02. Documentar el cambio mínimo necesario para conectar el arnés y el alcance correcto de la regla de nomenclatura de servicios.
- [x] Q03. Obtener autorización explícita para los cambios identificados en `CodeQuality.Tests`, antes de editar el proyecto o sus pruebas.
- [x] Q04. Con la autorización correspondiente, alinear el framework de `CodeQuality.Tests` con .NET 10 para permitir su referencia al proyecto servidor.
- [x] Q05. Con la autorización correspondiente, agregar una referencia desde `CodeQuality.Tests` a `SolarUY`.
- [x] Q06. Con la autorización correspondiente, cargar el ensamblado real de `SolarUY` en `ArchitectureSetup.cs` mediante un tipo existente, por ejemplo `SolarUY.Components.App`, en lugar de `System.Object`.
- [x] Q07. Resolver el conflicto de la regla `ApplicationServices_ShouldEndWithService` según el alcance expresamente aprobado; preservar el sufijo `Service` para los servicios y `Dto` para los DTO.
- [x] Q08. Incorporar `CodeQuality.Tests` a `SolarUY.slnx`.
- [x] Q09. Ejecutar el arnés conectado y registrar el resultado inicial; distinguir fallos preexistentes de los introducidos por el alta.
- [x] Q10. Crear un proyecto xUnit auxiliar `tests/SolarUY.Domain.Tests` con .NET 10 y referencia a `SolarUY`.
- [x] Q11. Incorporar `SolarUY.Domain.Tests` a `SolarUY.slnx`.
- [x] Q12. Verificar que el proyecto de dominio descubre y ejecuta sus pruebas cuando se agregue el primer caso de comportamiento.

Resultado: pruebas de dominio preparadas y ArchUnitNET conectado al ensamblado real. Ningún control arquitectónico se considera validado por una ejecución que siga analizando solamente `System.Object`.

## 2. Domain

Dependencias: P01, P02 y la estructura modular. Las pruebas unitarias acompañan cada comportamiento: escribir el caso, comprobar que falla por el comportamiento ausente y luego implementar lo necesario para que pase. Ejecutar ArchUnitNET después de cada incremento de código, conforme a las reglas del workspace.

### Nombre del parque

- [x] D01. Escribir la prueba parametrizada que rechaza nombres nulos, vacíos o compuestos solamente por espacios.
- [x] D02. Implementar la construcción de `NombreParqueSolar` con esa validación, sin dependencias de frameworks externos.
- [x] D03. Escribir la prueba que conserva el nombre visible sin espacios exteriores.
- [x] D04. Implementar el valor visible limpio de `NombreParqueSolar`.
- [x] D05. Escribir la prueba de equivalencia de claves para nombres con distinta capitalización y espacios exteriores.
- [x] D06. Implementar la generación de la clave de comparación en `NombreParqueSolar`.
- [x] D07. Escribir la prueba de equivalencia entre representaciones Unicode canónicamente iguales.
- [x] D08. Incorporar la normalización Form C a la generación de la clave.
- [x] D09. Escribir y ejecutar los casos que demuestran que acentos y espacios interiores significativos no se eliminan de la clave.

### Creación del parque

- [x] D10. Escribir la prueba de creación válida de `ParqueSolar`, con ID no vacío y conservación de nombre y ubicación.
- [x] D11. Implementar `ParqueSolar.Crear(nombre, ubicacion)` con generación de `Guid` y estado protegido mediante propiedades de solo lectura o setters privados.
- [x] D12. Escribir y ejecutar la prueba de generación de IDs distintos para dos creaciones.
- [x] D13. Escribir la prueba parametrizada que rechaza ubicación nula, vacía o compuesta solamente por espacios.
- [x] D14. Implementar la validación de ubicación dentro de la creación del parque.
- [x] D15. Escribir la prueba que elimina los espacios exteriores de la ubicación.
- [x] D16. Implementar la limpieza de ubicación dentro del dominio.
- [x] D17. Escribir la prueba de capacidad inicial `0m` para el parque vacío.
- [x] D18. Implementar `CapacidadTotalEstimadaMw` como propiedad derivada de solo lectura para el estado vacío, sin almacenamiento editable de capacidad.
- [x] D19. Incorporar `DatosParqueSolarInvalidosException`, si se adopta manejo mediante excepciones, con identificación del campo inválido para su traducción en Application.
- [x] D20. Verificar que la creación no genera ni asigna nodos, inversores o paneles y que no incorpora modelos provisionales de topología.

### Control de la capa

- [x] D21. Ejecutar todas las pruebas de `SolarUY.Domain.Tests` y corregir el comportamiento de Domain ante cualquier fallo.
- [x] D22. Ejecutar `CodeQuality.Tests` después de los cambios de Domain.
- [x] D23. Comprobar que los filtros de ArchUnitNET reconocen los tipos reales de `SolarUY.Domain` dentro del ensamblado servidor.
- [x] D24. Revisar que Domain no usa EF Core, ASP.NET, Blazor ni tipos de otras capas.

Resultado: parque válido, vacío e identificado; invariantes locales cubiertas por xUnit. El cálculo con nodos y paneles queda para el requerimiento de topología.

## 3. Application

Dependencias: Domain y resolución del alcance de la regla de nomenclatura. No generar pruebas unitarias de esta capa, según las reglas del workspace.

### Contratos y datos de entrada/salida

- [x] A01. Definir `CrearParqueSolarDto` exclusivamente con `Nombre` y `Ubicacion`.
- [x] A02. Definir `ParqueSolarDto` con ID, nombre visible, ubicación y capacidad en MW.
- [x] A03. Definir `ResultadoAltaParqueSolarDto` con éxito y errores tipados, incluyendo asociación de validaciones a campos.
- [x] A04. Definir `IUsuarioActualService` para consultar autenticación y pertenencia al rol sin exponer `HttpContext` ni `ClaimsPrincipal`.
- [x] A05. Definir la consulta asíncrona de existencia por clave de nombre en `IParqueSolarRepository`.
- [x] A06. Definir el guardado asíncrono del parque y su resultado específico de éxito o conflicto de nombre en `IParqueSolarRepository`.
- [x] A07. Definir `IParqueSolarService.CrearAsync` y su contrato de resultado.

### Caso de uso

- [x] A08. Crear `ParqueSolarService` con sus dependencias recibidas exclusivamente por constructor.
- [x] A09. Implementar la comprobación de autenticación antes de cualquier consulta o escritura del parque.
- [x] A10. Implementar la comprobación del rol exacto `Administrador` antes de cualquier consulta o escritura del parque.
- [x] A11. Invocar la creación del dominio desde el servicio, sin duplicar sus reglas de normalización o validación.
- [x] A12. Traducir las validaciones conocidas del dominio a errores asociados a nombre o ubicación.
- [x] A13. Implementar el rechazo temprano de una clave de nombre ya existente.
- [x] A14. Invocar el guardado del parque válido mediante `IParqueSolarRepository`.
- [x] A15. Traducir el conflicto definitivo de guardado al mismo resultado funcional de nombre duplicado.
- [x] A16. Construir el DTO de éxito únicamente después de confirmar la persistencia.
- [x] A17. Mantener los errores inesperados fuera del resultado de duplicado para su manejo centralizado en el servidor.

### Control de la capa

- [x] A18. Ejecutar `CodeQuality.Tests` después de los cambios de Application y corregir las dependencias indebidas en el código fuente.
- [x] A19. Revisar que Application solo utiliza Domain y bibliotecas base de .NET; no expone SQL, tipos de EF ni componentes de UI.
- [x] A20. Revisar que `CrearAsync` solo registra un parque y no crea usuarios, roles ni topología.

Resultado: caso de uso completo y contratos consumibles por Presentation e implementables por Infrastructure.

## 4. Infrastructure

Dependencias: contratos de Application, proveedor elegido, reglas de índice y mecanismo de identidad definido. No generar pruebas unitarias de esta capa.

### Persistencia

- [x] I01. Agregar al proyecto servidor los paquetes de EF Core y del proveedor elegido compatibles con .NET 10.
- [x] I02. Definir la configuración de conexión del entorno sin incorporar credenciales al código versionado.
- [x] I03. Crear `SolarUyDbContext` dentro de `SolarUY.Infrastructure.Persistencia`.
- [x] I04. Configurar el ID de `ParqueSolar` como clave primaria sin reemplazar el ID generado por el dominio.
- [x] I05. Mapear el objeto de valor del nombre a sus datos persistentes, sin agregar anotaciones de EF en Domain.
- [x] I06. Configurar nombre visible, clave normalizada y ubicación como datos obligatorios.
- [x] I07. Configurar los límites de texto acordados, si corresponden al proveedor seleccionado.
- [x] I08. Crear la restricción única sobre la clave normalizada con la comparación definida en P04.
- [x] I09. Excluir `CapacidadTotalEstimadaMw` del mapeo de persistencia.
- [x] I10. Generar la migración inicial del almacenamiento de parques dentro de Infrastructure.
- [x] I11. Revisar la migración para comprobar clave primaria, campos obligatorios e índice único de nombre.
- [x] I12. Aplicar la migración en la base de desarrollo destinada a esta implementación.
- [x] I13. Crear `ParqueSolarRepository` con el contexto recibido por constructor.
- [x] I14. Implementar la consulta asíncrona de existencia por clave de nombre.
- [x] I15. Implementar la inserción y confirmación del parque mediante una única operación de guardado.
- [x] I16. Identificar la violación de la restricción única de nombre mediante los datos específicos del proveedor.
- [x] I17. Traducir exclusivamente esa violación al resultado de conflicto previsto por Application.
- [x] I18. Propagar los errores de persistencia ajenos a la restricción de nombre para el manejo centralizado.

### Usuario actual

- [x] I19. Crear `UsuarioActualService` dentro de `SolarUY.Infrastructure.Seguridad`, con acceso al contexto confiable del servidor recibido por constructor.
- [x] I20. Implementar la consulta de autenticación desde la identidad de la solicitud actual.
- [x] I21. Implementar la consulta del rol `Administrador` desde la identidad validada por el mecanismo elegido.
- [x] I22. Verificar que el adaptador no obtiene identidad ni roles del formulario o de valores declarados por el navegador.

### Control de la capa

- [x] I23. Ejecutar `CodeQuality.Tests` después de agregar paquetes y después de los incrementos de Infrastructure.
- [x] I24. Revisar que todo acceso a datos del caso de uso queda dentro de `ParqueSolarRepository` y que los detalles de EF permanecen en Infrastructure.
- [x] I25. Verificar que las duraciones previstas para contexto, repositorio y usuario actual corresponden a la solicitud u operación y que no se registran como singleton.

Resultado: persistencia con unicidad definitiva y adaptador de identidad confiable, sin dependencias de infraestructura en las capas internas.

## 5. Composición del servidor

Dependencias: Application, Infrastructure y decisiones de autenticación. El punto de composición permanece en `SolarUY/Program.cs`.

- [x] C01. Registrar `IParqueSolarService` con su implementación de Application.
- [x] C02. Registrar `SolarUyDbContext` con el proveedor y la conexión configurados.
- [x] C03. Registrar `IParqueSolarRepository` con `ParqueSolarRepository` y duración apropiada.
- [x] C04. Registrar el acceso al contexto de solicitud requerido por el adaptador de usuario actual.
- [x] C05. Registrar `IUsuarioActualService` con `UsuarioActualService` y duración apropiada.
- [x] C06. Configurar los servicios del mecanismo de autenticación elegido.
- [x] C07. Configurar la autorización para el rol `Administrador`.
- [x] C08. Integrar autenticación y autorización en el flujo HTTP antes del procesamiento de las rutas protegidas.
- [x] C09. Configurar las rutas de inicio de sesión y acceso denegado que necesita el alta, reutilizando el mecanismo definido en P06.
- [x] C10. Mantener la protección antifalsificación y comprobar su integración con el formulario SSR.
- [x] C11. Preparar las identidades de desarrollo definidas en P07 mediante el mecanismo elegido, sin altas de usuarios como efecto secundario del caso de uso.
- [x] C12. Verificar que las implementaciones concretas de Infrastructure solo se utilizan desde su módulo y desde el punto de composición.
- [x] C13. Compilar la solución y ejecutar `CodeQuality.Tests` después de los cambios de composición.

Resultado: dependencias resolubles y seguridad operativa en el servidor, sin incorporar una API REST ni ejecutar el negocio en WebAssembly.

## 6. Presentation

Dependencias: caso de uso y servidor configurados. No generar pruebas unitarias de esta capa.

### Página y envío del formulario

- [x] V01. Crear los imports Razor de `SolarUY/Presentation` necesarios para formularios y contratos de Application.
- [x] V02. Crear `AltaParqueSolar.razor` con namespace `SolarUY.Presentation.Pages` y ruta `/parques-solares/nuevo`.
- [x] V03. Mantener esta página en renderizado estático del servidor y comprobar que el router la descubre en el mismo ensamblado.
- [x] V04. Proteger la página y el procesamiento de su POST con autorización de servidor para `Administrador`.
- [x] V05. Ajustar la integración del router y del estado de autenticación para presentar acceso autorizado o denegado según corresponda.
- [x] V06. Mostrar el encabezado “Registrar parque solar”.
- [x] V07. Incorporar el campo “Nombre” con etiqueta y señalización de obligatoriedad.
- [x] V08. Incorporar el campo “Ubicación (localidad / dirección)” con etiqueta y señalización de obligatoriedad.
- [x] V09. Mostrar la información de capacidad inicial de `0 MW` y su cálculo futuro al incorporar topología.
- [x] V10. Configurar el formulario SSR con nombre explícito y vinculación de sus datos en el POST.
- [x] V11. Incorporar la acción “Registrar parque” y enlazar su envío con `IParqueSolarService.CrearAsync`.
- [x] V12. Verificar que el modelo enviado solo admite nombre y ubicación, sin ID, capacidad, identidad o rol.

### Resultados y navegación

- [x] V13. Mostrar los errores devueltos por Application junto a los campos correspondientes.
- [x] V14. Conservar las entradas cuando el resultado sea inválido o el nombre esté duplicado.
- [x] V15. Mostrar “Parque solar registrado” únicamente tras recibir un resultado de guardado exitoso.
- [x] V16. Mostrar ID, nombre, ubicación y capacidad devueltos en el DTO de éxito.
- [x] V17. Incorporar la acción de registrar otro parque con un formulario limpio, sin depender de páginas todavía inexistentes.
- [x] V18. Mostrar el mensaje genérico previsto ante fallos inesperados de registro.
- [x] V19. Registrar el detalle técnico de esos fallos en el servidor sin exponerlo en el mensaje al usuario.
- [x] V20. Incorporar el enlace de navegación al alta visible para administradores, conservando la autorización efectiva en el servidor.

### Control de la capa

- [x] V21. Revisar que la página no consulta repositorios ni usa `DbContext`.
- [x] V22. Revisar que la página no calcula capacidad, normaliza nombres ni decide reglas de negocio.
- [x] V23. Ejecutar `CodeQuality.Tests` después de los cambios de Presentation y corregir el código ante fallos.
- [x] V24. Compilar la solución para validar componentes Razor, namespaces y resolución de servicios.

Resultado: formulario utilizable por administradores, con respuestas funcionales y datos calculados por las capas internas.

## 7. Validación funcional de aceptación

Ejecutar estos escenarios en el entorno de desarrollo con el proveedor y la autenticación reales configurados. Son verificaciones funcionales del flujo completo; no requieren crear pruebas unitarias de Application, Infrastructure o Presentation.

- [x] F01. Registrar un parque válido como administrador y comprobar la confirmación de éxito.
- [x] F02. Comprobar en el almacenamiento que el parque tiene ID no vacío generado por el sistema y datos consistentes con la confirmación.
- [x] F03. Comprobar que dos parques válidos con nombres distintos reciben IDs distintos.
- [x] F04. Comprobar que nombre vacío o compuesto solamente por espacios se rechaza sin persistir.
- [x] F05. Comprobar que ubicación vacía o compuesta solamente por espacios se rechaza sin persistir.
- [x] F06. Comprobar que se guardan los valores sin espacios exteriores conforme a las reglas del dominio.
- [x] F07. Intentar repetir exactamente un nombre existente y comprobar el mensaje de duplicado y la ausencia de una segunda inserción.
- [x] F08. Intentar repetir un nombre con diferencias de mayúsculas y espacios exteriores y comprobar el mismo rechazo.
- [x] F09. Comprobar con el proveedor real que nombres Unicode canónicamente equivalentes producen conflicto.
- [x] F10. Comprobar con el proveedor real que nombres distintos por acentos o espacios interiores conservan la distinción acordada.
- [x] F11. Ejecutar dos altas concurrentes con nombres equivalentes y comprobar que se persiste exactamente un parque y la otra solicitud recibe conflicto.
- [x] F12. Acceder directamente a la ruta sin autenticación y comprobar que el alta queda protegida por el servidor.
- [x] F13. Enviar directamente un POST de alta sin autenticación y comprobar que no se escribe un parque.
- [x] F14. Acceder a la ruta con un usuario autenticado sin rol `Administrador` y comprobar acceso denegado.
- [x] F15. Enviar directamente el POST con ese usuario y comprobar ausencia de escrituras.
- [x] F16. Comprobar que agregar valores de rol, identidad, ID o capacidad al envío no altera la autorización ni los datos generados por el caso de uso.
- [x] F17. Comprobar que un envío sin token antifalsificación válido se rechaza.
- [x] F18. Comprobar que el parque recién creado devuelve capacidad `0 MW` y no tiene nodos, inversores o paneles generados ni asignados por el alta.
- [x] F19. Provocar un fallo controlado de persistencia y comprobar que no aparece una confirmación de éxito ni un falso error de nombre duplicado.
- [x] F20. Verificar que el mensaje al usuario oculta detalles técnicos y que el registro del servidor permite diagnosticar el fallo controlado.
- [x] F21. Verificar que los datos ingresados se conservan tras validaciones y duplicados.
- [x] F22. Verificar que “registrar otro parque” devuelve un formulario limpio y permite un nuevo alta independiente.
- [x] F23. Recorrer el formulario con teclado y comprobar etiquetas, mensajes de campo y legibilidad de la confirmación.

Resultado: todos los criterios de aceptación cubiertos con evidencia del flujo real y de los rechazos sin escrituras.

## 8. Revisión de código y cierre

- [x] R01. Revisar nomenclatura: negocio en español, interfaces con prefijo `I`, servicios con sufijo `Service`, repositorios con `Repository` y excepciones con `Exception`.
- [x] R02. Revisar las dependencias entre los cuatro namespaces y el aislamiento de Infrastructure respecto de los componentes.
- [x] R03. Revisar inyección por constructor en servicios y repositorios, sin instanciación manual de esas dependencias.
- [x] R04. Revisar responsabilidad única de los métodos y ausencia de efectos secundarios ajenos al alta.
- [x] R05. Revisar que la validación de nombres tiene una única definición y coincide con la restricción del proveedor.
- [x] R06. Revisar que la migración, la configuración y los registros no incorporan credenciales versionadas ni datos de identidad confiados al navegador.
- [x] R07. Ejecutar la compilación final de `SolarUY.slnx` y resolver errores y advertencias introducidos por el cambio.
- [x] R08. Ejecutar todas las pruebas unitarias de dominio y registrar su resultado final.
- [x] R09. Ejecutar todas las pruebas de ArchUnitNET sobre el ensamblado real de SolarUY y registrar su resultado final.
- [x] R10. Ante cualquier fallo arquitectónico, corregir el código fuente y repetir el control afectado; no relajar pruebas para hacerlas pasar. Si la regla requiere una excepción, gestionar autorización antes de editarla.
- [ ] R11. Si se incorpora SonarQube al control de esta entrega, verificar la disponibilidad de Docker, del servidor y de credenciales gestionadas fuera del código versionado. **No aplica: SonarQube no se incorporó a esta entrega.**
- [ ] R12. Si se incorpora SonarQube, ejecutar el análisis mediante el arnés de `arnes/SonarQube` y registrar el resultado del Quality Gate. **No aplica: SonarQube no se incorporó a esta entrega.**
- [ ] R13. Si se incorpora SonarQube, resolver los hallazgos atribuibles al cambio y repetir el análisis hasta cumplir el control acordado. **No aplica: SonarQube no se incorporó a esta entrega.**
- [x] R14. Revisar el diff final para confirmar que el cambio se limita al alta, sus decisiones documentadas y los ajustes de calidad autorizados.
- [x] R15. Actualizar los checkboxes completados y registrar las decisiones, verificaciones pendientes y resultados reales de la entrega.

Resultado: implementación revisada, compilación correcta, pruebas de dominio y arquitectura exitosas y aceptación funcional comprobada. SonarQube es un control adicional condicionado a su incorporación en la entrega.

## Comandos de validación

Ejecutar desde la raíz del repositorio durante la implementación, una vez preparados los proyectos correspondientes:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

El control de ArchUnitNET se ejecuta después de cada incremento de código, incluidas nuevas dependencias, y vuelve a ejecutarse en el cierre. No basta con dejarlo para el final. Los controles de dominio se ejecutan durante cada ciclo de comportamiento y en el cierre. Una falla se corrige antes de marcar completada la tarea relacionada.

## Evidencia de avance

Completar este registro durante la implementación. La generación del plan no implica que las tareas o las pruebas ya se hayan ejecutado.

| Control / decisión | Resultado | Evidencia |
| --- | --- | --- |
| Equivalencia de nombres y ubicación | Confirmado | Trim + Form C + mayúsculas invariantes; ubicación en texto único. |
| Proveedor, comparación y límites de almacenamiento | Implementado | EF Core SQLite 10.0.12; BINARY, índice único, sin máximos adicionales. |
| Autenticación y emisión del rol | Verificado | Cookie de servidor; hashes y roles de configuración externa; se rechaza rol administrador en minúsculas. |
| Autorización de cambios del arnés | Recibida | Autorizaciones explícitas de conexión/nomenclatura, ajuste incremental y posterior retiro de ese ajuste. |
| Compilación inicial y final | Correctas | 0 errores; advertencias NU1903 del paquete WebAssembly.Server 10.0.12 preexistente. |
| Pruebas unitarias de Domain | 15/15 correctas | artifacts/req001/dominio-sin-opcion-incremental.trx |
| ArchUnitNET conectado a SolarUY | 17/17 correctas | artifacts/req001/arquitectura-sin-opcion-incremental.trx; cobertura de las cuatro capas y exigencia de resultados positivos restaurada. |
| Validación funcional de aceptación | F01–F23 correctas | HTTP: artifacts/req001/aceptacion-2b3fec8f88/resultado.json; teclado y captura: aceptacion-86c868f069. |
| SonarQube, si se incorpora | No aplica | No incorporado a esta entrega; R11–R13 no ejecutadas. |

### Preparación — 2026-10-14

SDK 10.0.401 disponible. Compilación inicial correcta: 0 errores, 4 advertencias NU1903 preexistentes (dos avisos repetidos del paquete WebAssembly.Server 10.0.12). El primer intento restringido falló sin diagnósticos; la compilación fuera de la restricción fue exitosa. Decisiones concretadas en specs.md. Autorización explícita del usuario recibida para los ajustes de ajustes_arnes_propuestos.md. No se modifican los requerimientos funcionales.

### Arnes conectado — control inicial

10 pruebas descubiertas sobre SolarUY: 2 correctas y 8 fallos preexistentes por selección vacía de tipos todavía no implementados. ResideInNamespaceMatching es la API regex verificada en la biblioteca local 0.13.4. Ajuste incremental adicional documentado y pendiente de autorización; no se relajan las reglas de dependencia.

Ajuste incremental autorizado explícitamente. Control inicial corregido: 11/11 pruebas arquitectónicas correctas, con cobertura de App y NombreParqueSolar. Primer ciclo TDD: fallo por tipo ausente y luego 3/3 casos correctos para nombre obligatorio.

### Domain — capa completada

15/15 casos xUnit correctos; 11/11 controles arquitectónicos correctos; solución compilada con 0 errores. Ciclos TDD observados: fallos por tipos ausentes, limpieza de nombre, equivalencia de capitalización, normalización Unicode, ubicación obligatoria, limpieza de ubicación y capacidad ausente; luego todos correctos. GUID independiente en cada creación y capacidad derivada 0m. No hay tipos ni asignaciones de topología ni dependencias externas en Domain.

### Application — capa completada

Caso de uso compilado y 14/14 controles arquitectónicos correctos. Dependencias únicamente de Domain y bibliotecas base. Identidad y rol se comprueban antes de las operaciones del repositorio. Validaciones traducidas por campo, conflicto previo y definitivo con el mismo mensaje, DTO de éxito posterior al guardado. No se crean usuarios, roles ni topología; no se generan tests unitarios de esta capa.

### Infrastructure — persistencia y adaptador completados

EF Core SQLite 10.0.12 restaurado; migración 20261014223526_AltaParqueSolarInicial generada y aplicada en solaruy-desarrollo.db (ignorado). PK Guid, Nombre/ClaveNombre/Ubicacion NOT NULL, comparación BINARY e índice único revisados; capacidad excluida. Única llamada a SaveChangesAsync; solo el código SQLite 2067 referido a ParquesSolares.ClaveNombre se traduce a conflicto. 16/16 controles arquitectónicos correctos y compilación sin errores. Duraciones scoped se verificarán en composición.

### Composición — completada

Servicios de aplicación, contexto, repositorio e identidad registrados scoped; implementaciones externas solo en Infrastructure y Program. Cookie de servidor, roles de configuración externa, antifalsificación explícita en login/logout y autenticación/autorización antes del formulario. Identidades efímeras administrador, usuario y rol en minúsculas preparadas en artifacts ignorado; procedimiento reutilizable en configuracion.md. 16/16 tests arquitectónicos correctos y solución compilada sin errores antes de Presentation.

### Presentation — implementación compilada

Página SSR, formulario alta-parque-solar, modelo limitado a nombre/ubicación, errores de campo y conservación de entradas. Confirmación y capacidad se muestran desde el DTO posterior al guardado; nuevo alta por GET limpio. Error inesperado genérico y detalle en ILogger. Router AuthorizeRouteView y enlace restringido. 17/17 controles arquitectónicos correctos y compilación sin errores. V03–V05 se confirman con los escenarios HTTP reales.

### Aceptación HTTP — F01 a F22 correctos

Servidor real en 127.0.0.1:5284, cookies y SQLite aislado. Evidencia en artifacts/req001/aceptacion-86c868f069/resultado.json y servidor.log (archivos locales ignorados). Incluye roles exactos, entradas manipuladas, concurrencia, token faltante y trigger SQLite que induce fallo técnico sin alta parcial ni éxito o duplicado falso. Se detectó y corrigió conflicto de endpoints POST entre la página de login SSR y el inicio de sesión; el POST de autenticación usa /cuenta/autenticar. Se repitió el control arquitectónico (17/17 correctos) y toda la aceptación HTTP. F23 pendiente de recorrido visual con teclado.

### Recorrido con teclado — F23 correcto

Verificado en navegador: Tab alcanza nombre, ubicación y botón; Enter envía. Un nombre duplicado conserva entradas y muestra mensaje junto al nombre. El alta válida muestra encabezado de confirmación, ID 4db85340-1838-4638-ae56-597c0cf9e636, nombre, ubicación y 0 MW. Tab/Enter en registrar otro parque devuelve campos vacíos. Captura local: artifacts/req001/aceptacion-86c868f069/confirmacion.png. Revisadas nomenclatura, dependencias, constructores, responsabilidades, normalización y ausencia de credenciales versionadas; cambios de calidad limitados al alcance autorizado.

### Cierre — 2026-10-14

Compilación final de SolarUY.slnx correcta, 0 errores y 6 advertencias NU1903 (los dos avisos preexistentes de WebAssembly.Server se repiten por sus referencias transitivas). Pruebas finales: Domain 15/15 y ArchUnitNET 17/17, sin omisiones. F01–F22 repetidos después de ampliar la comprobación de entradas inválidas conservadas: todos correctos en aceptacion-2b3fec8f88. F23 comprobado visualmente con teclado. Verificación adicional con proveedor real y consulta previa omitida: conflicto único de nombre traducido, contexto recuperable y violación de PK propagada. Evidencia auxiliar local en artifacts/req001/VerificarPersistencia, sin pruebas unitarias de capas externas. Servidores efímeros detenidos y pestaña temporal cerrada.

Diff revisado: cambio limitado al alta, persistencia, autenticación mínima necesaria, documentación y ajustes del arnés autorizados. Se preservan los cambios previos del usuario en requirements.md, .agents y CodeQuality.Tests.csproj de la raíz. No se modifica SolarUY.Client. No hay controles obligatorios pendientes; R11–R13 quedan sin marcar porque su condición no aplica y no se ejecutó SonarQube. Las bases e identidades de aceptación se mantienen en artifacts ignorado, fuera de los archivos versionables. Para ejecutar en otro entorno, configurar conexión e identidades según configuracion.md y aplicar la migración.

### Revalidación sin opción incremental — 2026-10-14

- [x] Retirar las nueve llamadas a WithoutRequiringPositiveResults() de los cuatro archivos de reglas, por autorización explícita del usuario.
- [x] Conservar las comprobaciones de cobertura y las reglas de dependencia y nomenclatura.
- [x] Compilar SolarUY.slnx: 0 errores; permanecen las 6 advertencias NU1903 preexistentes/transitivas.
- [x] Ejecutar nuevamente ArchUnitNET: 17/17 correctas, sin omisiones, con resultados positivos obligatorios.
- [x] Ejecutar nuevamente las pruebas de dominio: 15/15 correctas, sin omisiones.

La verificación compiló primero los tests modificados y después ejecutó esos ensamblados. No se modificó código de negocio. Los resultados actualizados figuran en la tabla de evidencia.
