namespace SolarUY.Infrastructure.Seguridad;

public static class RetornoLocal
{
    public static string Validar(string? destino)
    {
        if (string.IsNullOrWhiteSpace(destino) || destino.Length > 2048)
            return "/";

        // Se comprueba también la forma decodificada para excluir separadores y controles ocultos.
        var decodificado = destino;
        for (var intento = 0; intento < 3; intento++)
        {
            if (!EsSeguro(decodificado))
                return "/";
            var siguiente = Uri.UnescapeDataString(decodificado);
            if (siguiente == decodificado)
                return destino;
            decodificado = siguiente;
        }
        return "/";
    }

    private static bool EsSeguro(string destino)
    {
        if (!destino.StartsWith('/') || destino.StartsWith("//", StringComparison.Ordinal)
            || destino.Any(caracter => char.IsControl(caracter) || caracter == '\\'))
            return false;

        for (var indice = 0; indice < destino.Length; indice++)
        {
            if (destino[indice] != '%') continue;
            if (indice + 2 >= destino.Length || !Uri.IsHexDigit(destino[indice + 1])
                || !Uri.IsHexDigit(destino[indice + 2])) return false;
            indice += 2;
        }

        if (!Uri.TryCreate("https://retorno.invalid" + destino, UriKind.Absolute, out var uri)
            || uri.Host != "retorno.invalid") return false;
        var ruta = uri.AbsolutePath;
        return !ruta.StartsWith("/cuenta", StringComparison.OrdinalIgnoreCase)
            && !ruta.StartsWith("/signin-oidc", StringComparison.OrdinalIgnoreCase)
            && !ruta.StartsWith("/signout", StringComparison.OrdinalIgnoreCase);
    }
}
