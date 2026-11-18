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

Necesitás **.NET 10 y MySQL 8.4**. El acceso requiere una aplicación Auth0 de tipo Regular Web Application. Ejecutá los comandos en PowerShell desde la raíz del repositorio, donde está `SolarUY.slnx`.

**Primera configuración:**

1. Encender MySQL y disponer de una base dedicada, por ejemplo `solaruy`.
2. Crear el archivo de configuración local si todavía no existe:

   ```powershell
   if (-not (Test-Path SolarUY/appsettings.Local.json)) {
       Copy-Item SolarUY/appsettings.Local.example.json SolarUY/appsettings.Local.json
   }
   ```

3. Editar `SolarUY/appsettings.Local.json` con servidor, puerto, base y credenciales de MySQL. Agregar la sección privada OpenIdConnect y preparar el administrador en Auth0, siguiendo la [guía de acceso externo](arnes/prompting/requerimientos/req002_implementacion_openID/configuracion.md). El usuario de MySQL y el usuario de la aplicación son independientes; el archivo local queda excluido de Git.
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

Esperar a que la terminal indique que la aplicación está escuchando y abrir [https://localhost:7178/cuenta/iniciar-sesion](https://localhost:7178/cuenta/iniciar-sesion). Pulsar **Iniciar sesión con proveedor externo**, completar Universal Login con la cuenta administradora preparada y entrar en **Registrar parque solar**. Al guardar nombre y ubicación, la confirmación muestra el ID y la capacidad inicial de `0 MW`.

Para detener el proyecto, presionar `Ctrl+C` en la terminal. La configuración y los datos se conservan. Las tablas se actualizan mediante migraciones explícitas; la aplicación no las aplica automáticamente al arrancar.

La [guía de acceso externo](arnes/prompting/requerimientos/req002_implementacion_openID/configuracion.md) incluye Auth0, roles, configuración privada y diagnóstico. La [guía MySQL](arnes/prompting/modificaciones/mod001_cambio_base_datos/configuracion.md) conserva la preparación del esquema. Los detalles de permisos y pruebas están en la [referencia técnica](arnes/prompting/modificaciones/mod001_cambio_base_datos/referencia_tecnica.md).

## Ejecución con balanceador de carga

Para levantar Nginx y las dos instancias de SolarUY con Docker Compose, seguir la [guía de configuración del balanceador](arnes/prompting/tests/req008/configuracion.md). Incluye los requisitos, la preparación de certificados con PowerShell 7, los puertos y la red, la conexión a MySQL desde Docker, los callbacks OIDC y los comandos de arranque y apagado.

El acceso predeterminado es [https://localhost:8080](https://localhost:8080). Ambas instancias utilizan la configuración local y la misma base de datos; comparten las claves de protección de sesión mediante un volumen. La guía también explica la afinidad, la recuperación ante caídas y cómo preparar un entorno aislado de aceptación.

## Monitoreo en tiempo real

El detalle del nodo muestra la última medición guardada de cada panel, con la potencia en W, el estado y la fecha en hora de Uruguay. El monitoreo individual presenta la potencia en un medidor radial que se actualiza al recibir nuevas lecturas.

Cuando un Administrador o un Operador abre el monitoreo de un panel, el sensor simulado se inicia en el navegador con Blazor WebAssembly. Mientras la pantalla está abierta y el panel admite telemetría, el sensor envía una lectura de potencia por SignalR cada 20 segundos. El servidor guarda la medición con fecha UTC y publica la actualización.

Simular falla, simular arreglo, activar y desactivar son acciones exclusivas del Administrador. [Diagrama de WebAssembly](.agents/Images/Diagrama_WebAssembly.png).

## Documentación del proyecto

Accesos directos a las guías, los documentos de cada requerimiento y modificación, y las instrucciones de desarrollo. Al agregar documentación nueva, actualizar este índice.

### Requerimientos

| ID | Tema |
| --- | --- |
| [req001](arnes/prompting/requerimientos/req001_alta_parque_solar) | Alta de parque solar |
| [req002](arnes/prompting/requerimientos/req002_implementacion_openID) | Autenticación y autorización con OpenID Connect |
| [req003](arnes/prompting/requerimientos/req003_listar_parque_solar) | Listado de parques solares |
| [req004](arnes/prompting/requerimientos/req004_alta_nodo) | Alta de nodo |
| [req005](arnes/prompting/requerimientos/req005_detalle_nodo) | Detalle de nodo |
| [req006](arnes/prompting/requerimientos/req006_alta_panel_solar) | Alta de panel solar |
| [req007](arnes/prompting/requerimientos/req007_monitoreo) | Monitoreo de paneles en tiempo real |
| [req008](arnes/prompting/requerimientos/req008_balanceador_de_carga) | Balanceo de carga |
| [req009](arnes/prompting/requerimientos/req009_MCP) | Consultas en lenguaje natural mediante MCP |
| [req010](arnes/prompting/requerimientos/req010_generacion_energia) | Energía generada por parque, nodo y panel |
| [req011](arnes/prompting/requerimientos/req011_acciones_paneles) | Activación y desactivación de paneles |
| [req012](arnes/prompting/requerimientos/req012_mantenimiento_panel_solar) | Mantenimiento de paneles solares |

### Modificaciones

| ID | Tema |
| --- | --- |
| [mod001](arnes/prompting/modificaciones/mod001_cambio_base_datos) | Migración de SQLite a MySQL |
| [mod002](arnes/prompting/modificaciones/mod002_cambios_variados) | Cambios variados |
| [mod003](arnes/prompting/modificaciones/mod003_Dejar_de_mostrar_IDs) | Ocultar identificadores en la interfaz |
| [mod004](arnes/prompting/requerimientos/req005_detalle_nodo/mod004_seguridad_detalle_nodo) | Acceso del Operador al detalle del nodo |
| [mod005](arnes/prompting/requerimientos/req007_monitoreo/mod005_cambios_monitoreo) | Últimas mediciones, medidor radial e intervalo de 20 segundos |
| [mod006](arnes/prompting/requerimientos/req009_MCP/mod006/cambio-modelo-20261105.md) | Cambio de modelo del asistente a Gemini 3.5 Flash-Lite |
| [mod007](arnes/prompting/modificaciones/mod007_visual_solaruy) | Actualización visual de SolarUY y fechas en hora de Uruguay |
| [mod008](arnes/prompting/modificaciones/mod008_simulador_operador) | Sensor simulado para el Operador |

La aceptación de mod008 con cuentas reales y una base de datos aislada está pendiente. [Plan de tareas de mod008](arnes/prompting/modificaciones/mod008_simulador_operador/task.md).

### Guías de configuración y herramientas

- [Balanceador de carga: certificados, Docker Compose, MySQL, OIDC y recuperación](arnes/prompting/tests/req008/configuracion.md).
- [Acceso externo con Auth0: configuración, roles, inicio y cierre de sesión](arnes/prompting/requerimientos/req002_implementacion_openID/configuracion.md).
- [MySQL: configuración de conexión y arranque](arnes/prompting/modificaciones/mod001_cambio_base_datos/configuracion.md).
- [MySQL: referencia técnica, permisos y aceptación](arnes/prompting/modificaciones/mod001_cambio_base_datos/referencia_tecnica.md).
- [Chatbot de SolarUY: configuración y uso](arnes/prompting/tests/req009/configuration/configuracion.md).
- [OpenID Connect: resultados de aceptación y verificaciones pendientes](arnes/prompting/requerimientos/req002_implementacion_openID/aceptacion.md).
- [Arnés y herramientas de control de calidad](arnes/README.md).
- [SonarQube: preparación del entorno, token en archivo local y análisis](arnes/SonarQube/README.md).
- [Índice de prompting y equivalencias entre nombres actuales e históricos](arnes/prompting/README.md).
- [Configuración original del alta de parques — histórica, con SQLite y acceso local](arnes/prompting/requerimientos/req001_alta_parque_solar/configuracion.md).

### Guías de pruebas funcionales

- [req003 — listado de parques solares](arnes/prompting/tests/req003/README.md).
- [req004 — alta de nodo](arnes/prompting/tests/req004/README.md).
- [req005 — detalle de nodo](arnes/prompting/tests/req005/README.md).
- [req006 — alta de panel solar](arnes/prompting/tests/req006/README.md).
- [req008 — configuración de pruebas de balanceo de carga](arnes/prompting/tests/req008/configuracion.md).
- [req009 — configuración de pruebas del asistente MCP](arnes/prompting/tests/req009/configuracion.md).
- [mod002 — cambios variados](arnes/prompting/tests/mod002/README.md).
- [mod003 — presentación sin identificadores](arnes/prompting/tests/mod003/README.md).
- [mod004 — seguridad del detalle de nodo](arnes/prompting/tests/mod004/README.md).
- [mod005 — verificación de mediciones](arnes/prompting/tests/mod005/README.md).

Las comprobaciones de req001, req002 y mod001 se describen en sus guías de configuración, aceptación o referencia técnica enlazadas arriba.

### Arquitectura y trabajo con IA

- [Instrucciones generales del proyecto](AGENTS.md).
- [Clean Architecture](.agents/rules/1_clean_architecture.md).
- [Principios SOLID](.agents/rules/2_principios_solid.md).
- [Convenciones de nomenclatura](.agents/rules/3_convenciones_nomenclatura.md).
- [Modelado de dominio](.agents/rules/4_modelado_dominio.md).
- [Reglas de presentación](.agents/rules/5_capa_presentacion.md).
- [Protección de diagramas](.agents/rules/6_diagramas_de_SolarUY.md).
- [Flujo de trabajo, pruebas y evidencias](.agents/rules/7_flujo_trabajo_ia.md).
- [Skill para generar especificaciones](.agents/skills/sdd-generar-specs/SKILL.md) y [skill para generar tareas](.agents/skills/sdd-generar-tasks/SKILL.md).
- [Diagrama de arquitectura](.agents/Images/Diagrama_de_Arquitectura_General.png) y [modelo de dominio](.agents/Images/Modelo_de_Dominio.png).
- [Registro histórico de ajustes del arnés](arnes/prompting/ajustes_arnes_propuestos.md).

<h2 align="center"> Decisiones Técnicas</h2>

Decidimos usar una arquitectura modular, el cual consiste de la aplicación desplegada en una sola capa física (monolito) pero dividido en varios módulos distintos bien separados. 

Esto tiene la ventaja de que combina la simplicidad de un monolito, con la organización y desacoplamiento de los microservicios.

También decidimos crear el proyecto como una aplicación web Blazor con renderizado interactivo WebAssembly para así poder tener una interfaz de monitoreo muy interactiva.

Blazor encaja mejor para una interfaz de monitoreo muy interactiva y desarrollar principalmente en C\#, y WebAssembly creemos que es la mejor opción tomando en cuenta el requerimiento de alta disponibilidad.

Además, para el monitoreo en tiempo real vamos a utilizar SignalR.

La persistencia actual utiliza **MySQL 8.4 con Entity Framework Core** y el proveedor oficial `MySql.EntityFrameworkCore`. La conexión se configura en el servidor mediante `SolarUY/appsettings.Local.json` y puede apuntar a una instancia local o remota. La aplicación y las herramientas de migración comparten esa configuración.

<h2 align="center"> Diagrama de Arquitectura</h2>

<img width="1229" height="766" alt="diagrama_arquitectura" src="https://github.com/user-attachments/assets/01f42b35-c4e6-488c-bb4b-43904e2229c3" />

<h2 align="center">Modelo de Dominio </h2>

<img width="1034" height="569" alt="image2" src="https://github.com/user-attachments/assets/d5fecc92-ec00-4a3c-8590-7449eea077c5" />

<h2 align="center"> Proceso de Generación de Código con IA</h2>

Para este proyecto utilizamos la generación de código con un agente IA, específicamente aprovechamos la oferta de un mes gratuito de ChatGPT Plus donde nos ofrece el modelo GPT-6.1 Sol, un modelo más eficiente y superior.

El objetivo de programar de esta forma no es vibe-codear, ya que nos interesa la calidad del software y queremos priorizar la misma, no solo las funcionalidades. Para esto debemos entender como trabaja el software construido, dándole indicaciones al modelo (reglas no deterministas), y verificando lo realizado (chequeos deterministas) con SonarQube y   
ArchUnitNet. Le indicamos un flujo de trabajo creando un loop con la solicitud de intervención manual (human in the loop). Todo esto es nuestro arnés (harness) de programación.

**Configuración del agente y skills del proyecto:**

Las instrucciones compartidas están en [AGENTS.md](AGENTS.md), que remite a las reglas de [.agents/rules](.agents/rules). Los diagramas oficiales están en [.agents/Images](.agents/Images).

Las skills [sdd-generar-specs](.agents/skills/sdd-generar-specs/SKILL.md) y [sdd-generar-tasks](.agents/skills/sdd-generar-tasks/SKILL.md) se mantienen en la raíz. Codex puede descubrirlas desde el repositorio; para trabajar en SolarUY no es necesario copiarlas a la carpeta personal ni mantener duplicados en el arnés.

Para generar especificaciones usar $sdd-generar-specs; para planificar tareas usar $sdd-generar-tasks, indicando la carpeta del requerimiento o modificación. Ver la [organización del arnés](arnes/README.md).

El [índice de requerimientos y modificaciones](arnes/prompting/README.md) identifica las carpetas vigentes, las pruebas auxiliares y los nombres históricos de sus evidencias.

**Prompt para solicitar que genere el specs.md:**  
Usá la skill \[LE MARCAMOS LA SKILL sdd-generar-specs CON /sdd generar specs\] para generar las especificaciones de la carpeta /arnes/prompting/requerimientos/\[CARPETA DEL REQUERIMIENTO. EJ. (req001\_alta\_parque\_solar)\]

**Prompt para solicitar que genere el tasks.md**  
Usá la skill \[LE MARCAMOS LA SKILL sdd-generar-tasks CON /sdd generar tasks\] para generar las tareas de la carpeta /arnes/prompting/requerimientos/\[CARPETA DEL REQUERIMIENTO. EJ. (req001\_alta\_parque\_solar)\]


**Prompt para solicitar que implemente el requerimiento**  
Implementa la funcionalidad de \[CASO DE USO. EJ. (alta de parque solar)\] siguiendo estrictamente las decisiones arquitectónicas de arnes/prompting/requerimientos/\[CARPETA DEL REQUERIMIENTO\]/specs.md. Ejecuta paso por paso el plan detallado en tasks.md de la misma carpeta que specs.md. A medida que completes cada subtarea, marca el checkbox correspondiente con una x (- \[x\]). No avances a nueva capa hasta que el código de la actual compile correctamente y pase nuestras reglas definidas en los test unitarios.

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
