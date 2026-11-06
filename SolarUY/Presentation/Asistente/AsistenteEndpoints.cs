using Microsoft.AspNetCore.Antiforgery;
using SolarUY.Application.Asistente;
using SolarUY.Application.ParquesSolares;

namespace SolarUY.Presentation.Asistente;

public static class AsistenteEndpoints
{
    public static void MapearAsistente(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/asistente").RequireAuthorization(politica =>
            politica.RequireAuthenticatedUser().RequireRole("Administrador", "Operador"));
        grupo.MapGet("/sesion", async (HttpContext contexto, IUsuarioActualService usuario,
            IAntiforgery antiforgery, AsistenteOptions opciones, TimeProvider reloj) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            var sesion = await usuario.ObtenerSesionAsistenteAsync(contexto.RequestAborted);
            if (sesion is null || sesion.EmisionUtc > reloj.GetUtcNow() || sesion.VencimientoUtc <= reloj.GetUtcNow()) return Results.Unauthorized();
            var token = antiforgery.GetAndStoreTokens(contexto);
            return Results.Ok(new { sesion.Marca, sesion.VencimientoUtc, Disponible = opciones.Disponible,
                Token = token.RequestToken, Cabecera = token.HeaderName ?? "RequestVerificationToken",
                Aviso = opciones.Disponible ? null : "El asistente no está configurado. Las demás funciones continúan disponibles." });
        });
        grupo.MapPost("/mensajes", async (HttpContext contexto, IAntiforgery antiforgery,
            IAsistenteService asistente) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            if (contexto.Request.ContentLength > 256_000) return Results.BadRequest(new { mensaje = "El historial es demasiado extenso." });
            var limite = contexto.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limite is { IsReadOnly: false }) limite.MaxRequestBodySize = 256_000;
            try { await antiforgery.ValidateRequestAsync(contexto); }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new { mensaje = "El formulario venció. Recargá la página." });
            }
            PreguntaAsistenteDto? pregunta;
            try { pregunta = await contexto.Request.ReadFromJsonAsync<PreguntaAsistenteDto>(contexto.RequestAborted); }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(new { mensaje = "La pregunta no tiene formato válido." }); }
            catch (BadHttpRequestException) { return Results.BadRequest(new { mensaje = "La solicitud supera el tamaño permitido." }); }
            if (pregunta is null) return Results.BadRequest();
            var resultado = await asistente.PreguntarAsync(pregunta, contexto.RequestAborted);
            if (resultado.Codigo == CodigoAsistente.AccesoDenegado) return Results.Json(resultado, statusCode: 403);
            return Results.Ok(resultado);
        });
    }
}
