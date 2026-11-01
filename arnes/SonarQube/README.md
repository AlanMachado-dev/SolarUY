# Entorno SonarQube Local para .NET

Esta carpeta contiene la configuración necesaria para levantar un servidor de SonarQube local y analizar proyectos en C#/.NET sin necesidad de instalar SDKs de manera global en tu máquina host. Todo corre mediante Docker.

## Requisitos
- **Docker** instalado y funcionando en tu sistema (Docker Engine en Linux, o Docker Desktop en Windows/Mac).

---

## 1. Levantar el Servidor SonarQube

1. Abre tu terminal y ubícate en esta carpeta (donde está el archivo `docker-compose.yml`).
2. Ejecuta el comando para encender la infraestructura:
   ```bash
   docker compose up -d
   ```
3. Espera un par de minutos a que el servidor inicie. Puedes revisar los logs con `docker compose logs -f sonarqube`.
4. Abre en tu navegador: **http://localhost:9000**
5. Inicia sesión con:
   - **Usuario:** `admin`
   - **Contraseña:** `admin`
6. El sistema te pedirá cambiar la contraseña por defecto.

### 1.1 Crear el Token de Seguridad
Para que el analizador se comunique con el servidor, necesita un Token:
1. En SonarQube, ve a **My Account** (arriba a la derecha, click en el icono de usuario) > **Security**.
2. En **Generate Tokens**, ingresa un nombre (ej. `mi-token-local`), selecciona tipo `User Token`, y sin expiración (o a conveniencia).
3. Guardá el token en el archivo local **sonar-token.txt**, como se explica a continuación. No lo pegues en los scripts ni en la documentación.

> **NOTA IMPORTANTE:** Al iniciar sesión, la pantalla de inicio de SonarQube te invitará a crear un proyecto ("Create a local project"). **No es necesario que lo hagas.** Puedes ignorar esa pantalla. Simplemente crea tu token como se indica arriba y sigue con el Paso 2; el script se encargará de crear tu proyecto en SonarQube de forma automática la primera vez que analice tu código.

---

## 2. Cómo analizar tu código

Los scripts [analizar.ps1](analizar.ps1) para Windows y [analizar.sh](analizar.sh) para Linux/Mac leen el mismo archivo **sonar-token.txt**, ubicado junto a ellos. La ubicación del archivo no depende de la carpeta desde la que ejecutes el análisis.

### 2.1 Preparar el archivo privado

Desde la raíz del repositorio, crear el archivo a partir de [sonar-token.example.txt](sonar-token.example.txt), solo si todavía no existe:

**Windows (PowerShell):**

~~~powershell
if (-not (Test-Path arnes/SonarQube/sonar-token.txt)) {
    Copy-Item arnes/SonarQube/sonar-token.example.txt arnes/SonarQube/sonar-token.txt
}
~~~

**Linux/Mac (Bash):**

~~~bash
if [ ! -e arnes/SonarQube/sonar-token.txt ]; then
    cp arnes/SonarQube/sonar-token.example.txt arnes/SonarQube/sonar-token.txt
    chmod 600 arnes/SonarQube/sonar-token.txt
fi
~~~

Abrí sonar-token.txt con tu editor y reemplazá el marcador por el token generado en SonarQube. Guardalo como texto UTF-8, con **solo el token en una línea**, sin comillas ni la asignación SONAR_TOKEN=. No compartas ese archivo. Si ya existe, conservarlo; para cambiar de token basta con editarlo.

Retirar un token de los scripts no lo elimina de las versiones anteriores de Git. Como los tokens anteriores estaban versionados, renovarlos en SonarQube y guardar el reemplazo únicamente en sonar-token.txt.

El archivo privado queda excluido de Git mediante la regla /arnes/SonarQube/sonar-token.txt. La plantilla contiene únicamente un marcador y sí se versiona. El punto de entrada del escáner excluye los archivos sonar-token.txt mediante sonar.exclusions; ver la [configuración oficial del escáner](https://docs.sonarsource.com/sonarqube-community-build/analyzing-source-code/scanners/dotnet/configuring).

Los scripts rechazan un archivo ausente, ilegible, vacío, con el marcador de ejemplo o con varios valores separados por espacios o líneas. Muestran un error sin imprimir su contenido, antes de contactar SonarQube o iniciar Docker. No utilizan un token alternativo del entorno si el archivo falta.

### 2.2 Ejecutar el análisis

Ambos scripts usan PROJECT_KEY="SolarUY". Solo necesitás editar esa variable si querés analizar otro proyecto con una clave diferente. El token se mantiene siempre en el archivo privado.

Desde la raíz del repositorio:

**Windows (PowerShell):**

~~~powershell
.\arnes\SonarQube\analizar.ps1 .
~~~

**Linux/Mac (Bash):**

~~~bash
bash arnes/SonarQube/analizar.sh .
~~~

También podés pasar como argumento la ruta de otro proyecto, entre comillas si tiene espacios. Si omitís el argumento, se analiza la carpeta actual. El archivo sonar-token.txt se busca siempre junto al script, no en la carpeta del proyecto que se analiza.

El script configura el Quality Gate, construye o actualiza la imagen del escáner y ejecuta el análisis. Docker reutiliza las capas sin cambios. El token se entrega al contenedor por una variable de entorno y no se copia a la imagen ni se muestra en los mensajes del script.

Al finalizar, revisar los resultados en **http://localhost:9000**. Un token rechazado o vencido debe reemplazarse en sonar-token.txt por otro generado en SonarQube; no modificar los scripts para renovarlo.

---

## 3. Cómo Leer tus Resultados en SonarQube

La interfaz de SonarQube tiene mucha información. Para no perderte, enfócate en estas **3 pestañas principales** (ubicadas en el menú superior al entrar a tu proyecto):

### A. Overview (Resumen)
Es la portada de tu proyecto. Lo más importante aquí es el recuadro grande que dice **Passed** o **Failed** (el "Quality Gate"). Te indica rápidamente si tu código cumple con los estándares mínimos de calidad de la industria.

### B. Issues (Problemas)
¡Esta es tu área de trabajo! Aquí verás una lista detallada de todos los errores, bugs y "Code Smells" (malas prácticas). 
* **Tip de Aprendizaje:** Si haces clic en un problema, SonarQube te mostrará la línea exacta de código donde te equivocaste. Si haces clic en la pestaña **"Why is this an issue?"** (¿Por qué es un problema?), te dará una explicación teórica de por qué esa práctica está mal y cómo se escribe correctamente en C#.

### C. Measures (Medidas)
Aquí encontrarás gráficas y métricas detalladas del proyecto completo.
* **¿Cómo ver el Código Duplicado?** La forma más fácil de verlo en las versiones nuevas de SonarQube es directamente desde la pestaña **Overview**. En la parte inferior derecha verás un indicador llamado **Duplications** con un porcentaje. Si haces clic en ese número, el sistema te llevará exactamente a los archivos y a las líneas de código que copiaste y pegaste.
