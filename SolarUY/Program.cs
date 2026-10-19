using SolarUY.Client.Pages;
using SolarUY.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.ParquesSolares.Servicios;
using SolarUY.Infrastructure.Persistencia;
using SolarUY.Infrastructure.Seguridad;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
PersistenciaConfiguration.CargarConfiguracion(builder.Configuration,
    builder.Environment.ContentRootPath, builder.Environment.EnvironmentName, args);
// Los errores del conector pueden incluir SQL o datos del usuario en sus mensajes.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.None);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var conexion = PersistenciaConfiguration.ObtenerConexion(builder.Configuration);
builder.Services.AddDbContext<SolarUyDbContext>(options =>
    PersistenciaConfiguration.ConfigurarContexto(options, conexion));
builder.Services.AddScoped<IParqueSolarRepository, ParqueSolarRepository>();
builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActualService, UsuarioActualService>();
builder.Services.AddScoped<CredencialesService>();
builder.Services.AddScoped<IPasswordHasher<UsuarioConfigurado>, PasswordHasher<UsuarioConfigurado>>();
builder.Services.AddOptions<AutenticacionOptions>()
    .Bind(builder.Configuration.GetSection("Autenticacion"))
    .Validate(options => options.Usuarios.Count > 0
        && options.Usuarios.All(usuario => !string.IsNullOrWhiteSpace(usuario.Nombre)
            && !string.IsNullOrWhiteSpace(usuario.PasswordHash))
        && options.Usuarios.Select(usuario => usuario.Nombre).Distinct(StringComparer.Ordinal).Count()
            == options.Usuarios.Count,
        "Configurá identidades únicas y hashes de contraseña en Autenticacion:Usuarios.")
    .ValidateOnStart();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/cuenta/iniciar-sesion";
        options.AccessDeniedPath = "/cuenta/acceso-denegado";
        options.Cookie.Name = "SolarUY.Sesion";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("inicio-sesion", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapPost("/cuenta/autenticar", async (HttpContext context,
    IAntiforgery antiforgery, CredencialesService credenciales) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("El formulario venció. Volvé a iniciar sesión.");
    }

    var datos = await context.Request.ReadFormAsync(context.RequestAborted);
    var usuario = credenciales.Validar(datos["Nombre"].ToString(), datos["Password"].ToString());
    if (usuario is null)
    {
        return Results.LocalRedirect("/cuenta/iniciar-sesion?error=credenciales");
    }

    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, usuario);
    return Results.LocalRedirect("/parques-solares/nuevo");
}).RequireRateLimiting("inicio-sesion");

app.MapPost("/cuenta/cerrar-sesion", async (HttpContext context, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("El formulario venció. Recargá la página.");
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SolarUY.Client._Imports).Assembly);

app.Run();
