using System.Text;

namespace SolarUY.Domain.Paneles;

public sealed record NombrePanel
{
    public string Valor { get; private set; } = string.Empty;
    public string Clave { get; private set; } = string.Empty;

    private NombrePanel() { }

    public NombrePanel(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosPanelInvalidosException("Nombre", "El nombre es obligatorio.");

        Valor = nombre.Trim().Normalize(NormalizationForm.FormC);
        Clave = Valor.ToUpperInvariant();
        if (Valor.EnumerateRunes().Count() > 100 || Clave.EnumerateRunes().Count() > 100)
            throw new DatosPanelInvalidosException("Nombre",
                "El nombre del panel solar no puede superar los 100 caracteres.");
    }
}
