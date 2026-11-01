#!/bin/bash

# =========================================================================
# CONFIGURACIÓN DEL PROYECTO
# =========================================================================

# El token se lee de sonar-token.txt, junto a este script; no se guarda en código.

# 2. Inventa un nombre para tu proyecto (sin espacios) o usa el nombre de tu taller
PROJECT_KEY="SolarUY"

# =========================================================================
# NO MODIFICAR ABAJO DE ESTA LÍNEA
# =========================================================================

# =========================================================================
# RUTAS Y VALIDACIONES
# =========================================================================

# 3. Leer la ruta del proyecto desde el primer parámetro (o usar la carpeta actual por defecto)
PROJECT_PATH="${1:-$(pwd)}"

# Convertir a ruta absoluta (requerido por Docker para volúmenes)
ABS_PATH=$(realpath "$PROJECT_PATH")

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
TOKEN_FILE="$SCRIPT_DIR/sonar-token.txt"
if [ ! -f "$TOKEN_FILE" ] || [ ! -r "$TOKEN_FILE" ]; then
    echo "ERROR: Falta sonar-token.txt o no se puede leer. Copiá sonar-token.example.txt junto al script y completá tu token." >&2
    exit 1
fi
if ! SONAR_TOKEN="$(cat -- "$TOKEN_FILE")"; then
    echo "ERROR: No se pudo leer sonar-token.txt." >&2
    exit 1
fi
# Admitir UTF-8 con BOM y espacios/saltos de línea al principio o al final.
SONAR_TOKEN="${SONAR_TOKEN#$'\xEF\xBB\xBF'}"
SONAR_TOKEN="${SONAR_TOKEN#"${SONAR_TOKEN%%[![:space:]]*}"}"
SONAR_TOKEN="${SONAR_TOKEN%"${SONAR_TOKEN##*[![:space:]]}"}"
if [[ -z "$SONAR_TOKEN" || "$SONAR_TOKEN" == "pega_tu_token_aqui" || "$SONAR_TOKEN" =~ [[:space:]] ]]; then
    echo "ERROR: sonar-token.txt debe contener solo tu token en una línea, sin el marcador de ejemplo." >&2
    exit 1
fi

if [ ! -d "$ABS_PATH" ]; then
    echo "ERROR: El directorio $ABS_PATH no existe."
    exit 1
fi

# =========================================================================
# CONFIGURAR QUALITY GATE ESTRICTO
# =========================================================================
echo "Configurando Quality Gate Estricto en SonarQube..."
QG_NAME="EstrictoTaller"
# Intentar crear el Quality Gate y agregarle condiciones de 0 tolerancia a bloqueos (falla silenciosamente si ya existe)
curl -s -u "${SONAR_TOKEN}:" -X POST "http://localhost:9000/api/qualitygates/create?name=${QG_NAME}" > /dev/null
curl -s -u "${SONAR_TOKEN}:" -X POST "http://localhost:9000/api/qualitygates/create_condition?gateName=${QG_NAME}&metric=blocker_violations&op=GT&error=0" > /dev/null
curl -s -u "${SONAR_TOKEN}:" -X POST "http://localhost:9000/api/qualitygates/create_condition?gateName=${QG_NAME}&metric=security_rating&op=GT&error=1" > /dev/null
curl -s -u "${SONAR_TOKEN}:" -X POST "http://localhost:9000/api/qualitygates/create_condition?gateName=${QG_NAME}&metric=reliability_rating&op=GT&error=1" > /dev/null
curl -s -u "${SONAR_TOKEN}:" -X POST "http://localhost:9000/api/qualitygates/set_as_default?name=${QG_NAME}" > /dev/null

IMAGE_NAME="mi-taller-scanner"
echo "Actualizando la imagen del escáner (Docker reutiliza las capas sin cambios)..."
if ! docker build -t "$IMAGE_NAME" "$SCRIPT_DIR/scanner"; then
    echo "ERROR: No se pudo construir la imagen del escáner."
    exit 1
fi

echo "Iniciando contenedor analizador para $PROJECT_KEY..."
echo "Directorio a analizar: $ABS_PATH"

# Docker hereda el token del entorno, sin incluir su valor en los argumentos.
export SONAR_TOKEN
docker run --rm --network host \
  -v "$ABS_PATH":/app \
  -e SONAR_HOST_URL="http://localhost:9000" \
  -e SONAR_TOKEN \
  -e PROJECT_KEY="$PROJECT_KEY" \
  "$IMAGE_NAME"
