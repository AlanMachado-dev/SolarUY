using System.IO.Pipelines;
using System.Text.Json;
using SolarUY.Application.Asistente;
using SolarUY.Infrastructure.Asistente;
using SolarUY.Presentation.Asistente;

static class ExportacionCatalogoService
{
    public static async Task ExportarAsync()
    {
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cliente = new ClienteHerramientasMcp((instante, token) =>
        {
            var a = new Pipe(); var b = new Pipe();
            IAsyncDisposable servidor = new ServidorAsistenteMcp(a.Reader.AsStream(), b.Writer.AsStream(),
                new ConsultaCatalogoService(), instante, token);
            return Task.FromResult((b.Reader.AsStream(), a.Writer.AsStream(), servidor));
        });
        await using var conexion = await cliente.ConectarAsync(DateTimeOffset.UtcNow, cancelacion.Token);
        var catalogo = await conexion.DescubrirAsync(cancelacion.Token);
        Directory.CreateDirectory("artifacts/req009");
        await File.WriteAllTextAsync("artifacts/req009/catalogo-mcp.json", JsonSerializer.Serialize(catalogo), cancelacion.Token);
        Console.WriteLine($"Catálogo MCP exportado: {catalogo.Count} herramientas.");
    }
}

sealed class ConsultaCatalogoService : IConsultaOperativaService
{
    public Task<ResultadoOperativoDto> EjecutarAsync(string herramienta, ConsultaOperativaDto consulta,
        DateTimeOffset instanteUtc, CancellationToken cancellationToken) =>
        Task.FromResult(new ResultadoOperativoDto(CodigoAsistente.AccesoDenegado, instanteUtc));
}
