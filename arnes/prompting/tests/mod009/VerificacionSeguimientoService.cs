using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Asistente;

/// <summary>Regresión por MCP real con datos aislados existentes; no escribe ni emite sesiones OIDC.</summary>
static class VerificacionSeguimientoService
{
    public static async Task EjecutarAsync(bool geminiReal)
    {
        var verificaciones = new List<object>();
        void Comprobar(bool correcto, string caso)
        {
            verificaciones.Add(new { Caso = caso, Correcto = correcto });
            Console.WriteLine((correcto ? "OK " : "ERROR ") + caso);
            if (!correcto) throw new InvalidOperationException();
        }
        try
        {
            var privada = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(
                ".solaruy-acceptance/mod009/host.private.json")).Build();
            var conexion = new MySqlConnectionStringBuilder(privada.GetConnectionString("SolarUy"));
            if (conexion.Server != "127.0.0.1" || conexion.Port != 43329 || conexion.Database != "solaruy_mod009")
                throw new InvalidOperationException();
            using var datos = JsonDocument.Parse(await File.ReadAllTextAsync("artifacts/mod009/oidc-datos.json"));
            var instante = datos.RootElement.GetProperty("InstanteUtc").GetDateTimeOffset().AddSeconds(5);
            var panel = datos.RootElement.GetProperty("Principal").GetGuid();
            var reloj = new RelojFuncional(instante);
            var usuario = new UsuarioFuncional(reloj) { Rol = "Operador" };
            var opciones = new AsistenteOptions { Habilitado = true, ClaveApi = "auxiliar-sin-credencial" };
            if (geminiReal)
                new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(".solaruy-private/asistente.private.json"))
                    .Build().GetSection("Asistente").Bind(opciones);
            if (!opciones.Disponible) throw new InvalidOperationException();
            var db = new DbContextOptionsBuilder<SolarUyDbContext>();
            PersistenciaConfiguration.ConfigurarContexto(db, conexion.ConnectionString);
            var servicios = new ServiceCollection();
            servicios.AddSingleton<TimeProvider>(reloj).AddSingleton(opciones).AddSingleton<IUsuarioActualService>(usuario)
                .AddSingleton<IDbContextFactory<SolarUyDbContext>>(new ContextoFactory(db.Options))
                .AddScoped<PeriodoConsultaService>().AddScoped<FormatoRespuestaAsistenteService>()
                .AddScoped<IConsultaOperativaRepository, ConsultaOperativaRepository>()
                .AddScoped<IConsultaOperativaService, ConsultaOperativaService>();
            await using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions
                { ValidateOnBuild = true, ValidateScopes = true });
            var cliente = new ClienteHerramientasMcp((referencia, cancelacion) =>
            {
                var ambito = proveedor.CreateAsyncScope();
                var entrada = new Pipe(); var salida = new Pipe();
                IAsyncDisposable servidor = new ServidorConAmbito(new ServidorAsistenteMcp(entrada.Reader.AsStream(),
                    salida.Writer.AsStream(), ambito.ServiceProvider.GetRequiredService<IConsultaOperativaService>(),
                    referencia, cancelacion), ambito);
                return Task.FromResult((salida.Reader.AsStream(), entrada.Writer.AsStream(), servidor));
            });
            var observado = new ClienteSeguimiento(cliente);
            var formato = new FormatoRespuestaAsistenteService();
            var periodos = new PeriodoConsultaService(reloj, opciones);
            var consulta = new ConsultaOperativaDto { Tipo = "Panel", Id = panel,
                Periodo = "Ultimos", CantidadPeriodo = 2, UnidadPeriodo = "Minutos" };
            var modeloResumen = new ModeloEscenario(turno => turno.Intercambios.Count == 0
                ? new(CodigoAsistente.Exito, null, [Llamada("ResumirMediciones", consulta)])
                : new(CodigoAsistente.Exito, "Con las mediciones disponibles, el promedio de potencia de Mod009 principal, "
                    + "del nodo Mod009 Norte, es de 200,25 W.\n\n"
                    + JsonDocument.Parse(turno.Intercambios.Last().Resultados.Last().ContenidoJson).RootElement
                        .GetProperty("Presentacion").GetProperty("Periodo").GetString(), []));
            const string pregunta = "¿Cuál es el promedio del panel Mod009 principal en los últimos dos minutos?";
            var resumen = await new AsistenteService(usuario, modeloResumen, observado, periodos, opciones, formato)
                .PreguntarAsync(new(pregunta, "mod009-sintetica", [], null), default);
            Comprobar(resumen.Codigo == CodigoAsistente.Exito, "F07_resumen_MCP_real_exitoso");
            var periodo = resumen.Periodos?.Single();
            Comprobar(periodo is not null && periodo.DesdeUtc == instante.AddMinutes(-2)
                && periodo.HastaUtc == instante, "F07_respuesta_conserva_limites_exactos");
            Comprobar(periodo!.HastaUtc.Ticks % TimeSpan.TicksPerSecond != 0,
                "F07_escenario_incluye_fracciones_no_visibles");
            var resumenReal = observado.Resultados.Single(r => r.Herramienta == "ResumirMediciones").Resultado;
            Comprobar(JsonSerializer.SerializeToElement(resumenReal.Datos)[0].GetProperty("Muestras")
                .GetProperty("Cantidad").GetInt64() == 6, "F07_seis_muestras_antes_del_seguimiento");
            MensajeAsistenteDto[] historial = [new("user", pregunta),
                new("assistant", resumen.Mensaje, resumen.Referencias, resumen.Periodos)];
            var jsonWeb = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            historial = JsonSerializer.Deserialize<MensajeAsistenteDto[]>(JsonSerializer.Serialize(historial, jsonWeb), jsonWeb)!;
            Comprobar(historial[1].Periodos!.Single() == periodo, "F07_metadatos_sobreviven_serializacion_del_chat");
            reloj.Instante = instante.AddSeconds(25);
            var explicito = new ConsultaOperativaDto { Tipo = "Panel", Id = panel, Periodo = "Explicito",
                Desde = periodo.DesdeUtc.ToString("O"), Hasta = periodo.HastaUtc.ToString("O") };
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            IModeloConversacional modeloDetalle = geminiReal
                ? new GeminiConversacional(http, opciones, NullLogger<GeminiConversacional>.Instance)
                : new ModeloEscenario(turno =>
                {
                    if (turno.Intercambios.Count > 0) return new(CodigoAsistente.Exito, "Mediciones verificadas.", []);
                    Comprobar(turno.Instrucciones.Contains(JsonSerializer.Serialize(periodo.DesdeUtc))
                        && turno.Instrucciones.Contains(JsonSerializer.Serialize(periodo.HastaUtc)),
                        "F07_modelo_recibe_los_limites_sin_recortar_fracciones");
                    return new(CodigoAsistente.Exito, null, [Llamada("ConsultarHistorial", explicito)]);
                });
            var detalle = await new AsistenteService(usuario, modeloDetalle, observado, periodos, opciones, formato)
                .PreguntarAsync(new("Mostrame las mediciones utilizadas para ese promedio, conservando exactamente su período.",
                    "mod009-sintetica", historial, null), default);
            Comprobar(detalle.Codigo == CodigoAsistente.Exito, "F07_seguimiento_exitoso");
            var consultasDetalle = observado.Resultados.Where(r => r.Herramienta == "ConsultarHistorial").ToArray();
            Comprobar(consultasDetalle.Length > 0, "F07_detalle_vuelve_a_consultar_MCP");
            Comprobar(consultasDetalle.All(r => r.Resultado.Periodo?.DesdeUtc == periodo.DesdeUtc
                && r.Resultado.Periodo.HastaUtc == periodo.HastaUtc), "F07_detalle_no_desplaza_el_intervalo");
            Comprobar(consultasDetalle.Sum(r => JsonSerializer.SerializeToElement(r.Resultado.Datos)
                .GetProperty("Muestras").GetArrayLength()) == 6, "F07_detalle_conserva_las_seis_muestras");
            Comprobar(detalle.Periodos?.Any(p => p.DesdeUtc == periodo.DesdeUtc && p.HastaUtc == periodo.HastaUtc) == true,
                "F07_seguimiento_devuelve_la_referencia_original");
            var sinLlamadas = new ModeloEscenario(_ => throw new InvalidOperationException());
            var invalido = periodo with { HastaUtc = periodo.DesdeUtc };
            var rechazo = await new AsistenteService(usuario, sinLlamadas, observado, periodos, opciones, formato)
                .PreguntarAsync(new("Consulta", "mod009-sintetica",
                    [new("assistant", "Contexto", Periodos: [invalido])], null), default);
            Comprobar(rechazo.Codigo == CodigoAsistente.ArgumentosInvalidos && sinLlamadas.Llamadas == 0,
                "F07_historial_temporal_invalido_rechazado_antes_del_modelo");
        }
        catch (Exception error)
        {
            verificaciones.Add(new { Caso = "interrupcion", Correcto = false, Error = error.GetType().Name });
            Console.Error.WriteLine("Seguimiento aislado: " + error.GetType().Name + ". Sin detalles privados.");
            Environment.ExitCode = 1;
        }
        finally
        {
            Directory.CreateDirectory("artifacts/mod009");
            await File.WriteAllTextAsync("artifacts/mod009/seguimiento" + (geminiReal ? "-gemini" : "-local") + ".json",
                JsonSerializer.Serialize(new { Tipo = "auxiliar_MCP_MySQL_reales_reloj_identidad_controlados",
                    GeminiReal = geminiReal, AceptacionOidcReal = false, Resultados = verificaciones },
                    new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static LlamadaModeloDto Llamada(string herramienta, ConsultaOperativaDto consulta) =>
        new(Guid.NewGuid().ToString("N"), herramienta, JsonSerializer.Serialize(new { consulta }));

    private sealed class ClienteSeguimiento(IClienteHerramientasMcp original) : IClienteHerramientasMcp
    {
        public List<(string Herramienta, ResultadoOperativoDto Resultado)> Resultados { get; } = [];
        public async Task<IConexionHerramientasMcp> ConectarAsync(DateTimeOffset instanteUtc, CancellationToken cancellationToken) =>
            new Conexion(await original.ConectarAsync(instanteUtc, cancellationToken), Resultados);

        private sealed class Conexion(IConexionHerramientasMcp original,
            List<(string Herramienta, ResultadoOperativoDto Resultado)> resultados) : IConexionHerramientasMcp
        {
            public Task<IReadOnlyList<HerramientaModeloDto>> DescubrirAsync(CancellationToken cancellationToken) =>
                original.DescubrirAsync(cancellationToken);
            public async Task<ResultadoOperativoDto> InvocarAsync(LlamadaModeloDto llamada, CancellationToken cancellationToken)
            {
                var resultado = await original.InvocarAsync(llamada, cancellationToken);
                resultados.Add((llamada.Nombre, resultado));
                return resultado;
            }
            public ValueTask DisposeAsync() => original.DisposeAsync();
        }
    }
}
