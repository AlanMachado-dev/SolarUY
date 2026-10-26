// Aceptación sobre SolarUY real. Reenvía la sesión OIDC recibida del navegador
// exclusivamente a https://localhost:7178; cookies y tokens solo viven en memoria.
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SolarUY.Domain.ParquesSolares;
using SolarUY.Infrastructure.Persistencia;

internal static class VerificarOidc
{
    public static async Task ServirAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("https://localhost:8186");
        builder.Logging.ClearProviders();
        builder.Services.AddAntiforgery(options => options.Cookie.Name = "SolarUY.Req004.Aceptacion.Antiforgery");
        var configuracion = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var conexion = new MySqlConnectionStringBuilder(PersistenciaConfiguration.ObtenerConexion(configuracion));
        conexion.Database = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync("artifacts/req004/destino-aislado.json"))
            .GetProperty("Base").GetString()!;
        if (!conexion.Database.StartsWith("solaruy_req004_", StringComparison.Ordinal)) throw new InvalidOperationException();
        await using var app = builder.Build();
        app.MapGet("/", (HttpContext contexto, IAntiforgery antiforgery) =>
        {
            var token = antiforgery.GetAndStoreTokens(contexto).RequestToken;
            var html = $"""
                <!doctype html><html lang="es"><meta charset="utf-8"><title>Aceptación OIDC req004</title>
                <h1>Aceptación real de req004</h1>
                <p>Solo se opera sobre el destino aislado. Se utiliza tu sesión de SolarUY en memoria; no se guardan cookies ni tokens.</p>
                <form method="post" action="/ejecutar/admin"><input type="hidden" name="__RequestVerificationToken" value="{WebUtility.HtmlEncode(token)}"><button>Verificar Administrador</button></form>
                <form method="post" action="/ejecutar/sin-rol"><input type="hidden" name="__RequestVerificationToken" value="{WebUtility.HtmlEncode(token)}"><button>Verificar usuario sin rol Administrador</button></form>
                <p><a href="https://localhost:7178/parques-solares">Volver a SolarUY</a></p></html>
                """;
            return Results.Content(html, "text/html; charset=utf-8");
        });
        app.MapPost("/ejecutar/{modo}", async (HttpContext contexto, IAntiforgery antiforgery, string modo) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(contexto);
                using var sesion = new Sesion(conexion.ConnectionString, contexto.Request.Headers.Cookie.ToString());
                if (modo == "admin") await sesion.VerificarAdminAsync();
                else if (modo == "sin-rol") await sesion.VerificarSinRolAsync();
                else return Results.BadRequest();
                await File.WriteAllTextAsync($"artifacts/req004/oidc-{modo}.json", JsonSerializer.Serialize(sesion.Evidencia));
                return Results.Content($"<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><title>Aceptación req004 correcta</title><h1>Verificaciones correctas</h1><p>Sesión OIDC real: {WebUtility.HtmlEncode(modo)}. Comprobaciones: {sesion.Evidencia.Count}.</p><p><a href=\"https://localhost:7178/parques-solares\">Volver a SolarUY</a></p></html>", "text/html; charset=utf-8");
            }
            catch (Exception error)
            {
                // Solo tipo y origen; nunca mensajes del proveedor ni valores de sesión.
                var fallo = new { tipo = error.GetBaseException().GetType().Name,
                    codigo = error.GetBaseException() is MySqlException mysql ? mysql.Number : 0,
                    criterio = error is CriterioFallidoException criterio ? criterio.Message : null,
                    origen = error.TargetSite?.DeclaringType?.Name + "." + error.TargetSite?.Name };
                await File.WriteAllTextAsync("artifacts/req004/oidc-fallo.json", JsonSerializer.Serialize(fallo));
                return Results.Content("<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><h1>Verificación detenida</h1><p>Se guardó un diagnóstico depurado.</p><a href=\"/\">Volver</a></html>", "text/html; charset=utf-8");
            }
        });
        Console.WriteLine("Aceptación OIDC preparada en https://localhost:8186");
        await app.RunAsync();
    }

    private sealed class Sesion : IDisposable
    {
        private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { BaseAddress = new Uri("https://localhost:7178") };
        private readonly string _conexion;
        public List<string> Evidencia { get; } = [];

        public Sesion(string conexion, string cookies)
        {
            _conexion = conexion;
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookies);
        }

        private SolarUyDbContext Contexto()
        {
            var opciones = new DbContextOptionsBuilder<SolarUyDbContext>();
            PersistenciaConfiguration.ConfigurarContexto(opciones, _conexion);
            return new(opciones.Options);
        }

        private void Exigir(bool condicion, string criterio)
        {
            if (!condicion) throw new CriterioFallidoException(criterio);
            Evidencia.Add(criterio);
            // Criterios constantes, sin datos de usuario, HTML, tokens ni cookies.
            File.WriteAllText("artifacts/req004/oidc-progreso.json", JsonSerializer.Serialize(Evidencia));
        }

        public async Task VerificarAdminAsync()
        {
            var listado = await PaginaAsync("/parques-solares");
            Exigir(Regex.IsMatch(listado, "<span[^>]*>Administrador\\s*</span>"), "sesion-oidc-administrador");
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var parque = await CrearParqueAsync("OIDC req004 A " + sufijo);
            var otro = await CrearParqueAsync("OIDC req004 B " + sufijo);
            await File.WriteAllTextAsync("artifacts/req004/oidc-parques.json", JsonSerializer.Serialize(new { Parque = parque.ID, Otro = otro.ID }));
            var ruta = Ruta(parque.ID);
            var detalle = await PaginaAsync($"/parques-solares/{parque.ID:D}");
            Exigir(WebUtility.HtmlDecode(detalle).Contains("+ Agregar nodo") && detalle.Contains(ruta), "boton-detalle-correcto");
            var formulario = await PaginaAsync(ruta);
            Exigir(formulario.Contains("<legend>Nodo</legend>") && formulario.Contains("<legend>Inversor</legend>")
                && formulario.Contains("<legend>Sensor</legend>") && formulario.Contains(parque.ID.ToString("D")), "formulario-tres-secciones-y-parque");
            var alta = await EnviarAsync(ruta, Datos(), formulario);
            Exigir(alta.Estado == 200 && alta.Html.Contains("Nodo registrado"), "alta-endpoint-vacio-confirmada");
            var guardado = (await LeerAsync(parque.ID)).Nodos.Single();
            Exigir(guardado.CantidadPaneles == 0 && guardado.Inversor.Sensor.Endpoint is null
                && guardado.Latitud == 0 && guardado.Longitud == 0, "alta-cero-y-endpoint-null");
            var ids = new[] { guardado.ID, guardado.Inversor.ID, guardado.Inversor.Sensor.ID };
            Exigir(ids.Distinct().Count() == 3 && !ids.Contains(Guid.Empty)
                && ids.All(id => alta.Html.Contains(id.ToString("D"))), "ids-generados-confirmacion-y-base");
            var datos = Datos(); datos["Modelo.EndpointSensor"] = "  texto sin protocolo req004  ";
            Exigir((await EnviarAsync(ruta, datos)).Html.Contains("Nodo registrado"), "alta-endpoint-informado");
            Exigir((await LeerAsync(parque.ID)).Nodos.Any(n => n.Inversor.Sensor.Endpoint == "texto sin protocolo req004"), "endpoint-conservado-recortado");
            Exigir((await LeerAsync(parque.ID)).Nodos.Count == 2, "nombres-y-series-repetidos-permitidos");

            var campos = new Dictionary<string, string>
            {
                ["Nombre"] = "nombre-nodo-error", ["Latitud"] = "latitud-error", ["Longitud"] = "longitud-error",
                ["NombreInversor"] = "nombre-inversor-error", ["NumeroSerieInversor"] = "serie-inversor-error",
                ["NombreSensor"] = "nombre-sensor-error", ["NumeroSerieSensor"] = "serie-sensor-error"
            };
            foreach (var campo in campos)
            foreach (var vacio in new string?[] { null, "", "   " })
            {
                datos = Datos();
                if (vacio is null) datos.Remove("Modelo." + campo.Key); else datos["Modelo." + campo.Key] = vacio;
                var rechazo = await EnviarAsync(ruta, datos);
                var errorCampo = Regex.Match(rechazo.Html, "<div id=\"" + campo.Value + "\">(.*?)</div>", RegexOptions.Singleline).Value;
                Exigir(rechazo.Estado == 200 && !rechazo.Html.Contains("Nodo registrado") && errorCampo.Contains("validation-message"),
                    "obligatorio-" + campo.Key + "-" + (vacio is null ? "ausente" : vacio.Length == 0 ? "vacio" : "espacios"));
                var conservado = campo.Key == "NombreSensor" ? "Inversor OIDC req004" : "Sensor OIDC req004";
                Exigir(rechazo.Html.Contains("value=\"" + conservado + "\""), "conservacion-" + campo.Key);
            }
            foreach (var campo in new[] { "Latitud", "Longitud" })
            foreach (var valor in new[] { "no-numero", "NaN", "Infinity", "-Infinity", campo == "Latitud" ? "90.01" : "180.01", campo == "Latitud" ? "-90.01" : "-180.01" })
            {
                datos = Datos(); datos["Modelo." + campo] = valor;
                var rechazo = await EnviarAsync(ruta, datos);
                Exigir(rechazo.Estado == 200 && !rechazo.Html.Contains("Nodo registrado") && rechazo.Html.Contains("validation-message"), "coordenada-invalida-" + campo);
            }
            Exigir((await LeerAsync(parque.ID)).Nodos.Count == 2, "invalidos-sin-escrituras");
            foreach (var limite in new[] { ("-90", "-180"), ("90", "180") })
            {
                datos = Datos(); datos["Modelo.Latitud"] = limite.Item1; datos["Modelo.Longitud"] = limite.Item2;
                Exigir((await EnviarAsync(ruta, datos)).Html.Contains("Nodo registrado"), "limites-geograficos-validos");
            }
            var sinToken = await EnviarAsync(ruta, Datos(), incluirToken: false);
            Exigir(sinToken.Estado == 400 && (await LeerAsync(parque.ID)).Nodos.Count == 4, "post-sin-antifalsificacion-rechazado");
            foreach (var id in new[] { Guid.Empty, Guid.NewGuid() })
            {
                var inexistente = await PaginaAsync(Ruta(id));
                Exigir(inexistente.Contains("No se encontr") && !inexistente.Contains("<legend>Nodo"), "get-parque-inexistente");
                var post = await EnviarAsync(Ruta(id), Datos(), await PaginaAsync(ruta));
                Exigir(post.Estado == 400 || post.Html.Contains("No se encontr"), "post-parque-inexistente");
            }
            Exigir((int)(await _http.GetAsync("/parques-solares/no-guid/nodos/nuevo")).StatusCode == 404, "guid-malformado");
            datos = Datos(); datos["Modelo.ParqueId"] = parque.ID.ToString("D"); datos["ParqueId"] = parque.ID.ToString("D");
            Exigir((await EnviarAsync(Ruta(otro.ID), datos)).Html.Contains("Nodo registrado")
                && (await LeerAsync(otro.ID)).Nodos.Count == 1 && (await LeerAsync(parque.ID)).Nodos.Count == 4, "destino-de-ruta-no-del-post");
            var antes = (await LeerAsync(parque.ID)).NodosIds.Order().ToArray();
            var concurrentes = await Task.WhenAll(EnviarAsync(ruta, Datos()), EnviarAsync(ruta, Datos()));
            var despues = await LeerAsync(parque.ID);
            Exigir(concurrentes.All(r => r.Html.Contains("Nodo registrado")) && despues.Nodos.Count == 6
                && antes.All(id => despues.NodosIds.Contains(id)), "dos-solicitudes-concurrentes-conservadas");
            Exigir((await LeerAsync(otro.ID)).Nodos.Count == 1 && despues.CapacidadTotalEstimadaMw == 0m,
                "otros-parques-sin-efectos-y-capacidad-cero");
            await VerificarFalloAsync(parque.ID);
            await VerificarDesaparicionAsync(sufijo);
            // Control anónimo independiente: no recibe ninguna cookie.
            using var anonimo = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            var getAnonimo = await anonimo.GetAsync("https://localhost:7178" + ruta);
            var postAnonimo = await anonimo.PostAsync("https://localhost:7178" + ruta, new FormUrlEncodedContent(Datos()));
            Exigir(getAnonimo.StatusCode == HttpStatusCode.Redirect && postAnonimo.StatusCode == HttpStatusCode.Redirect
                && getAnonimo.Headers.Location!.AbsolutePath == "/cuenta/iniciar-sesion"
                && postAnonimo.Headers.Location!.AbsolutePath == "/cuenta/iniciar-sesion", "anonimo-get-y-post-exigen-sesion");
            Exigir((await LeerAsync(parque.ID)).Nodos.Count == 7, "anonimo-sin-escrituras");
        }

        public async Task VerificarSinRolAsync()
        {
            var listado = await PaginaAsync("/parques-solares");
            Exigir(!Regex.IsMatch(listado, "<span[^>]*>Administrador\\s*</span>") && WebUtility.HtmlDecode(listado).Contains("Sesión iniciada:"), "sesion-oidc-sin-administrador");
            var parqueId = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync("artifacts/req004/oidc-parques.json")).GetProperty("Parque").GetGuid();
            var antes = (await LeerAsync(parqueId)).Nodos.Count;
            Exigir(!WebUtility.HtmlDecode(await PaginaAsync($"/parques-solares/{parqueId:D}")).Contains("+ Agregar nodo"), "boton-ausente-sin-administrador");
            var get = await _http.GetAsync(Ruta(parqueId));
            Exigir(get.StatusCode == HttpStatusCode.Redirect && get.Headers.Location!.AbsolutePath == "/cuenta/acceso-denegado", "get-directo-denegado");
            // El token viene de un formulario real de la misma identidad OIDC, no del Administrador.
            var post = await EnviarAsync(Ruta(parqueId), Datos(), listado);
            Exigir(post.Estado == 302 && post.Ubicacion == "/cuenta/acceso-denegado", "post-denegado-con-antifalsificacion-valida");
            Exigir((await LeerAsync(parqueId)).Nodos.Count == antes, "sin-administrador-sin-escrituras");
        }

        private async Task VerificarFalloAsync(Guid parqueId)
        {
            await using var sql = new MySqlConnection(_conexion); await sql.OpenAsync();
            await EjecutarSqlAsync(sql, "CREATE TRIGGER req004_fallo_aceptacion BEFORE INSERT ON Sensores FOR EACH ROW BEGIN IF NEW.Nombre = 'Fallo controlado req004' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fallo_req004_controlado'; END IF; END");
            var datos = Datos(); datos["Modelo.NombreSensor"] = "Fallo controlado req004";
            try
            {
                var resultado = await EnviarAsync(Ruta(parqueId), datos);
                Exigir(resultado.Html.Contains("No se pudo registrar el nodo. Intent") && !resultado.Html.Contains("Nodo registrado"), "fallo-tecnico-mensaje-generico");
                Exigir((await LeerAsync(parqueId)).Nodos.Count == 6, "fallo-tecnico-sin-nodo-parcial");
                await using var contexto = Contexto();
                Exigir(!await contexto.Sensores.AnyAsync(s => s.Nombre == "Fallo controlado req004"
                        && s.Inversor!.Nodo!.Parque.ID == parqueId)
                    && await contexto.Inversores.CountAsync() == await contexto.Nodos.CountAsync()
                    && await contexto.Sensores.CountAsync() == await contexto.Nodos.CountAsync(), "fallo-tecnico-sin-equipos-huerfanos");
            }
            finally { await EjecutarSqlAsync(sql, "DROP TRIGGER IF EXISTS req004_fallo_aceptacion"); }
            Exigir((await EnviarAsync(Ruta(parqueId), datos)).Html.Contains("Nodo registrado"), "recuperacion-tras-fallo-tecnico");
        }

        private async Task VerificarDesaparicionAsync(string sufijo)
        {
            var parque = await CrearParqueAsync("OIDC req004 desaparicion " + sufijo);
            await using var controlador = new MySqlConnection(_conexion); await controlador.OpenAsync();
            await EjecutarSqlAsync(controlador, "SELECT GET_LOCK('req004_aceptacion_gate', 5)");
            await EjecutarSqlAsync(controlador, "CREATE TRIGGER req004_gate_aceptacion BEFORE INSERT ON Nodos FOR EACH ROW BEGIN IF NEW.Nombre = 'Desaparicion req004' THEN DO GET_LOCK('req004_aceptacion_gate', 20); DO RELEASE_LOCK('req004_aceptacion_gate'); END IF; END");
            Task<(int Estado, string Html, string? Ubicacion)>? pendiente = null;
            try
            {
                var datos = Datos(); datos["Modelo.Nombre"] = "Desaparicion req004";
                pendiente = EnviarAsync(Ruta(parque.ID), datos);
                var bloqueado = false;
                for (var intento = 0; intento < 100 && !bloqueado; intento++)
                {
                    await using var consultar = controlador.CreateCommand();
                    consultar.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB=@base AND STATE='User lock'";
                    consultar.Parameters.AddWithValue("@base", controlador.Database);
                    bloqueado = Convert.ToInt32(await consultar.ExecuteScalarAsync()) > 0;
                    if (!bloqueado) await Task.Delay(50);
                }
                Exigir(bloqueado, "alta-detenida-despues-de-lectura");
                await using (var eliminar = controlador.CreateCommand())
                {
                    eliminar.CommandText = "DELETE FROM ParquesSolares WHERE ID=@id";
                    eliminar.Parameters.AddWithValue("@id", parque.ID.ToString("D"));
                    Exigir(await eliminar.ExecuteNonQueryAsync() == 1, "eliminacion-concurrente-real");
                }
                await EjecutarSqlAsync(controlador, "DO RELEASE_LOCK('req004_aceptacion_gate')");
                var resultado = await pendiente;
                Exigir(resultado.Html.Contains("No se encontr") && !resultado.Html.Contains("Nodo registrado"), "desaparicion-concurrente-controlada");
                await using var contexto = Contexto();
                Exigir(!await contexto.Nodos.AnyAsync(n => n.Parque.ID == parque.ID)
                    && !await contexto.Nodos.AnyAsync(n => n.Nombre == "Desaparicion req004")
                    && await contexto.Inversores.CountAsync() == await contexto.Nodos.CountAsync()
                    && await contexto.Sensores.CountAsync() == await contexto.Nodos.CountAsync(), "desaparicion-sin-grafos-huerfanos");
            }
            finally
            {
                await EjecutarSqlAsync(controlador, "DO RELEASE_LOCK('req004_aceptacion_gate')");
                if (pendiente is not null) await pendiente;
                await EjecutarSqlAsync(controlador, "DROP TRIGGER IF EXISTS req004_gate_aceptacion");
            }
        }

        private async Task<ParqueSolar> CrearParqueAsync(string nombre)
        {
            var formulario = await PaginaAsync("/parques-solares/nuevo");
            var datos = new Dictionary<string, string> { ["_handler"] = "alta-parque-solar", ["Modelo.Nombre"] = nombre,
                ["Modelo.Ubicacion"] = "Destino aislado req004" };
            var resultado = await EnviarAsync("/parques-solares/nuevo", datos, formulario);
            Exigir(resultado.Html.Contains("Parque solar registrado"), "regresion-alta-parque");
            await using var contexto = Contexto();
            return await contexto.ParquesSolares.AsNoTracking().SingleAsync(p => p.Nombre.Valor == nombre);
        }

        private async Task<ParqueSolar> LeerAsync(Guid id)
        {
            await using var contexto = Contexto();
            return (await new ParqueSolarRepository(contexto).ObtenerPorIdAsync(id))!;
        }

        private static string Ruta(Guid id) => $"/parques-solares/{id:D}/nodos/nuevo";
        private static Dictionary<string, string> Datos() => new()
        {
            ["_handler"] = "alta-nodo", ["Modelo.Nombre"] = "Nodo OIDC req004", ["Modelo.Latitud"] = "0", ["Modelo.Longitud"] = "0",
            ["Modelo.NombreInversor"] = "Inversor OIDC req004", ["Modelo.NumeroSerieInversor"] = "I-OIDC",
            ["Modelo.NombreSensor"] = "Sensor OIDC req004", ["Modelo.NumeroSerieSensor"] = "S-OIDC", ["Modelo.EndpointSensor"] = ""
        };

        private async Task<string> PaginaAsync(string ruta)
        {
            using var respuesta = await _http.GetAsync(ruta);
            respuesta.EnsureSuccessStatusCode();
            return await respuesta.Content.ReadAsStringAsync();
        }

        private async Task<(int Estado, string Html, string? Ubicacion)> EnviarAsync(string ruta,
            Dictionary<string, string> datos, string? formulario = null, bool incluirToken = true)
        {
            if (incluirToken)
            {
                formulario ??= await PaginaAsync(ruta);
                datos["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(formulario,
                    "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            }
            using var respuesta = await _http.PostAsync(ruta, new FormUrlEncodedContent(datos));
            return ((int)respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync(), respuesta.Headers.Location?.AbsolutePath);
        }

        private static async Task EjecutarSqlAsync(MySqlConnection conexion, string sql)
        {
            await using var comando = conexion.CreateCommand(); comando.CommandText = sql;
            await comando.ExecuteNonQueryAsync();
        }
        public void Dispose() => _http.Dispose();
    }

    private sealed class CriterioFallidoException(string criterio) : Exception(criterio);
}
