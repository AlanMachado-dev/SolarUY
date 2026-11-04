namespace SolarUY.Application.Monitoreo;

public interface INotificacionMedicion
{
    Task PublicarAsync(LecturaPanelDto lectura);
}
