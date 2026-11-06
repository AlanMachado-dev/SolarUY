FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build
WORKDIR /src
# En .NET 10 los archivos Razor deben existir al restaurar para incluir los assets de Blazor.
COPY SolarUY/ SolarUY/
COPY SolarUY.Client/ SolarUY.Client/
RUN dotnet restore SolarUY/SolarUY.csproj
RUN dotnet publish SolarUY/SolarUY.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/claves && chown app:app /app/claves
WORKDIR /app
COPY --from=build /out/ .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SolarUY.dll"]
