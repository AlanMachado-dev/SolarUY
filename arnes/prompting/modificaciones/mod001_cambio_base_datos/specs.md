# mod001 — Especificaciones técnicas: Cambio de base de datos a MySQL

> Identificador vigente: **mod001 (cambio de base de datos)**, antes req002. Las evidencias en artifacts/req002 y los prefijos técnicos de pruebas conservan su nombre original. Ver [estructura y equivalencias](../../README.md).

Fuente funcional: `requirements.md` de esta carpeta. Diseño para revisión; este documento no implica que el cambio esté implementado.

## Objetivo y alcance

Sustituir la persistencia SQLite por MySQL conservando el alta de parques solares, la consulta de nombres existentes, la autorización y los resultados funcionales actuales. La conexión debe poder configurarse para una instancia MySQL externa al proceso de SolarUY.

Se mantienen .NET 10, Entity Framework Core, Blazor y la separación lógica en cuatro capas dentro del proyecto servidor `SolarUY`. Infrastructure implementa contratos de Application; Application depende de Domain; Presentation consume servicios y DTO de Application. La composición de implementaciones se limita al arranque. No se agregan MediatR, nuevos proyectos de capas, otro ORM ni nuevos casos de uso.

Aplican las reglas `1_clean_architecture.md` a `7_flujo_trabajo_ia.md` de `.agents/rules`. Se respetan los diagramas de `.agents/Images`, cuyo diagrama de arquitectura ya contempla MySQL. No se modifican los diagramas.

### Restricciones y supuestos explícitos

- No modificar, eliminar, mover ni regenerar ningún archivo de `arnes/prompting/requerimientos/req001_alta_parque_solar`, incluidos sus documentos y scripts.
- La limpieza elimina SQLite del código ejecutable, dependencias, migraciones vigentes y documentación operativa fuera de la carpeta protegida. Las referencias históricas de `req001` y la descripción del cambio en `mod001` son excepciones documentales necesarias; no se consideran dependencias activas.
- Se supone una base MySQL nueva, creada para SolarUY. El requerimiento no solicita transferir registros de una base anterior: no se presume autorización para eliminar archivos locales ni reinicializar bases con datos.
- Si deben conservarse datos anteriores, la implementación requiere definir previamente su volumen, origen, respaldo y procedimiento de importación. Cambiar el proveedor o aplicar migraciones de esquema no traslada registros entre motores.
- La versión objetivo propuesta es MySQL 8.4 con InnoDB y `utf8mb4`. La versión real del servidor y la compatibilidad del proveedor deben verificarse antes de aplicar el esquema. MariaDB no sustituye la aceptación contra MySQL.
- No se incorporan listados, edición, topología, telemetría ni cambios del mecanismo de autenticación.

## 1. Domain

### Modelo e invariantes que se conservan

`SolarUY.Domain.ParquesSolares` continúa utilizando únicamente C# y bibliotecas base de .NET. No recibe atributos de EF Core, opciones de conexión, SQL, tipos del conector ni propiedades creadas exclusivamente para representar almacenamiento.

Se conservan:

- `ParqueSolar.ID` de tipo `Guid`, generado por el dominio y no vacío.
- El objeto de valor `NombreParqueSolar`, su texto visible y su clave de comparación.
- La normalización actual: quitar espacios exteriores, aplicar Unicode Form C y generar la clave con `ToUpperInvariant()`.
- Acentos y espacios interiores significativos: `Sol` y `Sól`, así como `Parque Solar` y `Parque  Solar`, siguen siendo nombres diferentes.
- Ubicación obligatoria, limpia de espacios exteriores, y capacidad inicial derivada de `0 MW`.
- La unicidad global se coordina mediante Application y se garantiza definitivamente en la base; la entidad no realiza consultas.

### Longitud de nombres: máximo confirmado de 100 caracteres

La implementación actual admite nombres sin máximo funcional explícito. MySQL impone un límite físico al tamaño de las claves de índices InnoDB; trasladar el índice único sobre un texto sin límite no es una traducción directa. Tampoco se admite un índice único de prefijo, porque rechazaría nombres diferentes que compartan el comienzo. Véase la documentación de [límites de InnoDB](https://dev.mysql.com/doc/refman/8.4/en/innodb-limits.html).

Por indicación del usuario, se establece un máximo de **100 caracteres tanto para el nombre normalizado visible como para la clave resultante**, verificados después de la normalización y conversión actuales. Se cuentan caracteres Unicode como valores escalares, no bytes ni unidades UTF-16; un carácter suplementario cuenta como uno. El mapeo utiliza `varchar(100)` con `utf8mb4` e índice único completo. La migración debe verificar que el índice se crea correctamente en la instancia objetivo.

La validación pertenece a `NombreParqueSolar` y devuelve `DatosParqueSolarInvalidosException` asociada a `Nombre`, con el mensaje “El nombre del parque solar no puede superar los 100 caracteres.”. No se truncan textos ni se delega el rechazo exclusivamente a MySQL. Ubicación no recibe un máximo funcional nuevo.

Esta decisión queda resuelta para la planificación. Si posteriormente se solicita importar registros anteriores, comprobar que sus nombres cumplen el máximo antes de importarlos; nunca acortarlos automáticamente.

### Verificación de Domain

Mantener las pruebas xUnit actuales y agregar simultáneamente pruebas de nombres de 99, 100 y 101 caracteres y de medición posterior a la normalización, incluidos caracteres suplementarios. Verificar también que acentos, espacios interiores y equivalencia Unicode conservan su semántica. Ninguna prueba de dominio depende de MySQL.

## 2. Application

### Contratos y responsabilidades

Se conservan `IParqueSolarRepository`, `IParqueSolarService`, `ParqueSolarService`, `IUsuarioActualService` y los DTO existentes. Las firmas asíncronas y los `CancellationToken` continúan siendo independientes del proveedor.

| Operación | Contrato que debe mantenerse |
| --- | --- |
| `ExisteNombreAsync` | Consultar la existencia por la clave producida por Domain, respetando exactamente su equivalencia. |
| `GuardarAsync` | Devolver `Guardado` solo después de confirmar la escritura; devolver `ConflictoNombre` únicamente ante la restricción de nombre. |
| `CrearAsync` | Exigir el rol exacto `Administrador`, validar mediante Domain, comprobar el nombre, guardar y devolver los datos persistidos. |

Application no conoce `MySqlException`, códigos de error del servidor, tipos de columna ni cadenas de conexión. Los conflictos técnicos se traducen dentro del repositorio; los fallos inesperados se propagan al manejo existente de Presentation.

### Flujo que debe preservarse

1. Rechazar usuarios anónimos o sin el rol requerido antes de acceder a los datos del parque.
2. Crear el objeto de dominio y convertir sus errores conocidos en errores de campo.
3. Consultar si existe la clave normalizada.
4. Insertar y confirmar el parque mediante el repositorio.
5. Traducir un conflicto de nombre concurrente al mismo resultado funcional que el duplicado detectado previamente.
6. Mostrar éxito únicamente después de la confirmación, con el ID generado y capacidad `0 MW`.

No se agrega un servicio de aplicación para administrar la conexión ni un caso de uso de lectura que no existe en el requerimiento. La consulta se valida mediante `ExisteNombreAsync` y la comprobación de registros persistidos en el arnés funcional nuevo.

Conforme a las reglas del proyecto, no se generan pruebas unitarias de Application.

## 3. Infrastructure

### 3.1. Proveedor y dependencias

Se propone utilizar el proveedor oficial `MySql.EntityFrameworkCore` **10.0.9**, cuya publicación declara dependencias para `net10.0` de EF Core y EF Core Relational `>= 10.0.9`. El proyecto ya usa EF Core Design y herramientas EF **10.0.12**; se mantienen esas versiones de Microsoft y se verifica la combinación mediante restauración, compilación, generación de migración y aceptación real. La evidencia de compatibilidad procede de la [publicación versionada del proveedor](https://www.nuget.org/packages/MySql.EntityFrameworkCore/10.0.9).

La planificación debe registrar las versiones exactas restauradas. No se combina este proveedor con Pomelo ni se copian extensiones de configuración de otro conector. Se utiliza `UseMySQL`, correspondiente al proveedor elegido, mediante opciones configuradas externamente; el contexto no contiene credenciales. La [guía oficial de configuración](https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework-core.html) describe esta extensión.

En `SolarUY/SolarUY.csproj` se retira `Microsoft.EntityFrameworkCore.Sqlite` y se agrega el proveedor MySQL. Se eliminan imports, referencias y dependencias directas exclusivas del motor anterior. `MySql.Data` debe quedar en una versión compatible con el proveedor, ya sea resuelta transitivamente o fijada si el repositorio la declara directamente. Después de restaurar se inspecciona también el grafo transitivo.

### 3.2. Organización y composición

Se conserva el contexto y el repositorio existentes, cuyos nombres son independientes del motor. No hace falta duplicarlos ni crear un repositorio genérico.

```text
SolarUY/
├── Domain/ParquesSolares/                    # Modelo y validaciones
├── Application/ParquesSolares/               # Contratos, DTO y servicio existente
├── Infrastructure/Persistencia/
│   ├── PersistenciaConfiguration.cs          # Configuración común del proveedor
│   ├── SolarUyDbContext.cs                   # Contexto existente
│   ├── SolarUyDbContextFactory.cs            # Herramientas EF
│   ├── ParqueSolarConfiguration.cs           # Mapeo MySQL
│   ├── ParqueSolarRepository.cs              # Consultas y escrituras
│   └── Migrations/                           # Nueva línea de migraciones MySQL
├── Program.cs                               # Composición de servicios
├── appsettings.Local.example.json           # Plantilla versionada sin secretos
└── appsettings.Local.json                   # Conexión local, excluida de Git
arnes/prompting/modificaciones/mod001_cambio_base_datos/
├── requirements.md
├── specs.md
├── tasks.md
├── configuracion.md                         # Guía de conexión MySQL
└── referencia_tecnica.md                    # Detalles y aceptación
arnes/prompting/tests/mod001/
├── verificar_aceptacion.py                   # Arnés funcional MySQL
└── verificar_persistencia.cs                 # Ejecutable auxiliar
```

`PersistenciaConfiguration` concentra la selección del proveedor y la configuración técnica compartida. El arranque y `SolarUyDbContextFactory` utilizan esa misma configuración para evitar diferencias entre ejecución y migraciones. Los repositorios y servicios reciben dependencias exclusivamente por constructor. El contexto mantiene duración `scoped` para el flujo SSR actual; no se convierte en singleton.

### 3.3. Archivo de configuración y conexión externa

Durante la implementación se debe agregar `SolarUY/appsettings.Local.example.json` como plantilla sin credenciales reales. El usuario podrá copiarla a **`SolarUY/appsettings.Local.json`** y editar allí host, puerto, base, usuario y contraseña dentro de la cadena de conexión:

```json
{
  "ConnectionStrings": {
    "SolarUy": "Server=<host>;Port=<puerto>;Database=<base>;User ID=<usuario>;Password=<contraseña>;SslMode=Required;"
  }
}
```

Los valores entre ángulos son marcadores que deben reemplazarse. Las opciones de TLS se ajustan al entorno conforme a la guía de conexión. El archivo local es opcional si se proporciona la conexión por otro origen de configuración, y se excluye explícitamente de Git antes de crearlo con datos reales.

El servidor y la fábrica de diseño deben leer el mismo archivo y respetar la precedencia: `appsettings.json`, `appsettings.{Environment}.json`, `appsettings.Local.json`, variables de entorno y argumentos de línea de comandos, de menor a mayor prioridad. La configuración compartida de Infrastructure debe asegurar esa coherencia. La fábrica resuelve la ubicación del archivo respecto del proyecto servidor para que las migraciones ejecutadas desde la raíz usen la misma conexión que la aplicación. La guía explica cómo copiar la plantilla y configurar ambos procesos sin editar código.

- Mantener la clave `ConnectionStrings:SolarUy` y su representación de variable de entorno `ConnectionStrings__SolarUy`.
- Integrar el archivo local con la configuración de ASP.NET Core; la fábrica deja de depender exclusivamente de una variable de entorno. `ConnectionStrings__SolarUy` permite sobrescribir la cadena completa del archivo tanto al ejecutar como al aplicar migraciones.
- Permitir configurar servidor, puerto, base, usuario, contraseña y opciones de transporte del conector. No asumir que MySQL corre en el mismo equipo ni exigir Docker.
- Rechazar al configurar los servicios una conexión ausente, vacía o con sintaxis inválida. No volver automáticamente a otra base ni generar una conexión por defecto.
- Mantener secretos fuera del repositorio y del proyecto `SolarUY.Client`. No registrar la cadena completa, contraseñas ni parámetros sensibles; no habilitar el registro de datos sensibles de EF.
- Documentar los permisos necesarios para ejecutar la aplicación y los permisos adicionales para aplicar migraciones. La cuenta de ejecución no necesita administrar usuarios ni crear bases.
- La conexión física se comprueba explícitamente al preparar y verificar el entorno. No se confunde la validación sintáctica con una conexión exitosa.

### 3.4. Mapeo MySQL y unicidad

| Dato | Especificación propuesta |
| --- | --- |
| Tabla | `ParquesSolares`, motor InnoDB. |
| `ID` | Clave primaria, `Guid` del dominio; representación explícita `char(36)` mediante conversión reversible, sin generación por la base. |
| `Nombre` | Obligatorio, `varchar(100)`, máximo de 100 caracteres validado en Domain. |
| `ClaveNombre` | Obligatoria, `varchar(100)`, máximo de 100 caracteres validado en Domain; comparación `utf8mb4_0900_bin`. |
| `Ubicacion` | Obligatoria, `longtext`, sin truncamiento ni máximo funcional adicional. |
| Índice de nombre | Único, completo y denominado `UX_ParquesSolares_ClaveNombre`. |
| Capacidad | Se conserva ignorada en el mapeo por ser derivada. |
| Objeto de valor | Conservar `OwnsOne` y las columnas del nombre dentro de la tabla del parque. |

La colación de la clave se configura explícitamente, en vez de heredar una comparación que ignore acentos. `utf8mb4_0900_bin` es binaria y `NO PAD`, según la [documentación de colaciones MySQL](https://dev.mysql.com/doc/refman/8.4/en/charset-binary-collations.html). La migración y las verificaciones deben comprobar la colación efectiva de la columna, el índice y los casos Unicode; no basta con que compile el mapeo.

La consulta previa y el índice utilizan la misma clave y comparación. Los comandos son parametrizados mediante EF. Una escritura exitosa utiliza `SaveChangesAsync`; un fallo no informa éxito ni deja un alta parcial.

### 3.5. Errores y concurrencia

Reemplazar la dependencia de `SqliteException` y el código extendido `2067` en `ParqueSolarRepository` por el manejo específico del conector elegido.

Un `DbUpdateException` se transforma en `ConflictoNombre` solamente si su causa es `MySql.Data.MySqlClient.MySqlException`, corresponde a la entrada duplicada **1062** y permite identificar exactamente el índice `UX_ParquesSolares_ClaveNombre`. El código está documentado en la [referencia de errores del servidor](https://dev.mysql.com/doc/mysql-errors/8.4/en/server-error-reference.html).

El adaptador debe tolerar el nombre de clave calificado con tabla que devuelva la versión objetivo, sin buscar simplemente cualquier palabra “duplicate”. Si el conector no expone el nombre estructurado, encapsular y verificar la extracción del identificador informado por MySQL. Ante un formato no reconocido, propagar el error técnico; nunca asumir que todo código 1062 es un nombre repetido.

Se mantiene la desvinculación de la entidad y del objeto de valor del seguimiento después del conflicto esperado. Una colisión de clave primaria, falta de permisos, indisponibilidad, esquema faltante o error de SQL no se traduce a nombre duplicado. No se reintenta ciegamente una inserción cuyo resultado de confirmación sea incierto.

Dos solicitudes concurrentes con nombres equivalentes deben producir como máximo un parque confirmado. La consulta previa no reemplaza al índice único.

### 3.6. Migraciones y transición

1. Confirmar una base MySQL vacía y dedicada, la versión del servidor, el juego de caracteres y la configuración InnoDB prevista.
2. Sustituir los archivos de la migración inicial anterior, su diseñador y el snapshot dentro de `SolarUY/Infrastructure/Persistencia/Migrations` por una migración inicial MySQL, por ejemplo `InicialMySql`, y su snapshot generado. No reutilizar SQL específico del proveedor anterior.
3. Revisar el SQL generado: tipos, obligatoriedad, conversión de GUID, objeto de valor, colación y restricción única. El esquema solo contiene los datos del caso de uso existente, además del historial de migraciones EF.
4. Aplicar explícitamente con `dotnet ef database update` al destino configurado. Ejecutarlo otra vez no debe repetir la creación ni borrar registros.
5. Ejecutar las verificaciones de lectura, escritura y concurrencia sobre MySQL real.

No utilizar `EnsureCreated`, migraciones automáticas al arrancar, eliminación de bases ni `database update 0` como procedimiento de cambio. La nueva línea de migraciones se aplica únicamente al destino MySQL previsto; no constituye una actualización del historial del motor anterior. El retorno de una migración que elimina tablas no es un mecanismo de conservación de datos.

No borrar bases locales del usuario como parte de la limpieza. Revisar las exclusiones específicas de archivos de base en `.gitignore` sin provocar que datos locales o respaldos queden versionados. Los binarios y resultados previos se regeneran mediante el proceso normal de compilación.

### 3.7. Documentación de conexión

Durante la implementación se debe crear `arnes/prompting/modificaciones/mod001_cambio_base_datos/configuracion.md` y actualizar la sección operativa del `README.md` raíz para que apunte a esta guía y utilice MySQL.

La guía debe incluir:

- Versiones verificadas de .NET, EF, conector y MySQL, con los requisitos InnoDB, `utf8mb4` y colación.
- Creación de una base dedicada y preparación de usuarios y permisos, diferenciando operación de aplicación y migraciones.
- Copia de `SolarUY/appsettings.Local.example.json` a `SolarUY/appsettings.Local.json`, edición de host, puerto, base, usuario, contraseña y TLS, y exclusión del archivo local de Git. Los ejemplos emplean marcadores, nunca datos reales.
- Precedencia de configuración y sobrescritura mediante `ConnectionStrings__SolarUy`, tanto para ejecutar como para aplicar migraciones.
- Comandos desde la raíz para restaurar herramientas, aplicar migraciones y arrancar `SolarUY/SolarUY.csproj`.
- Una comprobación real de consulta y alta, y diagnóstico de conexión rechazada, credenciales, permisos, base inexistente y esquema pendiente.
- Configuración de transporte cifrado apropiada al entorno, sin deshabilitar de forma general la validación de certificados.
- Preparación del arnés MySQL y ubicación de sus evidencias.
- Aviso de que `req001` se conserva como antecedente y su arnés de SQLite no valida la nueva implementación.
- Declaración explícita de que preparar el esquema MySQL no importa automáticamente datos existentes.

No se modifica la guía histórica dentro de `req001` ni se la presenta como procedimiento vigente. Esta entrega genera solamente `specs.md`; los demás archivos descritos corresponden a la posterior implementación.

Conforme a las reglas del proyecto, no se generan pruebas unitarias de Infrastructure.

## 4. Presentation

### Comportamiento y seguridad

Se conserva la página SSR `/parques-solares/nuevo`, sus campos, sus mensajes y su consumo de `IParqueSolarService`. Presentation no conoce el motor, no solicita credenciales de conexión, no consulta repositorios ni agrega lógica SQL.

Se mantienen autorización de servidor con rol exacto `Administrador`, autenticación por cookie y antifalsificación. El proyecto cliente no recibe dependencias de EF ni secretos. El cambio de persistencia no requiere alterar el renderizado ni introducir una API adicional.

Presentation muestra el error de campo recibido del dominio a través de Application cuando el nombre supera 100 caracteres; no aplica un algoritmo propio de normalización. Cualquier ayuda visual debe coincidir con la validación y el conteo Unicode del servidor.

Ante indisponibilidad o fallo inesperado se conserva el mensaje “No se pudo registrar el parque solar. Intentá nuevamente.”. Se registra el diagnóstico técnico en el servidor sin mostrar excepciones, SQL ni credenciales. El nombre duplicado conserva su mensaje funcional y una escritura confirmada conserva la confirmación y los datos del parque.

Conforme a las reglas del proyecto, no se generan pruebas unitarias de Presentation.

### Aceptación funcional y trazabilidad

Crear un arnés funcional independiente en `arnes/prompting/tests/mod001`, sin editar ni importar el script específico de SQLite de `req001`. Debe utilizar un esquema MySQL aislado, identidades efímeras y registros de evidencia en `artifacts/req002`, excluidos de Git. Cualquier limpieza se limita al destino efímero identificado por el arnés; no se ejecuta contra la base de uso habitual. La falta de MySQL o de credenciales se informa como verificación pendiente, nunca como aceptación exitosa.

| Criterio | Verificación y evidencia esperada |
| --- | --- |
| CA1: conexión configurable | Copiar la plantilla al archivo local, configurar servidor, puerto, base y credenciales, y comprobar que aplicación y herramientas EF apuntan al destino indicado; verificar también la sobrescritura mediante variable de entorno. |
| CA1: esquema utilizable | Aplicar la nueva migración en una base vacía y repetir la actualización sin cambios destructivos; inspeccionar tipos, colación e índice. |
| CA1: escritura | Registrar como administrador y comprobar en MySQL el mismo ID, nombre, clave y ubicación; capacidad mostrada de `0 MW`. |
| CA1: lectura persistente | Consultar por `ExisteNombreAsync` usando un contexto nuevo, también después de reiniciar el servidor, y comprobar el rechazo de un segundo alta equivalente. |
| CA1: equivalencia | Mayúsculas, espacios exteriores y representaciones Unicode equivalentes producen duplicado; acentos y espacios interiores distintos permiten registros diferentes. |
| CA1: concurrencia | Dos altas equivalentes simultáneas producen exactamente una escritura y un conflicto funcional. |
| CA1: errores | Base inaccesible, permisos insuficientes y fallo de escritura no generan éxito ni duplicados falsos; no se exponen secretos. |
| Regresión funcional | Validaciones obligatorias, ID autogenerado, ausencia de topología, anónimos, roles incorrectos y antifalsificación conservan su comportamiento. |
| CA2: organización | Contexto, fábrica, mapeos, repositorio y migraciones están en Infrastructure; configuración común y composición coherentes; UI consume Application. |
| CA3: limpieza | Revisar código, imports, paquetes directos y transitivos, migraciones y documentación vigente; no quedan dependencias activas de SQLite fuera del antecedente protegido. |
| CA3: documentación | Una persona puede configurar una instancia disponible, aplicar el esquema y registrar un parque siguiendo la nueva guía sin editar código. |
| Carpeta protegida | Comparar contenido o hashes antes y después: ningún archivo de `arnes/prompting/requerimientos/req001_alta_parque_solar` cambia. |
| Máximo de nombre | Aceptar un nombre de 100 caracteres y rechazar uno de 101 sin truncamiento, con pruebas xUnit de Domain y aceptación funcional. |

La indisponibilidad necesaria para verificar errores se provoca únicamente en el entorno aislado. Una inspección con cliente MySQL complementa la aceptación HTTP; no sustituye la lectura mediante el repositorio de la aplicación.

### Controles de calidad de la implementación

Ejecutar desde la raíz:

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

El arnés de arquitectura actual ya carga el ensamblado de SolarUY y pertenece a la solución. Debe ejecutarse tras modificar código, según las reglas del proyecto. Si falla, corregir el código; no modificar los guardarraíles para acomodar el cambio. Cualquier modificación de ese proyecto de pruebas exige la autorización humana prevista en las reglas.

La compilación y los controles arquitectónicos no demuestran compatibilidad de SQL ni conectividad. La aceptación requiere ejecutar el arnés nuevo contra MySQL, registrar sus resultados y revisar la limpieza de dependencias. Estas verificaciones funcionales no incorporan pruebas unitarias en capas externas.

### Condiciones para la planificación

El plan debe cubrir las cuatro capas, con Domain y Application limitadas a lo descrito, e incluir proveedor, configuración compartida, plantilla y archivo local excluido de Git, mapeo, repositorio, migración nueva, guía, limpieza y aceptación. El máximo de nombre queda confirmado en 100 caracteres. Antes de implementar se deben confirmar los datos del entorno MySQL. Si se solicita conservación de registros anteriores, se debe ampliar primero la especificación de transición. No se requiere modificar el antecedente protegido para completar esta modificación.
