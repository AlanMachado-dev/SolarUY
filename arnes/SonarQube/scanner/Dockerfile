FROM mcr.microsoft.com/dotnet/sdk:10.0

# Instalar JRE (requerido por SonarScanner)
RUN apt-get update && \
    apt-get install -y default-jre && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Instalar la herramienta dotnet-sonarscanner globalmente
RUN dotnet tool install --global dotnet-sonarscanner

# Agregar las herramientas globales de dotnet al PATH
ENV PATH="$PATH:/root/.dotnet/tools"

# Copiar el script de punto de entrada
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Directorio de trabajo predeterminado donde montaremos el volumen del código
WORKDIR /app

# Al arrancar el contenedor, ejecutar el script
ENTRYPOINT ["/entrypoint.sh"]
