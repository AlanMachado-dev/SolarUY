<h1 align="center">SolarUY</h1>


<h2 align="center">Sistema de Gestión de Parques Solares</h2>


<h2 align="center"> Integrantes</h2>

<table align="center">
  <tr>
    <th>Nombre</th>
  </tr>
  <tr>
    <td><a href="mailto:lucas.brito@estudiantes.utec.edu.uy">Lucas Brito</a></td>
  </tr>
  <tr>
    <td><a href="mailto:matthew.freire@estudiantes.utec.edu.uy">Matthew Freire</a></td>
  </tr>
  <tr>
    <td><a href="mailto:luciano.gamba@estudiantes.utec.edu.uy">Luciano Gamba</a></td>
  </tr>
  <tr>
    <td><a href="mailto:alan.machado@estudiantes.utec.edu.uy">Alan Machado</a></td>
  </tr>
</table>
En este taller desarrollamos una aplicación web bajo la plataforma .NET, denominada SolarUY, para la gestión integral de parques solares del país.

<h2 align="center"> Decisiones Técnicas</h2>

Decidimos usar una arquitectura modular, el cual consiste de la aplicación desplegada en una sola capa física (monolito) pero dividido en varios módulos distintos bien separados. 

Esto tiene la ventaja de que combina la simplicidad de un monolito, con la organización y desacoplamiento de los microservicios.

También decidimos crear el proyecto como una aplicación web Blazor con renderizado interactivo WebAssembly para así poder tener una interfaz de monitoreo muy interactiva.

Blazor encaja mejor para una interfaz de monitoreo muy interactiva y desarrollar principalmente en C\#, y WebAssembly creemos que es la mejor opción tomando en cuenta el requerimiento de alta disponibilidad.

Además, para el monitoreo en tiempo real vamos a utilizar SignalR.

<h2 align="center"> Diagrama de Arquitectura</h2>

<img width="1647" height="851" alt="image2" src="https://github.com/user-attachments/assets/ca5bd1ff-7e54-4d38-95e7-7162e3de1435" />

<h2 align="center">Modelo de Dominio </h2>

<img width="1239" height="647" alt="image1" src="https://github.com/user-attachments/assets/24013405-6b7d-4e85-8ae8-df316606ce14" />

<h2 align="center"> Proceso de Generación de Código con IA</h2>

Para este proyecto utilizamos la generación de código con un agente IA, específicamente aprovechamos la oferta de un mes gratuito de ChatGPT Plus donde nos ofrece el modelo GPT-6.1 Sol, un modelo más eficiente y superior.

El objetivo de programar de esta forma no es vibe-codear, ya que nos interesa la calidad del software y queremos priorizar la misma, no solo las funcionalidades. Para esto debemos entender como trabaja el software construido, dándole indicaciones al modelo (reglas no deterministas), y verificando lo realizado (chequeos deterministas) con SonarQube y   
ArchUnitNet. Le indicamos un flujo de trabajo creando un loop con la solicitud de intervención manual (human in the loop). Todo esto es nuestro arnés (harness) de programación.

**Para configurar la skill para generar specs en ChatGPT (Windows):**

* Entrar a C:\\Users\\\[TU USUARIO\]  
* Crear las carpetas **.agents\\skills**.  
* Copiar la carpeta completa sdd-generar-specs desde:  
  * SolarUY\\arnes\\.agents\\skills  
* Y pegarla en la carpeta creada anteriormente, quedando finalmente:  
  * C:\\Users\\\[TU USUARIO\]\\.agents\\skills\\sdd-generar-specs\\SKILL.md  
* Por último reiniciar la app de Chat GPT y verificar que la detecte

En el menú de la izquierda clickear en la pestaña "Complementos", luego "Habilidades" donde se mostrarán todas las skills. En el medio de la pantalla habrán 2 tabs, una de "Sistema" y una de "SolarUY" donde al entrar deberían ver la skill ya lista para ser utilizada.

**Prompt para solicitar que genere el specs.md:**  
Usá la skill \[LE MARCAMOS LA SKILL sdd-generar-specs CON /sdd generar specs\] para generar las especificaciones de la carpeta /arnes/prompting/\[CARPETA DEL REQUERIMIENTO. EJ. (req001\_alta\_parque\_solar)\]

**Prompt para solicitar que genere el tasks.md**  
Basándote en el diseño propuesto en el archivo arnes/prompting/\[CARPETA DEL REQUERIMIENTO\]/spec.md, genera un plan de implementación detallado. Desglosa el trabajo capa por capa en tareas pequeñas y atómicas. Genera un archivo llamado tasks.md utilizando el formato de checkboxes de Markdown (- \[ \]) para que podamos llevar el seguimiento del avance. Asegúrate de incluir tareas para correr las pruebas de ArchUnitNET y validar el código.

**Prompt para solicitar que implemente el requerimiento**  
Implementa la funcionalidad de \[CASO DE USO. EJ. (alta de parque solar)\] siguiendo estrictamente las decisiones arquitectónicas de arnes/prompting/\[CARPETA DEL REQUERIMIENTO\]/specs.md. Ejecuta paso por paso el plan detallado en tasks.md de la misma carpeta que specs.md. A medida que completes cada subtarea, marca el checkbox correspondiente con una x (- \[x\]). No avances a nueva capa hasta que el código de la actual compile correctamente y pase nuestras reglas definidas en los test unitarios. 

<h2 align="center">Descripción Funcionalidades </h2>

**Es una idea aproximada de cómo visualizamos el sistema en cuanto a las funcionalidades, NO las funcionalidades definitivas.**

| Caso de uso | Actor principal | Qué permitiría hacer |
| ----- | ----- | ----- |
| **Iniciar sesión** | Usuario | Acceder mediante un proveedor externo y obtener los permisos correspondientes. |
| **Gestionar parques solares** | Administrador | Crear parques, consultar sus datos y modificar sus configuraciones independientes. |
| **Configurar nodos de un parque** | Administrador | Registrar los nodos, su ubicación y las características necesarias, incluyendo su inversor. |
| **Administrar paneles** | Administrador | Registrar paneles, editar sus características y consultar en qué nodo están instalados. |
| **Consultar el estado general de un parque** | Operador | Ver la generación actual y un resumen del estado de sus nodos y paneles. |
| **Consultar un nodo o panel en tiempo real** | Operador | Examinar sus mediciones y su estado de salud, profundizando desde la vista del parque. |
| **Consultar información histórica** | Operador | Elegir un parque, nodo o panel y un período para consultar la generación y el funcionamiento registrados. |
| **Registrar una tarea de mantenimiento** | Operador / Administrador | Indicar qué paneles requieren atención, el motivo y el trabajo previsto. |
| **Consultar mantenimientos** | Operador | Revisar tareas de mantenimiento pendientes asignadas al usuario. |
| **Organizar mantenimientos** | Administrador | Asignar y establecer fechas para tareas de mantenimiento pendientes. |
| **Registrar la ejecución de un mantenimiento** | Operador | Informar qué trabajo se realizó, cuándo se hizo y cuál fue el resultado. |
| **Consultar información con lenguaje natural** | Operador | Formular preguntas como “¿cuánta energía generó este parque ayer?” y recibir respuestas limitadas a la información que puede consultar. |
## 
## 

# Configuración del alta de parque solar

Requiere .NET 10 y Python 3 para el generador auxiliar de hashes. Toda la funcionalidad se ejecuta en el servidor. No se registran usuarios desde el formulario de alta.

## Base SQLite

Desde PowerShell (Windows), en la raíz del repositorio:

```powershell
$env:ConnectionStrings__SolarUy = 'Data Source=C:/estudio/NET/SolarUY/solaruy-desarrollo.db'
dotnet tool restore
dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
```

La base no se migra automáticamente al arrancar; aplicar las migraciones antes de usar el alta. Para otros entornos, usar su ruta de conexión externa y el mismo procedimiento de migración. Los archivos SQLite locales se excluyen de Git.

## Identidades provisionadas por el servidor

Generar dos hashes, introduciendo contraseñas propias cuando se soliciten:

```powershell
python arnes/prompting/req001_alta_parque_solar/generar_hash.py
```

Configurar externamente las siguientes claves (los valores entre ángulos son indicaciones, no credenciales):

```powershell
$env:Autenticacion__Usuarios__0__Nombre = 'administrador-desarrollo'
$env:Autenticacion__Usuarios__0__PasswordHash = '<primer hash generado>'
$env:Autenticacion__Usuarios__0__Roles__0 = 'Administrador'
$env:Autenticacion__Usuarios__1__Nombre = 'usuario-desarrollo'
$env:Autenticacion__Usuarios__1__PasswordHash = '<segundo hash generado>'
$env:Autenticacion__Usuarios__1__Roles__0 = 'Usuario'
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

Las credenciales se validan mediante ASP.NET Core Identity y se emite una cookie de sesión por 30 minutos. Los roles se leen de esta configuración y se comparan exactamente; el navegador no puede asignarlos. Se exige al menos una identidad configurada y nombres únicos al arrancar. No se incluyen cuentas ni contraseñas por defecto. En producción, usar configuración protegida y HTTPS; la cookie exige transporte seguro fuera de desarrollo.

Abrir `/cuenta/iniciar-sesion` y usar la identidad administradora para acceder a `/parques-solares/nuevo`. La segunda identidad permite comprobar acceso denegado. Inicio y cierre de sesión requieren token antifalsificación; el inicio tiene un límite de 10 intentos por minuto y dirección IP.

## Controles

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Las verificaciones HTTP de aceptación usarán identidades efímeras independientes de estas cuentas y una base aislada, sin modificar bases existentes.

Después de compilar, ejecutar con Python nativo de Windows:

```powershell
python arnes/prompting/req001_alta_parque_solar/verificar_aceptacion.py
```

El arnés genera su propia base, aplica la migración, inicia un servidor oculto en `127.0.0.1:5284`, verifica F01–F22 y lo detiene al terminar. Ese puerto debe estar libre. Registra resultados y logs en `artifacts/req001`, excluido de Git. La opción `--mantener-servidor` lo deja activo solo si todas las verificaciones pasan, para completar F23 con teclado; el PID queda en `artifacts/req001/ultima-aceptacion.json`. Las credenciales efímeras de ese servidor se guardan únicamente en su directorio local de evidencia.
## 

