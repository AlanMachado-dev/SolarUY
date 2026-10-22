# req001 — Especificaciones técnicas: Alta de Parque Solar

> Identificador: **req001 (alta de parque solar)**. La documentación está en requerimientos/req001_alta_parque_solar y las herramientas en tests/req001. Ver [estructura actual](../../README.md).

## Objetivo y alcance

Implementar el registro de un parque solar por un usuario autenticado con el rol exacto `Administrador`. El alta recibe nombre y ubicación (localidad / dirección), genera un identificador único y persiste un parque sin topología, con capacidad total estimada de **0 MW**.

Fuente funcional: `requirements.md` de esta carpeta. Reglas arquitectónicas: los siete documentos compartidos de [.agents/rules](../../../../.agents/rules), referidos por [AGENTS.md](../../../../AGENTS.md) en la raíz.

No forman parte de este requerimiento la edición, eliminación, listado general de parques ni la configuración de nodos, inversores o paneles. Tampoco se crean esos elementos automáticamente al registrar un parque.

### Contexto y decisiones de diseño

- La solución existente contiene `SolarUY` y `SolarUY.Client`, ambos en .NET 10, con Blazor y soporte de interactividad WebAssembly. Todavía no contiene capas de negocio separadas, persistencia ni autenticación configuradas.
- Las cuatro capas se organizan como módulos dentro del mismo proyecto servidor `SolarUY`, mediante carpetas `Domain`, `Application`, `Infrastructure` y `Presentation`, con sus respectivos namespaces `SolarUY.Domain`, `SolarUY.Application`, `SolarUY.Infrastructure` y `SolarUY.Presentation`. Estos nombres identifican namespaces, no nuevos proyectos `.csproj`.
- Para esta pantalla se propone renderizado estático de Blazor en el servidor (SSR), con envío del formulario al servidor. No requiere ejecutar el caso de uso en WebAssembly ni incorporar una API REST para este alcance.
- El caso de uso se implementa mediante un servicio de aplicación invocado directamente, sin MediatR ni un bus de mensajes.
- Persistencia propuesta: Entity Framework Core, exclusivamente en Infrastructure. El proveedor y la conexión se deberán definir durante la planificación; no existe un motor de base de datos elegido en el repositorio.
- Se considera que dos nombres son iguales después de quitar espacios de los extremos, normalizar Unicode a Form C y comparar sin distinguir mayúsculas mediante una clave en mayúsculas invariantes. Esta es una decisión de diseño propuesta, porque el requerimiento no define la equivalencia de nombres. Se conservan acentos y espacios interiores: `Parque Sur` y ` parque sur ` son duplicados; `Parque Sur` y `Parque  Sur` son distintos.
- Ubicación se captura como un único texto obligatorio, según la expresión “Localidad / Dirección” del requerimiento. No se agregan coordenadas ni un catálogo de localidades.
- No se imponen máximos de longitud o formatos geográficos no especificados. Si el proveedor necesita límites para el índice de nombres, estos deberán acordarse y documentarse antes de implementar la migración.

### Organización modular dentro del proyecto

```text
SolarUY/
├── SolarUY.csproj
├── Domain/
│   └── ParquesSolares/         # Entidad, objetos de valor y reglas locales
├── Application/
│   └── ParquesSolares/         # Servicios, contratos y DTO del caso de uso
├── Infrastructure/
│   ├── Persistencia/          # Contexto, mapeos y repositorios
│   └── Seguridad/             # Adaptador del usuario autenticado
├── Presentation/
│   └── Pages/                 # Páginas Blazor de los nuevos casos de uso
├── Components/                # Componentes existentes y estructura de Blazor
└── Program.cs                 # Composición e inicio de la aplicación
```

La separación es lógica: todas las capas compilan en el mismo ensamblado. Domain no utiliza tipos de las otras capas; Application utiliza Domain; Infrastructure implementa contratos de Application y utiliza Domain; Presentation consume los contratos y DTO de Application. La composición en `Program.cs` conecta las implementaciones mediante inyección de dependencias.

Al compartir un `.csproj`, el compilador no impide por sí solo las dependencias indebidas entre capas y los paquetes están disponibles para todo el proyecto. Las fronteras se mantienen mediante namespaces, reglas de diseño y verificaciones arquitectónicas sobre los tipos utilizados. Por ejemplo, agregar EF Core al proyecto no autoriza a usarlo desde Domain o desde un componente Blazor.

## 1. Domain

### Responsabilidades

Representar el parque y proteger sus invariantes mediante C# puro, sin referencias a Application, Infrastructure, Blazor, ASP.NET ni Entity Framework Core. Las reglas propias del objeto permanecen dentro del dominio; el servicio de aplicación coordina las operaciones externas.

### Modelo propuesto

| Elemento | Tipo / comportamiento | Regla |
| --- | --- | --- |
| `ParqueSolar.ID` | `Guid`, generado por el dominio al crear el objeto | No vacío; no recibido desde el formulario; inmutable después de crear el parque. |
| `ParqueSolar.Nombre` | Objeto de valor `NombreParqueSolar` | Obligatorio; contiene texto limpio para mostrar y la clave de comparación. |
| `ParqueSolar.Ubicacion` | `string` | Obligatoria; se quitan espacios de los extremos; no acepta texto compuesto solamente por espacios. |
| `ParqueSolar.CapacidadTotalEstimadaMw` | `decimal`, de solo lectura | Magnitud derivada en MW; devuelve `0m` para el parque recién creado y vacío. |

La creación se concentra en una fábrica o método `ParqueSolar.Crear(nombre, ubicacion)`. Este método valida los datos locales, genera el ID y devuelve el parque vacío. `NombreParqueSolar` encapsula la validación y la normalización de nombres; la UI y el repositorio no mantienen algoritmos propios diferentes.

El ID se respalda además con una clave primaria en persistencia. La generación de un GUID no reemplaza la restricción de unicidad del almacenamiento.

### Capacidad y topología

La capacidad no es un dato ingresado por el administrador ni una propiedad modificable desde el formulario. No se persiste un valor editable que pueda divergir de la topología.

El contrato de negocio para la futura configuración de topología es:

`CapacidadTotalEstimadaMw = suma de la generación máxima de cada panel perteneciente a los nodos del parque, expresada en MW`.

La suma de una topología vacía es cero. La incorporación del modelo y las relaciones de nodos y paneles corresponde al requerimiento de topología; este alta no necesita crear entidades provisionales para representarlos. Cuando se incorpore dicho modelo, el cálculo deberá pertenecer al dominio, recorrer relaciones entre objetos y convertir unidades explícitamente si los paneles expresan potencia en W o kW. Los inversores no suman potencia adicional a la capacidad definida por este requerimiento.

### Validación y unicidad

- Rechazar nombre o ubicación nulos, vacíos o compuestos solamente por espacios.
- Conservar las invariantes mediante propiedades de solo lectura o setters privados y métodos del dominio.
- La entidad no consulta una base de datos para verificar nombres duplicados. La unicidad global requiere coordinación en Application y una restricción definitiva en Infrastructure.
- Si se usan excepciones de dominio, sus nombres terminan en `Exception`, por ejemplo `DatosParqueSolarInvalidosException`.

### Pruebas unitarias de dominio

Durante la implementación se deben crear pruebas xUnit simultáneamente con el comportamiento de Domain, conforme a las reglas del proyecto:

- Creación válida: ID no vacío, nombre y ubicación limpios, capacidad `0m`.
- Dos creaciones generan identificadores distintos.
- Rechazo de nombre y ubicación nulos, vacíos y con espacios solamente.
- Igualdad de claves para nombres con diferencias de mayúsculas y espacios exteriores.
- Equivalencia de representaciones Unicode canónicamente iguales; conservación de acentos y espacios interiores significativos.

Las pruebas de suma y conversión de potencias se incorporarán cuando el requerimiento de topología implemente ese comportamiento.

## 2. Application

### Responsabilidades y dependencias

Depende solamente de Domain y de las bibliotecas base de .NET. Define el caso de uso, sus DTO y los contratos que implementa Infrastructure. No importa `DbContext`, SQL, `HttpContext`, `ClaimsPrincipal` ni componentes Blazor.

### Contratos propuestos

| Contrato | Responsabilidad |
| --- | --- |
| `IParqueSolarService` / `ParqueSolarService` | Ejecutar `CrearAsync` y devolver el resultado del alta. |
| `CrearParqueSolarDto` | Transportar únicamente `Nombre` y `Ubicacion`. |
| `ParqueSolarDto` | Devolver `ID`, nombre, ubicación y capacidad calculada en MW. |
| `ResultadoAltaParqueSolarDto` | Comunicar éxito, datos creados o errores tipados y asociados a campos. |
| `IParqueSolarRepository` | Consultar existencia por clave de nombre y guardar un parque mediante operaciones asíncronas. |
| `IUsuarioActualService` | Informar autenticación y pertenencia al rol desde el contexto confiable del servidor. |

Las dependencias de `ParqueSolarService` se reciben exclusivamente por constructor. El contrato de persistencia no expone consultas de EF ni detalles del proveedor. La operación de guardado devuelve un resultado específico para éxito o conflicto de nombre; los errores inesperados se propagan para su manejo centralizado, sin presentarlos como duplicados.

El DTO de entrada no incluye ID, capacidad, rol, usuario ni topología. El DTO de salida transporta valores listos para mostrar; el componente no recalcula ni decide reglas de negocio.

### Flujo del caso de uso

1. Consultar `IUsuarioActualService` y rechazar la acción si el usuario no está autenticado o no pertenece a `Administrador`, antes de consultar o escribir datos del parque.
2. Crear el objeto de dominio con los datos recibidos; traducir las validaciones conocidas a errores de campo.
3. Consultar si la clave normalizada del nombre ya existe. Si existe, devolver conflicto sin guardar.
4. Solicitar al repositorio la inserción y confirmación del parque.
5. Si otro alta concurrente reservó el mismo nombre, traducir el conflicto definitivo del repositorio al mismo error funcional de nombre duplicado.
6. Después de confirmar la persistencia, devolver el DTO del parque con su ID y capacidad de `0 MW`.

`CrearAsync` solamente registra el parque. No crea usuarios, roles ni componentes de topología como efecto secundario.

### Resultados funcionales

| Resultado | Comportamiento |
| --- | --- |
| Alta exitosa | Devuelve los datos efectivamente persistidos. |
| Datos inválidos | Devuelve errores de nombre o ubicación; no persiste. |
| Nombre duplicado | Devuelve “Ya existe un parque solar con ese nombre.”; no crea un segundo parque. |
| Usuario no autenticado | Rechaza la operación; Presentation ofrece el acceso al mecanismo de inicio de sesión configurado. |
| Usuario sin rol | Rechaza la operación; Presentation muestra acceso denegado. |
| Error inesperado de persistencia | No informa éxito; Presentation muestra un mensaje genérico y el servidor registra el detalle técnico. |

No se generan pruebas unitarias para Application, conforme a `7_flujo_trabajo_ia.md`.

## 3. Infrastructure

### Responsabilidades

Implementar los contratos de Application para persistencia y usuario actual. Puede depender de Application y Domain. Los componentes de Presentation no conocen estas implementaciones; su registro se realiza solamente en el punto de composición del servidor.

### Persistencia propuesta

- `ParqueSolarRepository` implementa `IParqueSolarRepository` y concentra las consultas y escrituras del caso de uso.
- Un `SolarUyDbContext` y el mapeo de `ParqueSolar` permanecen exclusivamente en Infrastructure; Domain no incorpora anotaciones ni referencias del ORM.
- El mapeo guarda ID como clave primaria, nombre visible, clave normalizada y ubicación. La capacidad se ignora en el mapeo por ser derivada.
- Nombre visible, clave de nombre y ubicación son obligatorios. La clave normalizada tiene un índice o restricción única, cuya comparación debe coincidir con la equivalencia definida en Domain. La configuración del proveedor no debe agregar equivalencias implícitas por acentos o espacios interiores.
- La consulta previa de existencia mejora la respuesta al usuario, pero la restricción única resuelve la concurrencia de forma definitiva.
- La inserción se confirma mediante una única operación de guardado. El repositorio identifica específicamente la violación de la restricción del nombre y la traduce a conflicto. No transforma cualquier fallo de persistencia en “nombre duplicado”.
- Un fallo durante la confirmación no deja un parque parcialmente creado. El alta no escribe registros de nodos, inversores ni paneles.
- El contexto de datos y el repositorio se reciben por constructor y tienen duración limitada a la operación o solicitud; no se comparten como singleton.

La migración inicial se diseñará después de elegir el proveedor de EF Core y los límites necesarios para la clave indexada. Credenciales y conexión se configuran fuera del código fuente.

### Identidad y autorización

`UsuarioActualService` implementa `IUsuarioActualService` y obtiene la identidad autenticada del contexto de la solicitud del servidor. No recibe roles declarados por el formulario o por el navegador.

El servidor debe contar con un mecanismo de autenticación y emisión de roles confiable antes de habilitar el alta. Este documento no presupone un proveedor de identidad existente ni agrega un sistema completo de administración de cuentas. La planificación deberá identificar o configurar el mecanismo, asegurar la asignación del rol `Administrador` y conectar autenticación y autorización al flujo HTTP.

La comprobación del servicio de aplicación se mantiene además de la protección de la ruta y del envío del formulario. Así, invocar el caso de uso por otro punto de entrada del servidor conserva la misma restricción.

No se generan pruebas unitarias para Infrastructure, conforme a las reglas del proyecto.

## 4. Presentation

### Pantalla y formulario

- Página propuesta: `SolarUY/Presentation/Pages/AltaParqueSolar.razor`, namespace `SolarUY.Presentation.Pages`, ruta `/parques-solares/nuevo`, con renderizado estático del servidor. La carpeta Presentation tendrá los imports Razor necesarios; el router existente podrá descubrir la página porque pertenece al mismo ensamblado.
- Encabezado: “Registrar parque solar”.
- Campos editables: “Nombre” y “Ubicación (localidad / dirección)”, ambos señalados como obligatorios.
- Información visible: “La capacidad inicial es 0 MW. Se calculará al incorporar nodos y paneles.” Esta información describe el alta; no calcula la capacidad.
- ID y capacidad no se solicitan ni se enlazan como entrada. Después del alta se muestran los valores devueltos por Application.
- Acción principal: “Registrar parque”. Usar un formulario Blazor con nombre de formulario explícito y vinculación compatible con POST en SSR.

El componente captura entradas, invoca `IParqueSolarService` y muestra el resultado. No consulta repositorios, no normaliza nombres para aplicar reglas, no decide duplicados y no calcula potencias. El estado visual y la asociación de mensajes con campos pertenecen a Presentation; la decisión funcional pertenece al servicio o al dominio.

### Seguridad y composición

- Proteger la ruta de alta y el procesamiento de su POST mediante autorización de servidor para el rol `Administrador`.
- Ajustar la integración de autorización de rutas de Blazor y los servicios de identidad necesarios. El router actual utiliza `RouteView` y no constituye por sí solo una protección de acceso.
- Mantener protección antifalsificación para el formulario; el servidor ya incluye `UseAntiforgery`, y el alta deberá integrarse con ese mecanismo.
- La navegación puede mostrar el acceso al alta según la identidad, pero ocultar un enlace no sustituye la autorización.
- El punto de composición en `SolarUY/Program.cs` registra los servicios de los módulos Application e Infrastructure y la autenticación. El uso de implementaciones concretas de Infrastructure fuera de su módulo queda limitado al arranque; los componentes solo consumen contratos de Application.
- El formulario SSR no ejecuta el servicio de negocio en `SolarUY.Client`. Ese proyecto no recibe dependencias de Infrastructure ni acceso a la base de datos.

### Respuesta al usuario

- Ante validaciones o duplicados, conservar los datos ingresados y mostrar el mensaje correspondiente junto al campo.
- Tras un guardado confirmado, mostrar “Parque solar registrado” y los datos devueltos: ID, nombre, ubicación y capacidad `0 MW`.
- Ofrecer nuevamente la acción de registrar otro parque mediante un formulario limpio. No depender de un listado o una página de detalle que todavía no existen.
- Ante un fallo inesperado, mostrar “No se pudo registrar el parque solar. Intentá nuevamente.” sin exponer excepciones, SQL ni credenciales.
- No anunciar éxito antes de que el servidor confirme el guardado.

No se generan pruebas unitarias para Presentation, conforme a las reglas del proyecto.

### Verificación de aceptación y trazabilidad

Estas verificaciones funcionales se ejecutarán durante la implementación; no constituyen una solicitud de crear pruebas unitarias en las capas externas.

| Requerimiento | Evidencia esperada |
| --- | --- |
| ID obligatorio, único y autogenerado | Alta válida devuelve ID no vacío; el almacenamiento impide repetir la clave; el formulario no permite ingresarlo. |
| Nombre obligatorio | Entradas nulas, vacías o de espacios se rechazan sin persistir. |
| Ubicación obligatoria | Entradas nulas, vacías o de espacios se rechazan sin persistir. |
| Capacidad inicial calculada | El parque vacío devuelve `0 MW`, sin campo editable de capacidad. |
| Solo administradores autenticados | Acceso y envío directo del formulario por anónimos o usuarios de otro rol se rechazan sin escrituras. |
| Sin nodos ni paneles al crear | El alta solo persiste el parque; no genera topología ni asignaciones. |
| Nombre único | Segundo alta con nombre equivalente se rechaza; dos solicitudes concurrentes equivalentes producen como máximo un parque. |
| Parque previo a la topología | El alta produce la identidad persistida que los futuros casos de uso de topología deberán requerir y verificar. |

### Control arquitectónico y condiciones para planificar

Al implementar código, ejecutar `dotnet test` sobre `arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj` y corregir el código ante fallos, respetando la prohibición de modificar los guardarraíles sin autorización humana.

Actualmente `ArchitectureSetup.cs` carga solamente el ensamblado de `System.Object`, y el proyecto de calidad no está incluido en `SolarUY.slnx`. Por eso, un resultado exitoso en su estado actual no demostraría que las capas propuestas cumplen las reglas. La conexión del ensamblado de `SolarUY` deberá resolverse en la planificación con la autorización requerida por las reglas para editar ese proyecto de pruebas. Los guardarraíles identificarán las cuatro capas por sus namespaces dentro de ese mismo ensamblado.

Antes de implementar deben quedar concretados el proveedor de persistencia y sus límites de índice, el mecanismo de autenticación y la emisión del rol. La equivalencia de nombres y la ubicación como texto único quedan documentadas como decisiones propuestas para revisión. Estas definiciones no impiden revisar este diseño ni pasar a la planificación de tareas.

## Decisiones de implementación (2026-10-14)

- Se adopta Trim, Unicode Form C y clave en mayúsculas invariantes, conservando acentos y espacios interiores. Ubicación es un único texto obligatorio. La solicitud de implementar estrictamente este diseño confirma estas decisiones sin ampliar el alcance funcional.
- Persistencia: EF Core 10 con SQLite. La clave normalizada usa comparación `BINARY` e índice único. SQLite permite indexar `TEXT` sin imponer máximos funcionales de longitud; no se introducen límites adicionales.
- La conexión se suministra mediante `ConnectionStrings:SolarUy` en configuración externa (variables de entorno o secretos de desarrollo). La base local de desarrollo se excluye del control de versiones. No se versionan conexiones del equipo ni credenciales.
- Autenticación: cookie emitida por el servidor tras verificar credenciales contra identidades provisionadas en configuración externa. Los hashes se verifican mediante `PasswordHasher` de ASP.NET Core Identity; no se almacenan contraseñas en texto plano ni se habilita registro de usuarios. Nombre y roles provienen exclusivamente de la configuración del servidor. Cookies HttpOnly, SameSite y antifalsificación protegen el flujo HTTP; producción requiere HTTPS.
- Desarrollo: provisionar externamente dos identidades independientes, una con rol exacto `Administrador` y otra con rol `Usuario`. Las identidades se preparan antes de probar el alta, nunca como efecto del caso de uso. El procedimiento y las claves se documentarán con la implementación.
- Servicios concretos: `SolarUY.Application.ParquesSolares.Servicios`; contratos y DTO: `SolarUY.Application.ParquesSolares`. Se preservan las cuatro capas dentro del proyecto servidor.
- El usuario autorizó explícitamente los ajustes descritos en `ajustes_arnes_propuestos.md` el 2026-10-14. Hasta conectar el arnés, sus resultados actuales no se consideran evidencia arquitectónica de SolarUY.
- SonarQube no se incorpora a esta entrega; R11–R13 son tareas condicionales no aplicables.
