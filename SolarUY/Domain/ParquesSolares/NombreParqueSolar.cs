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
    }
}
