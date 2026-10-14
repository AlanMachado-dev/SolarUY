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
3. **Guarda muy bien este Token**, lo necesitarás para analizar tu código.

> **NOTA IMPORTANTE:** Al iniciar sesión, la pantalla de inicio de SonarQube te invitará a crear un proyecto ("Create a local project"). **No es necesario que lo hagas.** Puedes ignorar esa pantalla. Simplemente crea tu token como se indica arriba y sigue con el Paso 2; el script se encargará de crear tu proyecto en SonarQube de forma automática la primera vez que analice tu código.

---

## 2. Cómo Analizar tu Código

Ejecutar scripts (`analizar.sh` para Linux/Mac y `analizar.ps1` para Windows). 

1. Abre el script correspondiente a tu SO con tu editor de texto favorito (VS Code, Notepad, etc.).
2. En las primeras líneas, verás dos variables. Rellénalas con tus datos:
   ```bash
   SONAR_TOKEN="pega_tu_token_aqui"
   PROJECT_KEY="ElNombreQueLeDisteAlProyecto"
   ```
3. Guarda el archivo.
4. Abre tu terminal y ejecuta el script pasándole la ruta de la carpeta de tu proyecto C# (donde está tu `.sln` o `.csproj`). Si no le pasas ruta, analizará la carpeta actual.

   **En Linux/Mac:**
   ```bash
   chmod +x analizar.sh  # (Solo la primera vez para darle permisos)
   
   # Opción A: Pasando la ruta de tu proyecto
   ./analizar.sh /ruta/hacia/tu/proyecto
   
   # Opción B: Estando dentro de la carpeta de tu proyecto
   /ruta/hacia/sonar/analizar.sh
   ```

   **En Windows (PowerShell):**
   ```powershell
   # Opción A: Pasando la ruta de tu proyecto
   .\analizar.ps1 "C:\ruta\hacia\tu\proyecto"
   
   # Opción B: Estando dentro de la carpeta de tu proyecto
   C:\ruta\hacia\sonar\analizar.ps1
   ```

5. El script verificará si tienes la imagen del escáner; si no la tienes, **la construirá automáticamente la primera vez**.
6. Luego, verás cómo arranca mágicamente el contenedor, compila tu código, y envía los resultados.
7. Regresa a **http://localhost:9000** en tu navegador y actualiza la página. ¡Deberías ver tu proyecto analizado!

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
