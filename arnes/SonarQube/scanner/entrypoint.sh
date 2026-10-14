#!/bin/bash
set -e

# Verificaciones básicas de variables de entorno
if [ -z "$SONAR_HOST_URL" ]; then
    echo "ERROR: La variable SONAR_HOST_URL no está definida."
    echo "Ejemplo: -e SONAR_HOST_URL=http://localhost:9000"
    exit 1
fi

if [ -z "$SONAR_TOKEN" ]; then
    echo "ERROR: La variable SONAR_TOKEN no está definida."
    exit 1
fi

if [ -z "$PROJECT_KEY" ]; then
    echo "ERROR: La variable PROJECT_KEY no está definida."
    exit 1
fi

echo "========================================="
echo "Iniciando análisis de SonarQube para: $PROJECT_KEY"
echo "Servidor: $SONAR_HOST_URL"
echo "========================================="

# 1. Iniciar el escáner
dotnet sonarscanner begin \
  /k:"$PROJECT_KEY" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.login="$SONAR_TOKEN"

# 2. Compilar el proyecto
echo "Construyendo proyecto..."
dotnet build

# 3. Finalizar el análisis y enviar resultados
echo "Finalizando análisis y enviando resultados..."
dotnet sonarscanner end /d:sonar.login="$SONAR_TOKEN"

echo "========================================="
echo "Análisis completado exitosamente."
echo "========================================="
