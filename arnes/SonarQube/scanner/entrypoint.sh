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

domain_tests="tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj"
coverage_report="artifacts/coverage/sonarqube.xml"
coverage_options=()
if [ -f "$domain_tests" ]; then
    mkdir -p "$(dirname "$coverage_report")"
    coverage_options+=("/d:sonar.cs.vscoveragexml.reportsPaths=$coverage_report")
fi

# 1. Iniciar el escáner
dotnet sonarscanner begin \
  /k:"$PROJECT_KEY" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.exclusions="**/sonar-token.txt" \
  "${coverage_options[@]}" \
  /d:sonar.login="$SONAR_TOKEN"

# 2. Compilar el proyecto
echo "Construyendo proyecto..."
dotnet build --no-incremental

# 3. Ejecutar las pruebas de dominio y medir cobertura
if [ -f "$domain_tests" ]; then
    echo "Ejecutando pruebas de dominio con cobertura..."
    dotnet-coverage collect "dotnet test $domain_tests --no-build" -f xml -o "$coverage_report"
fi

# 4. Finalizar el análisis y enviar resultados
echo "Finalizando análisis y enviando resultados..."
dotnet sonarscanner end /d:sonar.login="$SONAR_TOKEN"

echo "========================================="
echo "Análisis completado exitosamente."
echo "========================================="
