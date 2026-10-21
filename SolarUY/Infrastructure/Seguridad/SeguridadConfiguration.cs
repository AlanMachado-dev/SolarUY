using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SolarUY.Application.ParquesSolares;

namespace SolarUY.Infrastructure.Seguridad;

public static class SeguridadConfiguration
{
    public static void AgregarAutenticacionExterna(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AutenticacionExternaOptions>()
            .Bind(configuration.GetSection("OpenIdConnect"))
            .Validate(options => Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
                && authority.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(authority.Host)
                && string.IsNullOrEmpty(authority.UserInfo) && string.IsNullOrEmpty(authority.Fragment)
                && EstaCompleto(options.Authority),
                "OpenIdConnect:Authority debe ser una URL HTTPS absoluta sin credenciales ni fragmentos.")
            .Validate(options => EstaCompleto(options.ClientId), "Configurá OpenIdConnect:ClientId.")
            .Validate(options => EstaCompleto(options.ClientSecret), "Configurá OpenIdConnect:ClientSecret en una fuente privada.")
            .Validate(options => EstaCompleto(options.RolesClaimType), "Configurá OpenIdConnect:RolesClaimType.")
            .ValidateOnStart();

        services.AddDataProtection().SetApplicationName("SolarUY");
        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioActualService, UsuarioActualService>();
        services.AddScoped<OpenIdConnectEventsHandler>();
        services.AddScoped<SesionCookieEventsHandler>();
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }).AddCookie(options =>
        {
            options.LoginPath = "/cuenta/iniciar-sesion";
            options.AccessDeniedPath = "/cuenta/acceso-denegado";
            options.Cookie.Name = "SolarUY.Sesion.Oidc";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.EventsType = typeof(SesionCookieEventsHandler);
        }).AddOpenIdConnect();

        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<AutenticacionExternaOptions>>((options, configuracion) =>
            {
                var externa = configuracion.Value;
                options.Authority = externa.Authority;
                options.ClientId = externa.ClientId;
                options.ClientSecret = externa.ClientSecret;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.SignedOutRedirectUri = "/";
                options.ResponseType = "code";
                options.UsePkce = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = externa.RolesClaimType;
                options.UseTokenLifetime = false;
                options.SaveTokens = true;
                options.EventsType = typeof(OpenIdConnectEventsHandler);
                // Se conservan validaciones criptográficas, nonce, correlación y PAR negociado.
            });
    }

    public static void EliminarCookieAnterior(HttpContext context) =>
        new ChunkingCookieManager().DeleteCookie(context, "SolarUY.Sesion",
            new CookieOptions { Path = "/", HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax });

    private static bool EstaCompleto(string? valor) => !string.IsNullOrWhiteSpace(valor)
        && !valor.Contains('<') && !valor.Contains('>');
}
