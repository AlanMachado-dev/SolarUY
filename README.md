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

## Inicio rápido

Necesitás **.NET 10 y MySQL 8.4**. Python 3 se utiliza para generar el hash de la contraseña de acceso. Ejecutá los comandos en PowerShell desde la raíz del repositorio, donde está `SolarUY.slnx`.

**Primera configuración:**

1. Encender MySQL y disponer de una base dedicada, por ejemplo `solaruy`.
2. Crear el archivo de configuración local si todavía no existe:

   ```powershell
   if (-not (Test-Path SolarUY/appsettings.Local.json)) {
       Copy-Item SolarUY/appsettings.Local.example.json SolarUY/appsettings.Local.json
   }
   ```

3. Editar `SolarUY/appsettings.Local.json` con servidor, puerto, base y credenciales de MySQL. Configurar también un usuario de SolarUY con su hash de contraseña y el rol exacto `Administrador`, siguiendo la [guía de configuración y acceso](arnes/prompting/req002_cambio_base_datos/configuracion.md). El usuario de MySQL y el usuario de la aplicación son independientes; el archivo local queda excluido de Git.
4. Restaurar las dependencias y preparar las tablas:

   ```powershell
   dotnet restore SolarUY.slnx
   dotnet tool restore
   dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
   ```

**Para encenderlo en los siguientes usos**, iniciar MySQL y ejecutar:

```powershell
dotnet run --project SolarUY/SolarUY.csproj --launch-profile https
```

Esperar a que la terminal indique que la aplicación está escuchando y abrir [https://localhost:7178/cuenta/iniciar-sesion](https://localhost:7178/cuenta/iniciar-sesion). Iniciar sesión con el usuario de SolarUY configurado y entrar en **Registrar parque solar**. Al guardar nombre y ubicación, la confirmación muestra el ID y la capacidad inicial de `0 MW`.

Para detener el proyecto, presionar `Ctrl+C` en la terminal. La configuración y los datos se conservan. Las tablas se actualizan mediante migraciones explícitas; la aplicación no las aplica automáticamente al arrancar.

La [guía de configuración](arnes/prompting/req002_cambio_base_datos/configuracion.md) incluye los ejemplos completos y la solución a problemas de inicio. Los detalles de permisos y pruebas están en la [referencia técnica](arnes/prompting/req002_cambio_base_datos/referencia_tecnica.md).

<h2 align="center"> Decisiones Técnicas</h2>

Decidimos usar una arquitectura modular, el cual consiste de la aplicación desplegada en una sola capa física (monolito) pero dividido en varios módulos distintos bien separados. 

Esto tiene la ventaja de que combina la simplicidad de un monolito, con la organización y desacoplamiento de los microservicios.

También decidimos crear el proyecto como una aplicación web Blazor con renderizado interactivo WebAssembly para así poder tener una interfaz de monitoreo muy interactiva.

Blazor encaja mejor para una interfaz de monitoreo muy interactiva y desarrollar principalmente en C\#, y WebAssembly creemos que es la mejor opción tomando en cuenta el requerimiento de alta disponibilidad.

Además, para el monitoreo en tiempo real vamos a utilizar SignalR.

La persistencia actual utiliza **MySQL 8.4 con Entity Framework Core** y el proveedor oficial `MySql.EntityFrameworkCore`. La conexión se configura en el servidor mediante `SolarUY/appsettings.Local.json` y puede apuntar a una instancia local o remota. La aplicación y las herramientas de migración comparten esa configuración.

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

## Base MySQL

Desde PowerShell (Windows), en la raíz del repositorio:

```powershell
if (-not (Test-Path SolarUY/appsettings.Local.json)) {
    Copy-Item SolarUY/appsettings.Local.example.json SolarUY/appsettings.Local.json
}
dotnet tool restore
dotnet ef database update --project SolarUY/SolarUY.csproj --startup-project SolarUY/SolarUY.csproj
```

Antes de aplicar migraciones, editar `SolarUY/appsettings.Local.json` con la conexión a una base MySQL 8.4 vacía y dedicada. Ese archivo queda excluido de Git. La [guía de configuración MySQL](arnes/prompting/req002_cambio_base_datos/configuracion.md) explica cómo configurar el acceso y encender el proyecto. La [referencia técnica](arnes/prompting/req002_cambio_base_datos/referencia_tecnica.md) amplía TLS, permisos, prioridades de configuración, migraciones y diagnóstico. La conexión puede apuntar a otro equipo y puede sobrescribirse con `ConnectionStrings__SolarUy`.

La base no se migra automáticamente al arrancar. Este cambio prepara un esquema nuevo y no importa datos anteriores ni elimina bases locales.

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

Las verificaciones HTTP de aceptación utilizan identidades efímeras independientes de estas cuentas y esquemas MySQL aislados, sin modificar bases existentes. Preparar las opciones y permisos de aceptación indicados en la [referencia técnica](arnes/prompting/req002_cambio_base_datos/referencia_tecnica.md).

Después de compilar, ejecutar con Python nativo de Windows:

```powershell
python arnes/prompting/req002_cambio_base_datos/verificar_aceptacion.py
```

El arnés crea un esquema nuevo identificado, aplica la migración e inicia un servidor oculto en `127.0.0.1:5285` (puerto configurable). Comprueba persistencia, Unicode, concurrencia, errores y seguridad; registra resultados depurados en `artifacts/req002`, excluido de Git. Limpia exclusivamente los recursos efímeros que creó. La opción `--mantener-servidor` permite completar el recorrido con teclado. La carpeta `req001` se conserva como antecedente; su script histórico no valida MySQL.
## 

