# Plan de implementación: Cambio de base de datos a MySQL

## Base del plan y seguimiento

Fuentes: `specs.md` y `requirements.md` de esta carpeta, junto con `.agents/rules/1_clean_architecture.md` a `7_flujo_trabajo_ia.md`. El máximo de nombre está confirmado en **100 caracteres Unicode**, contados como valores escalares después de la normalización, tanto para el texto visible como para su clave.

Este archivo contiene trabajo pendiente. Marcar cada casilla únicamente después de completar la tarea y su verificación; conservar la evidencia y los impedimentos en un registro al final del documento. Esta entrega de planificación no implementa código, no configura credenciales ni aplica migraciones.

Orden: preparación → Domain → Application → Infrastructure → Presentation → aceptación y cierre. No avanzar de capa hasta que la solución compile y pasen las pruebas de Domain y ArchUnitNET. Ejecutar también ArchUnitNET después de cada cambio de código, dependencias o estructura, según las reglas del proyecto; corregir el código ante fallos y repetir los controles afectados.

No modificar los guardarraíles de `CodeQuality.Tests` para que el cambio pase. No generar pruebas unitarias de Application, Infrastructure ni Presentation; sus comprobaciones de comportamiento se realizan mediante aceptación funcional contra la aplicación y MySQL real.

No modificar ningún archivo de `arnes/prompting/req001_alta_parque_solar` ni los diagramas de `.agents/images`. No borrar bases locales ni ejecutar operaciones destructivas sobre bases de uso habitual. La transición prevista crea un esquema MySQL nuevo y no importa datos anteriores.

## 0. Preparación y línea base

- [x] P01. Registrar el estado inicial del repositorio y los cambios preexistentes para preservarlos durante la implementación.
- [x] P02. Guardar un inventario de rutas y hashes de todos los archivos de `arnes/prompting/req001_alta_parque_solar` y `.agents/images` en `artifacts/req002`, para comparar al cierre sin alterar esas carpetas.
- [x] P03. Ejecutar los tres controles de calidad indicados al final de este plan y registrar sus resultados iniciales; identificar cualquier fallo previo antes de atribuirlo al cambio.
- [x] P04. Confirmar la instancia MySQL objetivo: versión real, host, puerto, base dedicada, disponibilidad de InnoDB, `utf8mb4` y `utf8mb4_0900_bin`; tomar MySQL 8.4 como objetivo de las especificaciones.
- [x] P05. Identificar las credenciales de ejecución y de migraciones por un canal local o configuración protegida, registrando únicamente los nombres de las opciones y permisos requeridos, nunca sus valores secretos.
- [x] P06. Confirmar que la base de destino está vacía y dedicada a este requerimiento. Si contiene datos o se requiere importar registros anteriores, resolver primero el procedimiento de transición descrito en `specs.md`, sin reinicializarla.
- [x] P07. Definir un esquema MySQL de aceptación independiente del destino de uso habitual, con nombre identificable, cuenta apropiada y puerto HTTP libre para el servidor efímero.
- [x] P08. Comprobar disponibilidad de .NET 10, Python y cliente MySQL para las verificaciones; registrar las herramientas que utiliza el arnés y continuar el trabajo independiente si falta acceso al entorno, dejando pendientes las comprobaciones que lo requieran.

Resultado esperado: línea base registrada y destinos de operación y aceptación diferenciados. La falta de una instancia disponible no impide editar código, pero impide marcar como completadas las tareas de conectividad, migración aplicada y aceptación real.

## 1. Domain

Dependencias: P01–P03. Archivos principales: `SolarUY/Domain/ParquesSolares/NombreParqueSolar.cs` y `tests/SolarUY.Domain.Tests/NombreParqueSolarTests.cs`.

- [x] D01. Agregar primero casos xUnit para nombres normalizados de 99 y 100 caracteres aceptados y de 101 rechazados, comprobando que el nuevo caso de exceso falla con la implementación actual.
- [x] D02. Implementar en `NombreParqueSolar` el máximo de 100 valores escalares Unicode para `Valor` y `Clave`, después de `Trim`, Form C y `ToUpperInvariant()`, y hacer pasar los casos de D01.
- [x] D03. Verificar mediante xUnit que el exceso devuelve `DatosParqueSolarInvalidosException`, campo `Nombre` y mensaje exacto “El nombre del parque solar no puede superar los 100 caracteres.”; ajustar conjuntamente implementación y prueba si hace falta.
- [x] D04. Agregar pruebas de frontera con caracteres suplementarios: 100 valores escalares deben aceptarse aunque ocupen más de 100 unidades UTF-16; 101 deben rechazarse.
- [x] D05. Agregar pruebas que demuestren que el conteo ocurre después de quitar espacios exteriores y normalizar representaciones Unicode canónicamente equivalentes.
- [x] D06. Ejecutar las pruebas existentes de obligatoriedad, acentos, espacios interiores y equivalencia de mayúsculas, y comprobar que el valor aceptado nunca se trunca.
- [x] D07. Revisar que `ParqueSolar` conserve ID autogenerado, ubicación obligatoria y capacidad derivada `0 MW`, sin incorporar referencias de persistencia ni validaciones de longitud para ubicación.
- [x] D08. Ejecutar el control de salida de capa: compilación de la solución, pruebas de Domain y ArchUnitNET; registrar resultados y corregir cualquier regresión antes de continuar.

Resultado esperado: el límite confirmado está protegido por Domain y pruebas xUnit, con la semántica Unicode acordada.

## 2. Application

Dependencia: D08. Revisión de los contratos y del servicio existentes; modificar únicamente si se detecta una desviación respecto de `specs.md`.

- [x] A01. Revisar que `IParqueSolarRepository` conserve `ExisteNombreAsync`, `GuardarAsync`, resultados independientes del motor y propagación de `CancellationToken`.
- [x] A02. Comprobar que `ParqueSolarService` rechace anónimos y usuarios sin rol exacto `Administrador` antes de consultar o escribir el parque.
- [x] A03. Verificar que el manejo existente de `DatosParqueSolarInvalidosException` transporte el nuevo error de longitud al campo `Nombre` en `ResultadoAltaParqueSolarDto`, sin duplicar la validación del dominio.
- [x] A04. Revisar que el servicio mantenga la consulta previa, el guardado confirmado y la traducción de `ConflictoNombre` al mensaje funcional existente, sin transformar fallos inesperados en duplicados.
- [x] A05. Confirmar que contratos, DTO y servicios no importen SQL, EF, tipos MySQL ni configuración de conexión, y que no se agreguen casos de uso ajenos al cambio.
- [x] A06. Ejecutar el control de salida de capa: compilación, pruebas de Domain y ArchUnitNET; registrar si la revisión no requirió cambios de Application.

Resultado esperado: el mismo caso de uso opera sobre contratos independientes del proveedor y comunica la validación nueva sin alterar sus firmas.

## 3. Infrastructure

Dependencia: A06. Las tareas de conexión real dependen además de P04–P08. Archivos principales: `SolarUY/SolarUY.csproj`, `SolarUY/Infrastructure/Persistencia`, `SolarUY/Program.cs`, configuración del servidor y `.gitignore`.

### 3.1. Dependencias y configuración local

- [x] I01. Comprobar la disponibilidad y compatibilidad de `MySql.EntityFrameworkCore` 10.0.9 con EF Core 10.0.12 y el servidor objetivo, según la elección de `specs.md`; registrar las versiones exactas resueltas sin cambiar de proveedor silenciosamente.
- [x] I02. Agregar el proveedor MySQL al proyecto servidor, manteniendo `Microsoft.EntityFrameworkCore.Design` y `dotnet-ef` 10.0.12. No agregar paquetes de persistencia al cliente ni incorporar Pomelo.
- [x] I03. Agregar a `.gitignore` la exclusión específica de `SolarUY/appsettings.Local.json` antes de crear un archivo con conexión real; comprobar que la plantilla de ejemplo sí pueda versionarse.
- [x] I04. Crear `SolarUY/appsettings.Local.example.json` con `ConnectionStrings:SolarUy` y marcadores para host, puerto, base, usuario, contraseña y TLS, conforme al ejemplo de las especificaciones.
- [x] I05. Incorporar en `Infrastructure/Persistencia/PersistenciaConfiguration.cs` la carga compartida de configuración con precedencia explícita: base JSON, JSON por entorno, JSON local, variables de entorno y argumentos.
- [x] I06. Definir la resolución del directorio del proyecto servidor para la fábrica de diseño, de forma que los comandos EF ejecutados desde la raíz localicen el mismo archivo local que la aplicación.
- [x] I07. Centralizar la lectura y validación sintáctica de `ConnectionStrings:SolarUy`, rechazando valores ausentes, vacíos o inválidos con diagnóstico sin secretos y sin conexión alternativa automática.
- [x] I08. Centralizar la configuración del contexto con `UseMySQL` mediante las opciones compartidas; no incluir credenciales en `SolarUyDbContext` ni habilitar registros sensibles.
- [x] I09. Adaptar `SolarUyDbContextFactory` para utilizar la configuración compartida y los argumentos recibidos, dejando de depender exclusivamente de la variable de entorno.
- [x] I10. Adaptar `Program.cs` para cargar el archivo local con la precedencia acordada y registrar el contexto MySQL, conservando las duraciones `scoped`, la autenticación y los registros de servicios existentes.
- [x] I11. Comprobar que el archivo local sea opcional cuando la conexión se suministra por variables de entorno u otro origen permitido, y que una opción de menor prioridad no sobrescriba las variables ni los argumentos.
- [x] I12. Crear la copia local ignorada a partir de la plantilla cuando se disponga de los datos del entorno, configurar sus valores sin imprimirlos y verificar su exclusión del control de versiones.

### 3.2. Mapeo y repositorio

- [x] I13. Mantener `SolarUyDbContext` y `ParqueSolarRepository` como implementaciones existentes de Infrastructure, con inyección por constructor, operaciones asíncronas y cancelación.
- [x] I14. Adaptar `ParqueSolarConfiguration` para tabla `ParquesSolares` con InnoDB y `utf8mb4`; configurar el GUID como `char(36)` mediante conversión reversible y `ValueGeneratedNever()`.
- [x] I15. Mapear `Nombre` y `ClaveNombre` como columnas obligatorias `varchar(100)`, conservando `OwnsOne` y el índice único completo `UX_ParquesSolares_ClaveNombre`.
- [x] I16. Sustituir la colación anterior por `utf8mb4_0900_bin` en `ClaveNombre`; mantener ubicación obligatoria como `longtext` y capacidad derivada fuera del mapeo.
- [x] I17. Mantener `ExisteNombreAsync` como consulta parametrizada sobre la clave del dominio, sin una normalización adicional que cambie sus equivalencias.
- [x] I18. Sustituir el reconocimiento de `SqliteException` por el de `MySql.Data.MySqlClient.MySqlException` con código 1062 e identificación exacta del índice de nombre.
- [x] I19. Encapsular la identificación del índice, admitiendo la forma calificada por tabla del servidor objetivo; ante una clave distinta o un formato desconocido, propagar el fallo técnico.
- [x] I20. Conservar un único guardado confirmado para el alta y la desvinculación del parque y su objeto de valor después del conflicto esperado; no introducir reintentos de inserción de resultado incierto.
- [x] I21. Retirar `Microsoft.EntityFrameworkCore.Sqlite`, los imports de `Microsoft.Data.Sqlite` y los demás usos exclusivos de SQLite del código ejecutable del servidor.
- [x] I22. Restaurar dependencias e inspeccionar el grafo directo y transitivo de la solución para comprobar la eliminación del proveedor SQLite y la coherencia de versiones de EF y `MySql.Data`.

### 3.3. Migraciones MySQL

- [x] I23. Sustituir exclusivamente la migración SQLite, su diseñador y snapshot de `SolarUY/Infrastructure/Persistencia/Migrations`, preservando los archivos históricos de la carpeta protegida y las bases locales existentes.
- [x] I24. Restaurar las herramientas y generar una migración inicial `InicialMySql` en la carpeta de migraciones existente, con su diseñador y snapshot generados por el proveedor elegido.
- [x] I25. Generar y revisar el SQL de migración: `char(36)`, `varchar(100)`, `longtext`, obligatoriedad, tabla compartida del objeto de valor, InnoDB, `utf8mb4`, colación y unicidad completa.
- [x] I26. Comprobar que el esquema no incluya capacidad editable ni tablas de nodos, inversores o paneles, además de conservar únicamente el historial de migraciones EF necesario.
- [x] I27. Ejecutar compilación, pruebas de Domain y ArchUnitNET después de completar el reemplazo del proveedor y la nueva línea de migraciones; corregir errores antes de aplicar cambios al destino.
- [x] I28. Aplicar la migración explícitamente a la base MySQL vacía identificada, utilizando la fábrica y configuración compartidas; no usar `EnsureCreated` ni migraciones automáticas al arrancar.
- [x] I29. Inspeccionar el esquema efectivo mediante MySQL y guardar evidencia sin secretos de los tipos, motor, colación, índice completo e historial de migraciones.
- [x] I30. Ejecutar nuevamente la actualización del esquema y comprobar que no repita la creación ni borre registros; no ejecutar `database update 0` como mecanismo de cambio.

### 3.4. Comprobaciones de configuración y persistencia

Estas tareas son verificaciones funcionales sobre MySQL real y sus procesos consumidores. Se pueden automatizar dentro del arnés de aceptación de este requerimiento sin crear pruebas unitarias en Infrastructure ni endpoints de diagnóstico en producción.

- [x] I31. Comprobar que aplicación y fábrica conecten al mismo destino cuando la única conexión configurada se encuentre en `appsettings.Local.json`, ejecutando las herramientas desde la raíz.
- [x] I32. Sobrescribir la conexión local con `ConnectionStrings__SolarUy` hacia otro esquema de aceptación y verificar que tanto el arranque como las herramientas EF respeten esa prioridad.
- [x] I33. Verificar la prioridad de los argumentos de línea de comandos usando conexiones de aceptación sin exponer contraseñas en evidencias ni historiales; comprobar también la selección coherente del entorno.
- [x] I34. Comprobar el rechazo de conexiones ausentes, vacías y sintácticamente inválidas, diferenciándolo de los errores de conectividad real.
- [x] I35. Guardar un parque a través del repositorio real y consultar su clave mediante `ExisteNombreAsync` en un contexto nuevo; comprobar la conversión de GUID al leerlo.
- [x] I36. Provocar un conflicto real del índice de nombre en dos escrituras equivalentes, comprobando el resultado `ConflictoNombre` del repositorio y su estado de seguimiento después del rechazo.
- [x] I37. Provocar en el entorno aislado una clave duplicada distinta al índice de nombre y un fallo de permisos de escritura; comprobar que ninguno se convierta en `ConflictoNombre`.
- [x] I38. Ejecutar el control de salida de capa: compilación, pruebas de Domain y ArchUnitNET; registrar separadamente los resultados de MySQL y dejar pendientes los que no se hayan podido ejecutar.

Resultado esperado: la aplicación y las herramientas usan la misma configuración MySQL, el esquema se aplica de forma explícita y el repositorio conserva los contratos funcionales y de concurrencia.

## 4. Presentation

Dependencia: I38. Archivo principal: `SolarUY/Presentation/Pages/AltaParqueSolar.razor`; mantener el flujo SSR y el proyecto cliente existentes.

- [x] V01. Revisar que `/parques-solares/nuevo` siga consumiendo exclusivamente `IParqueSolarService`, sin referencias al contexto, repositorio concreto, conector o configuración de conexión.
- [x] V02. Comprobar que el formulario muestre junto a `Nombre` el nuevo error de 100 caracteres recibido desde Application, conserve el texto ingresado y no lo trunque.
- [x] V03. Incorporar una ayuda visual breve sobre el máximo de 100 caracteres si resulta necesaria, sin agregar conteo ni normalización propios ni un `maxlength` que rechace caracteres suplementarios válidos por medir unidades UTF-16.
- [x] V04. Mantener la confirmación con ID, nombre, ubicación y `0 MW` únicamente después del guardado confirmado, y conservar el mensaje funcional de nombre duplicado.
- [x] V05. Mantener el mensaje genérico ante errores inesperados y revisar que el registro del servidor no exponga cadenas de conexión ni credenciales.
- [x] V06. Revisar la continuidad de autorización para `Administrador`, cookie de sesión y antifalsificación, sin introducir cambios de identidad o renderizado ajenos al requerimiento.
- [x] V07. Confirmar que los recursos públicos y `SolarUY.Client` no incorporen paquetes de persistencia, archivo local ni secretos del servidor.
- [x] V08. Ejecutar el control de salida de capa: compilación, pruebas de Domain y ArchUnitNET; registrar los resultados.

Resultado esperado: el alta conserva su experiencia y seguridad, muestra el nuevo límite y sigue siendo independiente del motor de datos.

## 5. Documentación y limpieza

Dependencias: configuración y comportamiento definidos en I01–I38 y V01–V08.

- [x] C01. Crear `arnes/prompting/req002_cambio_base_datos/configuracion.md` con las versiones efectivamente verificadas y los requisitos del servidor, motor, juego de caracteres y colación.
- [x] C02. Documentar la preparación de la base dedicada y usuarios, diferenciando permisos para ejecución, migraciones y aceptación sin publicar credenciales reales.
- [x] C03. Documentar la copia de `appsettings.Local.example.json` a `appsettings.Local.json` y dónde editar host, puerto, base, usuario, contraseña y TLS; explicar los marcadores y la exclusión de Git.
- [x] C04. Documentar la precedencia de configuración, la sobrescritura mediante `ConnectionStrings__SolarUy` y el comportamiento común de aplicación y fábrica.
- [x] C05. Incluir comandos completos desde la raíz para restaurar herramientas, aplicar migraciones y arrancar el servidor, conservando las indicaciones necesarias para las identidades provisionadas existentes.
- [x] C06. Documentar cómo comprobar un alta y su consulta, y cómo diagnosticar servidor inaccesible, credenciales, permisos, base inexistente y esquema pendiente sin exponer secretos.
- [x] C07. Explicar las opciones de TLS según el entorno y la validación de certificados, sin proponer su desactivación general como solución de conexión.
- [x] C08. Documentar los requisitos, comando y evidencias del arnés nuevo de `req002`, una vez implementado, y aclarar que el script histórico de `req001` no valida MySQL.
- [x] C09. Indicar que el cambio prepara un esquema nuevo y no importa datos anteriores; explicar que cualquier importación futura debe conservar IDs y validar el máximo de nombre sin truncamientos.
- [x] C10. Actualizar el `README.md` raíz para que la configuración operativa utilice MySQL y enlace a la nueva guía, sin modificar los documentos históricos protegidos.
- [x] C11. Revisar código, configuración, imports, migraciones vigentes y documentación operativa con una búsqueda de SQLite, `UseSqlite`, `SqliteException`, `Data Source` y la colación anterior; clasificar solo las referencias históricas o explicativas previstas como excepciones.
- [x] C12. Revisar `.gitignore` sin retirar protecciones que expongan bases locales o respaldos, y verificar que `artifacts/req002` y la conexión local queden ignorados mientras la plantilla se mantenga versionable.
- [x] C13. Seguir la nueva guía desde un entorno de aceptación para confirmar que permite configurar, migrar y registrar un parque sin editar código; corregir instrucciones que no coincidan con el comportamiento real.

## 6. Arnés funcional y aceptación

Dependencias: controles de salida de las cuatro capas y acceso al esquema aislado de P07. Archivo previsto: `arnes/prompting/req002_cambio_base_datos/verificar_aceptacion.py`.

- [x] F01. Crear un arnés independiente para MySQL, sin modificar ni importar `verificar_aceptacion.py` de `req001`; configurar externamente la conexión de aceptación y comprobar que el destino sea el esquema efímero previsto antes de escribir o limpiar datos.
- [x] F02. Preparar identidades efímeras con rol exacto `Administrador`, rol `Usuario` y un rol de distinta capitalización para la regresión de permisos; no reutilizar cuentas ni secretos de uso habitual.
- [x] F03. Iniciar el servidor efímero de forma oculta, con puerto configurable y comprobación de disponibilidad; registrar solo su proceso y detenerlo al terminar incluso ante fallos.
- [x] F04. Registrar versiones, resultado de cada caso y diagnósticos depurados en `artifacts/req002`; informar claramente requisitos faltantes y devolver un resultado no exitoso si no se ejecutó la aceptación contra MySQL.
- [x] F05. Incluir una verificación funcional del repositorio real para cubrir I35–I37, usando si hace falta un ejecutable auxiliar temporal en `artifacts/req002` que referencie el servidor, sin agregar un proyecto de pruebas unitarias ni endpoints a la aplicación.
- [x] F06. Registrar un parque por HTTP autenticado y comprobar en MySQL el mismo ID, nombre, clave y ubicación; verificar capacidad mostrada `0 MW` y ausencia de topología.
- [x] F07. Reiniciar el proceso servidor conservando el esquema y comprobar la persistencia mediante consulta real y rechazo de un segundo alta con clave equivalente.
- [x] F08. Verificar nombres equivalentes por mayúsculas, espacios exteriores y Form C, y verificar que diferencias de acentos y espacios interiores permitan registros distintos.
- [x] F09. Verificar un nombre de 100 caracteres aceptado y uno de 101 rechazado sin escritura ni truncamiento; repetir con caracteres suplementarios y normalización para comprobar coherencia entre Domain, formulario y MySQL.
- [x] F10. Ejecutar dos altas HTTP equivalentes simultáneas y comprobar exactamente una escritura y un conflicto funcional. Complementar con escrituras sincronizadas del repositorio para alcanzar el conflicto definitivo del índice, aunque la consulta previa evite la carrera en alguna ejecución HTTP.
- [x] F11. Verificar nombres y ubicaciones obligatorios y solicitudes manipuladas: el servidor genera el ID y no acepta una capacidad o topología impuestas por el formulario.
- [x] F12. Verificar acceso GET y POST por anónimos, usuarios sin el rol y rol con capitalización incorrecta; comprobar ausencia de escrituras. Verificar también rechazo del POST sin token antifalsificación.
- [x] F13. Provocar indisponibilidad de conexión, permisos insuficientes y fallo de escritura únicamente mediante el destino de aceptación; comprobar mensaje genérico, ausencia de éxito o duplicado falso y ausencia de registro parcial.
- [x] F14. Inspeccionar la respuesta HTTP, recursos públicos y evidencias para comprobar que no expongan contraseñas, cadenas de conexión, SQL ni excepciones técnicas.
- [x] F15. Recorrer el formulario con teclado y revisar los mensajes de nombre inválido, duplicado, error técnico y éxito, conservando los datos cuando corresponda.
- [x] F16. Ejecutar el arnés completo con MySQL real y guardar los resultados; limpiar únicamente los recursos efímeros identificados o documentar los que se conserven como evidencia, sin tocar bases existentes.

## 7. Validación final y cierre

- [x] R01. Ejecutar el control final de compilación, Domain y ArchUnitNET después del último cambio de código; corregir los fallos y repetir únicamente los controles y casos afectados por nuevas modificaciones.
- [x] R02. Revisar el grafo final de paquetes para comprobar que no queden dependencias activas de SQLite y que el cliente continúe sin acceso a persistencia.
- [x] R03. Comparar rutas y hashes con P02 y confirmar que `req001` y los diagramas permanezcan intactos, incluyendo archivos agregados o eliminados.
- [x] R04. Revisar los cambios finales para comprobar que no se incluyeron secretos, archivos locales, evidencias ignoradas ni modificaciones ajenas al requerimiento.
- [x] R05. Completar la matriz de aceptación siguiente con referencias a evidencias y registrar cualquier verificación pendiente, sin marcar como logrado lo que solo se revisó estáticamente.
- [x] R06. Actualizar el seguimiento de este plan y entregar el resultado con el enlace a `configuracion.md`, los controles ejecutados y las limitaciones reales del entorno.

### Comandos de control

Ejecutar desde la raíz del repositorio al finalizar cada capa y tras cambios de código según las reglas:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Herramientas y migraciones durante Infrastructure, una vez configurado el destino:

```powershell
dotnet tool restore
dotnet ef migrations add InicialMySql --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj --output-dir Infrastructure/Persistencia/Migrations
dotnet ef migrations script --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
```

No repetir la generación de `InicialMySql` si ya existe; los comandos corresponden a las tareas indicadas y no constituyen autorización para borrar migraciones o datos ajenos a este cambio. Revisar el SQL antes de aplicar el esquema.

Comando previsto para el nuevo arnés, después de completar y documentar sus requisitos:

```powershell
python arnes/prompting/req002_cambio_base_datos/verificar_aceptacion.py
```

### Matriz de aceptación

| Criterio | Tareas principales | Resultado y evidencia en artifacts/req002 |
| --- | --- | --- |
| CA1: archivo de conexión y prioridades | I03–I12, I31–I34, C03–C04 | Correcto: I31–I34 en ejecucion-963cbb2c6db0/resultados.json; dos esquemas reales y restauración del archivo local. |
| CA1: esquema MySQL aplicable y repetible | I23–I30 | Correcto: migracion-solaruy.log, esquema-solaruy.log, migracion.sql; I29–I30 en resultados.json. |
| CA1: consulta y escritura persistentes | I35–I36, F05–F08 | Correcto: I35–I36 y F06–F08 en resultados.json; GUID reversible y reinicio real. |
| CA1: concurrencia y errores correctos | I18–I20, I36–I37, F10, F13–F14 | Correcto: repositorio concurrente, F10, F13; permisos-http.json y teclado.json. |
| CA2: organización y fronteras de capas | D07–D08, A01–A06, I05–I10, I13, I38, V01, V07–V08 | Correcto: controles por capa; final-build.log, final-domain.log y final-arquitectura.log (21/21 y 17/17). |
| CA3: eliminación de dependencias anteriores | I21–I26, C10–C12, R02 | Correcto: paquetes-finales.log, paquetes-cliente.log, revision-paquetes.log y búsqueda de referencias activas sin resultados. |
| CA3: guía de conexión utilizable | C01–C10, C13 | Correcto: configuracion.md y README; conexión, migración y alta verificadas con configuración externa en aceptación. |
| Nombre máximo de 100 caracteres Unicode | D01–D06, I15, V02–V03, F09 | Correcto: Domain 21/21 y F09 real con caracteres suplementarios y Form C, sin truncamiento. |
| Regresión de alta y seguridad | A02–A04, V02–V06, F06–F15 | Correcto: F06–F14 en resultados.json; permisos-http.json y recorrido de teclado.json. |
| Integridad de carpeta protegida y diagramas | P02, R03 | Correcto: inventario-protegido.json e integridad-protegida.log; rutas y SHA-256 idénticos. |
| Calidad y aceptación completa | D08, A06, I27, I38, V08, F16, R01–R06 | Correcto: resultado final compuesto en ejecucion-963cbb2c6db0/resultados.json, controles final-*.log y estado-final-mysql.log. |

### Registro de ejecución

Preparación: estado inicial e inventario SHA-256 conservados en `artifacts/req002`. Cambios preexistentes sin seguimiento: carpeta req002 y arnes/.agents/skills/sdd-generar-tasks. Compilación inicial correcta; Domain 15/15 y ArchUnitNET 17/17 (`inicial-build.log`, `base-domain.log`, `base-arquitectura.log`). SDK 10.0.400, runtime 10.0.11; Python y cliente MySQL 8.4.3 disponibles. Destino indicado: localhost:3306; pendientes esquema y credenciales. La ejecución restringida de .NET no funciona correctamente; controles ejecutados con permisos ampliados. Advertencias NU1903 de WebAssembly.Server 10.0.12 ya presentes en la línea base.


Domain: D01–D08 completos. Rojo esperado en dos casos de 101 escalares (domain-rojo.log); salida verde: compilación, 21/21 Domain y 17/17 ArchUnitNET (domain-*.log). Application revisada sin cambios; pendiente control propio de salida.



Infrastructure: MySQL real 8.4.3 confirmado, InnoDB, utf8mb4 y utf8mb4_0900_bin NO PAD; solaruy vacía (mysql-preparacion-real.log). Archivo local ignorado configurado con los datos entregados por el usuario, sin registrar valores. Versiones restauradas: MySql.EntityFrameworkCore 10.0.9, MySql.Data 26.7.0, EF Core/Relational/Design y dotnet-ef 10.0.12 (paquetes-mysql-completo.log, herramientas.log). Relational fijado explícitamente para evitar resolver 10.0.9 junto con Core 10.0.12. Sin dependencias SQLite activas. Migración generada por EF; corregidos imports omitidos por el generador y representación de la FK owned para usar la misma conversión del ID. SQL revisado: el generador omite charset/colación y se añaden explícitamente antes del índice. Verificación física y aceptación todavía pendientes.


Control de salida Infrastructure completado: compilación, Domain 21/21 y ArchUnitNET 17/17 (infrastructure-salida-*.log). Migración aplicada a solaruy y esquema inspeccionado (migracion-solaruy.log, esquema-solaruy.log). Aceptación real confirma lectura, GUID, conflicto, seguimiento, PK propagada, concurrencia e idempotencia. Se fuerza OldGuids=true en las opciones compartidas para que el lector entregue char(36) como texto a la conversión explícita; el almacenamiento sigue siendo char(36). I37 pendiente en su caso de permisos: MySQL 1044 al otorgar SELECT; la cuenta efímera se elimina ante este fallo. Las verificaciones de configuración continúan independientemente. Presentation solo incorpora ayuda de longitud y registro de tipo de error sin excepción completa.


I31–I33 verificadas contra HTTP y fábrica EF desde la raíz, con archivo local, entorno y argumentos; segundo esquema de aceptación real sin escrituras y ambos esquemas eliminados al terminar (resultados.json de la ejecución de Infrastructure en artifacts/req002). El archivo local original se restaura byte por byte. Usuario aportó una cuenta existente de lectura para completar I37 sin administrar usuarios; sus valores se guardan solo en la configuración local ignorada.





Aceptación HTTP: F06–F13 verificadas en MySQL real, incluidos reinicio, Form C, diferencias de acentos y espacios interiores, 100/101 escalares con caracteres suplementarios, carrera HTTP, datos manipulados, roles y antifalsificación. El auxiliar de aceptación declara UTF-8 explícitamente para la entrada por consola en Windows. El usuario habilitó SELECT para la cuenta existente de lectura sobre el prefijo efímero: lectura e INSERT denegado confirmados sin modificar la cuenta (viewer-permisos-verificados.json). La inspección de logs detectó una excepción completa en la categoría Query de EF: corregida la configuración; aceptación final en curso antes de marcar F14–F16.


### Cierre verificado

Todas las subtareas completadas. Compilación final correcta, Domain **21/21**, ArchUnitNET **17/17** y snapshot sin cambios pendientes (`final-*.log`). Los tests estructurales no se modificaron. Persisten únicamente las advertencias NU1903 previas de WebAssembly.Server 10.0.12, documentadas en la guía; no hay errores de compilación.

Aceptación real: `artifacts/req002/ejecucion-963cbb2c6db0/resultados.json`. El arnés cerró inicialmente con código 1 por los casos pendientes de permisos y teclado, sin declararlos exitosos. Tras el permiso aportado por el usuario se repitió solo I37/F13 en el mismo esquema retenido (`permisos-http.json`); F15 se completó mediante el navegador, Tab y Enter (`teclado.json`, `formulario-verificado.jpg`). El resultado automático original se conserva en `resultados-automaticos.json`, y el resultado final incorpora las verificaciones complementarias y la limpieza.

F14 verificado tras omitir también los diagnósticos completos de la categoría Query de EF. Los logs históricos fallidos se depuraron y las credenciales efímeras de procesos terminados se retiraron (`depuracion-evidencias.json`). Se conserva únicamente evidencia sin secretos. La conexión principal y la de lectura quedan en el archivo local ignorado, con la plantilla versionable sin credenciales.

Servidor efímero detenido y esquemas creados por el arnés eliminados (`limpieza-final.json`, `estado-final-mysql.log`); las cuentas existentes no se modificaron. La base `solaruy` conserva una migración, el índice y colación requeridos y **cero registros de prueba**. No se eliminaron bases anteriores. Las rutas y hashes protegidos coinciden con P02. Los cambios preexistentes se preservaron.

Guía operativa: [configuracion.md](configuracion.md). Sin verificaciones funcionales pendientes de este requerimiento; la aceptación queda documentada como resultado compuesto, sin ocultar los impedimentos iniciales ni confundirlos con éxito automático.
