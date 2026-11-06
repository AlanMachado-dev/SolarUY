using SolarUY.Application.Asistente;

namespace SolarUY.Infrastructure.Asistente;

public static class AsistenteConfiguration
{
    public static void AgregarConfiguracion(WebApplicationBuilder builder)
    {
        // Archivo adicional optativo, nunca reemplaza appsettings.Local.json.
        var archivo = builder.Configuration["AsistenteArchivoPrivado"];
        var opciones = new AsistenteOptions();
        try
        {
            builder.Configuration.GetSection("Asistente").Bind(opciones);
            if (!string.IsNullOrWhiteSpace(archivo))
            {
                var privada = new ConfigurationBuilder().SetBasePath(builder.Environment.ContentRootPath)
                    .AddJsonFile(archivo, optional: true, reloadOnChange: false).Build();
                privada.GetSection("Asistente").Bind(opciones);
            }
            new ConfigurationBuilder().AddEnvironmentVariables().Build().GetSection("Asistente").Bind(opciones);
        }
        catch (Exception error) when (error is IOException or FormatException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            opciones.Habilitado = false;
        }
        // Una configuración inválida deshabilita solo el asistente, no la aplicación.
        if (opciones.ResultadosPagina <= 0 || opciones.MaximoPagina < opciones.ResultadosPagina || opciones.MaximoPagina > 200
            || opciones.MaximoIds is <= 0 or > 100 || opciones.MaximoDias is <= 0 or > 31
            || opciones.VigenciaSegundos <= 0 || opciones.HuecoSegundos <= 0
            || opciones.MaximoPregunta is <= 0 or > 2000 || opciones.MaximoMensajes is < 2 or > 30
            || opciones.MaximoContexto is < 2000 or > 32000 || opciones.MaximoContexto < opciones.MaximoPregunta
            || opciones.MaximoRondas is <= 0 or > 8 || opciones.MaximoInvocaciones is <= 0 or > 12
            || opciones.TiempoTurnoSegundos is <= 0 or > 60)
            opciones.Habilitado = false;
        builder.Services.AddSingleton(opciones);
        builder.Services.AddHttpClient<IModeloConversacional, GeminiConversacional>(cliente =>
            cliente.Timeout = TimeSpan.FromSeconds(Math.Clamp(opciones.TiempoTurnoSegundos, 1, 60)))
            .RemoveAllLoggers();
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.None);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None);
    }
}
