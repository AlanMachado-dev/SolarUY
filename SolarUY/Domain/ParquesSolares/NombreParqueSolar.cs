using System.Text;

namespace SolarUY.Domain.ParquesSolares;

public sealed record NombreParqueSolar
{
    public string Valor { get; private set; }
    public string Clave { get; private set; }

    private NombreParqueSolar()
    {
        Valor = string.Empty;
        Clave = string.Empty;
    }

    public NombreParqueSolar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DatosParqueSolarInvalidosException("Nombre", "El nombre es obligatorio.");
        }

        Valor = nombre.Trim().Normalize(NormalizationForm.FormC);
        Clave = Valor.ToUpperInvariant();
        if (Valor.EnumerateRunes().Count() > 100 || Clave.EnumerateRunes().Count() > 100)
        {
            throw new DatosParqueSolarInvalidosException("Nombre",
                "El nombre del parque solar no puede superar los 100 caracteres.");
        }
    }
}
