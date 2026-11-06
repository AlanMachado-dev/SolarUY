using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Presentation.Asistente;

static class GuardasMcpService
{
    public static async Task EjecutarAsync()
    {
        var opcionesDb = new DbContextOptionsBuilder<SolarUyDbContext>();
        PersistenciaConfiguration.ConfigurarContexto(opcionesDb, PreparacionEscenariosService.ObtenerConexion());
        var opciones = new AsistenteOptions();
        var usuario = new UsuarioGuardasMcp();
        var consultas = new ConsultaOperativaService(new ConsultaOperativaRepository(new ContextoFactory(opcionesDb.Options), opciones),
            usuario, new PeriodoConsultaService(TimeProvider.System, opciones), opciones);
        var cliente = new ClienteHerramientasMcp((instante, cancelacion) =>
        {
            var a = new Pipe(); var b = new Pipe();
            IAsyncDisposable servidor = new ServidorAsistenteMcp(a.Reader.AsStream(), b.Writer.AsStream(), consultas, instante, cancelacion);
            return Task.FromResult((b.Reader.AsStream(), a.Writer.AsStream(), servidor));
        });
        var resultados = new List<string>();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var conexion = await cliente.ConectarAsync(DateTimeOffset.UtcNow, limite.Token);
        await conexion.DescubrirAsync(limite.Token);
        async Task Comprobar(string caso, string argumentos, CodigoAsistente esperado)
        {
            var resultado = await conexion.InvocarAsync(new(Guid.NewGuid().ToString("N"), "ConsultarEstadoActual", argumentos), limite.Token);
            if (resultado.Codigo != esperado) throw new InvalidOperationException("Falló la guarda MCP " + caso);
            resultados.Add(caso + ": " + esperado);
        }
        foreach (var caso in new[] { "sin-contexto", "vencido", "otro-rol" })
        {
            usuario.Caso = caso;
            await Comprobar(caso, "{\"consulta\":{}}", CodigoAsistente.AccesoDenegado);
        }
        usuario.Caso = "autorizado";
        await Comprobar("argumentos-rol-fabricado", "{\"consulta\":{\"rol\":\"Administrador\"}}", CodigoAsistente.ArgumentosInvalidos);
        await Comprobar("argumentos-identidad-fabricada", "{\"consulta\":{\"usuarioId\":\"inventado\"}}", CodigoAsistente.ArgumentosInvalidos);
        await Comprobar("lista-ids-nula", "{\"consulta\":{\"Ids\":null}}", CodigoAsistente.ArgumentosInvalidos);
        await Comprobar("id-inexistente", JsonSerializer.Serialize(new { consulta = new { Tipo = "Panel", Id = Guid.NewGuid() } }), CodigoAsistente.NoEncontrado);
        var repository = new ConsultaOperativaRepository(new ContextoFactory(opcionesDb.Options), opciones);
        var candidatos = await repository.BuscarAsync(new() { Tipo = "Panel", Nombre = "Panel 3", TamanoPagina = 10 }, limite.Token);
        var consultaReferencias = new ConsultaOperativaDto { Tipo = "Panel", Ids = candidatos.Elementos.Select(p => p.Id).ToArray(), TamanoPagina = 200 };
        try
        {
            var directa = await consultas.EjecutarAsync("BuscarElementos", consultaReferencias, DateTimeOffset.UtcNow, limite.Token);
            Console.WriteLine("Referencias directas: " + directa.Codigo + "; total=" + directa.Total);
            var porMcp = await conexion.InvocarAsync(new(Guid.NewGuid().ToString("N"), "BuscarElementos", JsonSerializer.Serialize(new { consulta = consultaReferencias })), limite.Token);
            Console.WriteLine("Referencias por MCP: " + porMcp.Codigo + "; total=" + porMcp.Total);
            if (directa.Codigo != CodigoAsistente.Exito || porMcp.Codigo != CodigoAsistente.Exito || directa.Total != candidatos.Total || porMcp.Total != candidatos.Total)
                throw new InvalidOperationException("Revalidación de referencias incompleta.");
            resultados.Add("referencias-multiples: Exito");
            await Comprobar("estado-multiples-ids", JsonSerializer.Serialize(new { consulta = consultaReferencias }), CodigoAsistente.Exito);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Tipo depurado de fallo de referencias: " + error.GetBaseException().GetType().Name);
            foreach (var marco in new System.Diagnostics.StackTrace(error).GetFrames().Take(10))
                Console.Error.WriteLine(marco.GetMethod()?.DeclaringType?.FullName + "." + marco.GetMethod()?.Name);
            throw new InvalidOperationException("No se validaron las referencias múltiples.");
        }
        var opcionesConversacion = new AsistenteOptions { Habilitado = true, ClaveApi = "doble-funcional-no-credencial" };
        var usuarioConversacion = new UsuarioFuncional(DateTimeOffset.UtcNow);
        var modeloContexto = new ModeloGuionado((turno, _) =>
        {
            if (!turno.Instrucciones.Contains("Req009 Norte") || !turno.Instrucciones.Contains("Req009 Sur"))
                throw new InvalidOperationException("El modelo no recibió pertenencias revalidadas por MCP.");
            return Task.FromResult(new PasoModeloDto(CodigoAsistente.Exito, "Aclaración sintética de contextos", []));
        });
        var respuestaContexto = await new AsistenteService(usuarioConversacion, modeloContexto, cliente,
            new PeriodoConsultaService(TimeProvider.System, opcionesConversacion), opcionesConversacion).PreguntarAsync(
                new("¿Este panel?", "funcional-aislada", [new("assistant", "Contexto sintético anterior",
                    [new("Panel", candidatos.Elementos[0].Id)])], new("Panel", candidatos.Elementos[1].Id)), limite.Token);
        if (respuestaContexto.Codigo != CodigoAsistente.Exito) throw new InvalidOperationException("Contexto revalidado no completado.");
        resultados.Add("pertenencias-revalidadas-para-modelo: Exito");
        var sinLecturas = await repository.BuscarAsync(new() { Tipo = "Panel", Nombre = "Sin lecturas", TamanoPagina = 10 }, limite.Token);
        var estadoVacio = await conexion.InvocarAsync(new(Guid.NewGuid().ToString("N"), "ConsultarEstadoActual",
            JsonSerializer.Serialize(new { consulta = new ConsultaOperativaDto { Tipo = "Panel", Id = sinLecturas.Elementos[0].Id } })), limite.Token);
        var detalleVacio = JsonSerializer.SerializeToElement(estadoVacio.Datos).GetProperty("Detalle")[0];
        if (estadoVacio.Codigo != CodigoAsistente.SinDatos || detalleVacio.GetProperty("Ultima").ValueKind != JsonValueKind.Null
            || detalleVacio.GetProperty("Vigencia").GetString() != "SinMedicion" || detalleVacio.TryGetProperty("Resumen", out _))
            throw new InvalidOperationException("El estado sin medición no debe enviar ceros ni un resumen vacío.");
        resultados.Add("estado-sin-lectura-sin-ceros-inferidos: Exito");
        await VerificacionConversacion.EjecutarAsync(cliente, usuarioConversacion,
            new PeriodoConsultaService(TimeProvider.System, opcionesConversacion), opcionesConversacion);
        Directory.CreateDirectory("artifacts/req009");
        await File.WriteAllTextAsync("artifacts/req009/guardas-mcp.json", JsonSerializer.Serialize(new
        { FechaUtc = DateTimeOffset.UtcNow, Tipo = "mcp_real_contextos_sinteticos", Resultados = resultados }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("OK: MCP real rechaza contexto ausente/vencido, otro rol, identidad fabricada e ID inexistente. Contextos sintéticos identificados.");
    }
}

sealed class UsuarioGuardasMcp : IUsuarioActualService
{
    public string Caso { get; set; } = "sin-contexto";
    public bool EstaAutenticado => Caso != "sin-contexto";
    public bool PerteneceAlRol(string rol) => Caso != "otro-rol" && rol == "Operador";
    public Task<SesionAsistenteDto?> ObtenerSesionAsistenteAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SesionAsistenteDto?>(Caso == "sin-contexto" ? null : new("contexto-funcional-sintetico",
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMinutes(Caso == "vencido" ? -1 : 10)));
}
