using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Paneles;

namespace SolarUY.Application.Asistente;

public sealed class ConsultaOperativaService(IConsultaOperativaRepository repository,
    IUsuarioActualService usuario, PeriodoConsultaService periodos, AsistenteOptions opciones) : IConsultaOperativaService
{
    private readonly IConsultaOperativaRepository _repository = repository;
    private readonly IUsuarioActualService _usuario = usuario;
    private readonly PeriodoConsultaService _periodos = periodos;
    private readonly AsistenteOptions _opciones = opciones;
    private (string Clave, PeriodoConsultaDto Periodo, InstantaneaConsultaDto Datos)? _ultimoResumen;

    public async Task<ResultadoOperativoDto> EjecutarAsync(string herramienta, ConsultaOperativaDto consulta,
        DateTimeOffset instanteUtc, CancellationToken cancellationToken)
    {
        if (!await EstaAutorizadoAsync(cancellationToken))
            return new(CodigoAsistente.AccesoDenegado, instanteUtc, Mensaje: "La sesión no autoriza esta consulta o venció.");
        try
        {
            Validar(herramienta, consulta);
            consulta = consulta with { TamanoPagina = consulta.TamanoPagina ?? _opciones.ResultadosPagina };
            if (herramienta is "BuscarElementos" or "ObtenerDetalle")
                return await BuscarAsync(herramienta, consulta, instanteUtc, cancellationToken);

            var historico = herramienta != "ConsultarEstadoActual";
            var comparacion = herramienta == "CompararMediciones";
            var periodo = historico ? _periodos.Resolver(consulta.Periodo, consulta.Desde, consulta.Hasta,
                instanteUtc, comparacion, consulta.DiasCompletos) : null;
            var referencia = comparacion ? _periodos.Resolver(consulta.PeriodoReferencia,
                consulta.DesdeReferencia, consulta.HastaReferencia, instanteUtc, true, consulta.DiasCompletos) : null;
            var datos = await _repository.LeerAsync(consulta, instanteUtc, periodo, referencia,
                herramienta == "ConsultarHistorial", cancellationToken);
            if (periodo?.EnCurso == true)
                datos = datos with { Series = datos.Series.Select(serie => serie with
                    { Cobertura = serie.Cobertura with { Completa = false } }).ToArray() };
            if (referencia?.EnCurso == true)
                datos = datos with { Referencia = datos.Referencia.Select(serie => serie with
                    { Cobertura = serie.Cobertura with { Completa = false } }).ToArray() };
            if (consulta.ParqueId.HasValue && !datos.Elementos.Any(elemento => elemento.Tipo == "Parque" && elemento.Id == consulta.ParqueId)
                || consulta.NodoId.HasValue && !datos.Elementos.Any(elemento => elemento.Tipo == "Nodo" && elemento.Id == consulta.NodoId))
                return new(CodigoAsistente.NoEncontrado, instanteUtc, Mensaje: "No existe el padre indicado en el ámbito consultado.");
            if (consulta.Id.HasValue && !datos.Elementos.Any(elemento => elemento.Tipo == consulta.Tipo && elemento.Id == consulta.Id))
                return new(CodigoAsistente.NoEncontrado, instanteUtc, Mensaje: "El elemento o su pertenencia no existe.");
            if (consulta.Ids.Any(id => !datos.Elementos.Any(elemento => elemento.Tipo == consulta.Tipo && elemento.Id == id)))
                return new(CodigoAsistente.NoEncontrado, instanteUtc, Mensaje: "Una referencia ya no existe en el ámbito consultado. Aclará los elementos.");
            if (herramienta == "ResumirMediciones")
            {
                var clave = System.Text.Json.JsonSerializer.Serialize(new { consulta.Tipo, consulta.Id,
                    Ids = consulta.Ids.Order().ToArray(), consulta.ParqueId, consulta.NodoId, consulta.Estado, consulta.Agrupacion });
                if (consulta.ComponerConAnterior)
                {
                    if (_ultimoResumen is not { } previo || previo.Clave != clave || previo.Periodo.HastaUtc != periodo!.DesdeUtc)
                        return new(CodigoAsistente.ArgumentosInvalidos, instanteUtc, Mensaje: "Solo se componen tramos contiguos del mismo ámbito, filtro y agrupación dentro del turno actual.");
                    periodo = periodo! with { DesdeUtc = previo.Periodo.DesdeUtc, DesdeLocal = previo.Periodo.DesdeLocal,
                        Descripcion = "Composición de tramos contiguos, cada invocación dentro del límite de días" };
                    datos = ComposicionResumenService.Componer(previo.Datos, datos, periodo, instanteUtc, _opciones.HuecoSegundos);
                }
                _ultimoResumen = (clave, periodo!, datos);
            }
            return herramienta switch
            {
                "ConsultarEstadoActual" => Estado(datos, consulta, instanteUtc),
                "ConsultarHistorial" => new(datos.Muestras.Count == 0 ? CodigoAsistente.SinDatos : CodigoAsistente.Exito,
                    instanteUtc, new { datos.Muestras, Cobertura = datos.Series.Select(serie => new { serie.Panel, serie.Cobertura }) },
                    Cursor: datos.Continuacion, Periodo: periodo),
                "ResumirMediciones" => Resumir(datos, consulta, instanteUtc, periodo!),
                "CompararMediciones" => Comparar(datos, consulta, instanteUtc, periodo!, referencia!),
                _ => new(CodigoAsistente.ArgumentosInvalidos, instanteUtc)
            };
        }
        catch (ArgumentException error)
        {
            return new(CodigoAsistente.ArgumentosInvalidos, instanteUtc, Mensaje: error.Message);
        }
        catch (DependenciaAsistenteException error)
        {
            return new(error.Codigo, instanteUtc, Mensaje: "No se pudo consultar la dependencia. Reintentá manualmente.");
        }
    }

    private async Task<bool> EstaAutorizadoAsync(CancellationToken cancellationToken)
    {
        var sesion = await _usuario.ObtenerSesionAsistenteAsync(cancellationToken);
        return _usuario.EstaAutenticado && (_usuario.PerteneceAlRol("Administrador") || _usuario.PerteneceAlRol("Operador"))
            && sesion is not null && sesion.EmisionUtc <= _periodos.Ahora && sesion.VencimientoUtc > _periodos.Ahora;
    }

    private void Validar(string herramienta, ConsultaOperativaDto consulta)
    {
        if (!CatalogoAsistente.Nombres.Contains(herramienta)) throw new ArgumentException("Herramienta fuera del catálogo de solo lectura.");
        if (consulta.ComponerConAnterior && herramienta != "ResumirMediciones") throw new ArgumentException("La composición se admite en ResumirMediciones.");
        if (consulta.Nombre is not null && herramienta != "BuscarElementos") throw new ArgumentException("Primero buscá el nombre y resolvé el elemento por su ID.");
        if (!new[] { "Global", "Parque", "Nodo", "Panel" }.Contains(consulta.Tipo)) throw new ArgumentException("Tipo inválido.");
        if (!new[] { "Global", "Parque", "Nodo", "Panel" }.Contains(consulta.Agrupacion)) throw new ArgumentException("Agrupación inválida.");
        if (consulta.Id == Guid.Empty || consulta.ParqueId == Guid.Empty || consulta.NodoId == Guid.Empty
            || consulta.Ids is null || consulta.Ids.Any(id => id == Guid.Empty) || consulta.Ids.Distinct().Count() != consulta.Ids.Length)
            throw new ArgumentException("Identificadores inválidos o repetidos.");
        if (consulta.Tipo == "Global" && (consulta.Id.HasValue || consulta.Ids.Length > 0)) throw new ArgumentException("Global no recibe IDs explícitos.");
        if (consulta.Id.HasValue && consulta.Ids.Length > 0) throw new ArgumentException("Usá Id o Ids.");
        if (consulta.Ids.Length > _opciones.MaximoIds) throw new ArgumentException($"Máximo {_opciones.MaximoIds} IDs explícitos. Un ámbito Global no tiene ese límite.");
        if (consulta.Pagina < 1 || consulta.Pagina > int.MaxValue / Math.Max(1, _opciones.MaximoPagina)
            || consulta.TamanoPagina is <= 0 || consulta.TamanoPagina > _opciones.MaximoPagina)
            throw new ArgumentException($"Tamaño de página entre 1 y {_opciones.MaximoPagina}.");
        if (consulta.Nombre?.Length > 200) throw new ArgumentException("Nombre demasiado extenso.");
        if (consulta.Estado is not null && !Enum.GetNames<EstadoPanel>().Contains(consulta.Estado))
            throw new ArgumentException("Estado inválido.");
        if (consulta.Orden is not ("Ascendente" or "Descendente") || consulta.Cambio is not (null or "Aumento" or "Disminucion" or "Igualdad"))
            throw new ArgumentException("Orden o cambio inválido.");
        if (herramienta == "BuscarElementos" && consulta.Tipo == "Global") throw new ArgumentException("Indicá Parque, Nodo o Panel para buscar.");
        if (herramienta == "ObtenerDetalle" && (!consulta.Id.HasValue || consulta.Tipo == "Global")) throw new ArgumentException("El detalle necesita tipo e ID inequívocos.");
        if (herramienta == "ConsultarHistorial" && (!consulta.Id.HasValue || consulta.Tipo == "Global")) throw new ArgumentException("El historial necesita un elemento inequívoco.");
        if (consulta.Cursor is { Id: var cursorId } && cursorId == Guid.Empty) throw new ArgumentException("Cursor inválido.");
    }

    private async Task<ResultadoOperativoDto> BuscarAsync(string herramienta, ConsultaOperativaDto consulta,
        DateTimeOffset instanteUtc, CancellationToken cancellationToken)
    {
        var pagina = await _repository.BuscarAsync(herramienta == "ObtenerDetalle"
            ? consulta with { Pagina = 1, TamanoPagina = 1 } : consulta, cancellationToken);
        if (herramienta == "ObtenerDetalle" && pagina.Total > 0)
        {
            var elemento = pagina.Elementos[0];
            var tipoHijo = consulta.Tipo switch { "Parque" => "Nodo", "Nodo" => "Panel", _ => null };
            var hijos = tipoHijo is null ? new PaginaElementosDto([], 0) : await _repository.BuscarAsync(
                new ConsultaOperativaDto { Tipo = tipoHijo, ParqueId = elemento.ParqueId,
                    NodoId = consulta.Tipo == "Nodo" ? elemento.Id : null, Pagina = consulta.Pagina, TamanoPagina = consulta.TamanoPagina }, cancellationToken);
            return new(CodigoAsistente.Exito, instanteUtc,
                new { Elemento = elemento, TipoRelacion = tipoHijo, Relaciones = hijos.Elementos, TotalRelaciones = hijos.Total },
                Total: hijos.Total, PaginaSiguiente: Siguiente(consulta, hijos.Total));
        }
        return new(pagina.Total == 0 ? herramienta == "ObtenerDetalle" ? CodigoAsistente.NoEncontrado : CodigoAsistente.SinDatos
            : CodigoAsistente.Exito, instanteUtc, pagina.Elementos,
            Mensaje: "Los nombres pueden repetirse. Mostrá candidatos con ID y pertenencia y pedí aclaración; no elijas un homónimo.",
            Total: pagina.Total, PaginaSiguiente: Siguiente(consulta, pagina.Total));
    }

    private ResultadoOperativoDto Estado(InstantaneaConsultaDto datos, ConsultaOperativaDto consulta, DateTimeOffset instante)
    {
        var total = EstadoActualObservado.Calcular(datos.Series.Select(serie => serie.Ultima is { } muestra
            ? new LecturaObservada(muestra.FechaUtc, muestra.PotenciaW, muestra.Estado) : null),
            instante, TimeSpan.FromSeconds(_opciones.VigenciaSegundos));
        var filtrados = datos.Series.Where(serie => consulta.Estado is null || serie.Ultima?.Estado.ToString() == consulta.Estado).ToArray();
        var detalle = Paginar(filtrados, consulta).Select(serie =>
        {
            var lectura = serie.Ultima is { } muestra ? new LecturaObservada(muestra.FechaUtc, muestra.PotenciaW, muestra.Estado) : null;
            var estadoPanel = EstadoActualObservado.Calcular([lectura], instante, TimeSpan.FromSeconds(_opciones.VigenciaSegundos));
            return new EstadoPanelConsultaDto(serie.Panel, serie.Ultima,
                estadoPanel.PanelesSinMedicion == 1 ? "SinMedicion" : estadoPanel.PanelesAntiguos == 1 ? "Antigua" : "Vigente");
        }).ToArray();
        return new(datos.Series.All(serie => serie.Ultima is null) ? CodigoAsistente.SinDatos : CodigoAsistente.Exito,
            instante, new EstadoConsultaDto(total, detalle, filtrados.Length),
            Mensaje: "Los conteos y la potencia corresponden a todo el ámbito; el filtro de estado afecta el detalle. Estados antiguos conservan sus fechas.",
            Total: filtrados.Length, PaginaSiguiente: Siguiente(consulta, filtrados.Length));
    }

    private ResultadoOperativoDto Resumir(InstantaneaConsultaDto datos, ConsultaOperativaDto consulta,
        DateTimeOffset instante, PeriodoConsultaDto periodo)
    {
        var resumenes = Agrupar(datos, consulta.Agrupacion).Select(grupo => CrearResumen(grupo.Elemento, grupo.Series)).ToArray();
        // El agregado se calcula antes de paginar; las series detalladas no se envían al modelo.
        var salida = Paginar(resumenes, consulta).Select(resumen => new
        {
            resumen.Elemento, resumen.Potencia, resumen.Muestras, resumen.Estados,
            Cobertura = new { Paneles = resumen.Paneles.Count,
                Completos = resumen.Paneles.Count(panel => panel.Cobertura.Completa),
                PrimeraUtc = resumen.Paneles.Min(panel => panel.Cobertura.PrimeraUtc),
                UltimaUtc = resumen.Paneles.Max(panel => panel.Cobertura.UltimaUtc),
                MayorHuecoSegundos = resumen.Paneles.Select(panel => panel.Cobertura.MayorHuecoSegundos).DefaultIfEmpty(0).Max() }
        }).ToArray();
        return new(datos.Series.All(serie => serie.Resumen.Cantidad == 0) ? CodigoAsistente.SinDatos : CodigoAsistente.Exito,
            instante, salida, Total: resumenes.Length, PaginaSiguiente: Siguiente(consulta, resumenes.Length), Periodo: periodo);
    }

    private ResultadoOperativoDto Comparar(InstantaneaConsultaDto datos, ConsultaOperativaDto consulta,
        DateTimeOffset instante, PeriodoConsultaDto periodo, PeriodoConsultaDto referencia)
    {
        var comparaciones = Agrupar(datos, consulta.Agrupacion).Select(grupo =>
        {
            var actual = CrearResumen(grupo.Elemento, grupo.Series).Potencia;
            var ids = grupo.Series.Select(serie => serie.Panel.Id).ToHashSet();
            var anterior = CrearResumen(grupo.Elemento, datos.Referencia.Where(serie => ids.Contains(serie.Panel.Id)).ToArray()).Potencia;
            return new ComparacionElementoDto(grupo.Elemento, actual, anterior, actual.CompararCon(anterior));
        }).Where(item => consulta.Cambio is null || item.Comparacion.CambioObservado == consulta.Cambio).ToArray();
        var ordenados = consulta.Orden == "Ascendente"
            ? comparaciones.OrderBy(item => item.Comparacion.DiferenciaW).ThenBy(item => item.Elemento.Id).ToArray()
            : comparaciones.OrderByDescending(item => item.Comparacion.DiferenciaW).ThenBy(item => item.Elemento.Id).ToArray();
        return new(datos.Series.All(serie => serie.Resumen.Cantidad == 0) && datos.Referencia.All(serie => serie.Resumen.Cantidad == 0)
            ? CodigoAsistente.SinDatos : CodigoAsistente.Exito, instante, Paginar(ordenados, consulta),
            Mensaje: "El filtro usa CambioObservado de las muestras. Una comparación parcial mantiene Conclusion NoConcluyente: no acredita una disminución o aumento global ni un ganador.",
            Total: ordenados.Length, PaginaSiguiente: Siguiente(consulta, ordenados.Length), Periodo: periodo, Referencia: referencia);
    }

    private static ResumenElementoDto CrearResumen(ElementoOperativoDto elemento, IReadOnlyList<SeriePanelDto> series)
    {
        var potencia = PotenciaObservada.Agregar(series.Select(serie => serie.Resumen), series.Count, series.All(serie => serie.Cobertura.Completa));
        var muestras = series.Aggregate(ResumenMediciones.Vacio, (acumulado, serie) => acumulado.Componer(serie.Resumen));
        var estados = series.SelectMany(serie => serie.Estados).GroupBy(estado => estado.Estado)
            .Select(grupo => new ConteoEstadoObservado(grupo.Key, grupo.Sum(estado => estado.Cantidad),
                grupo.Min(estado => estado.PrimeraFechaUtc), grupo.Max(estado => estado.UltimaFechaUtc))).ToArray();
        return new(elemento, potencia, muestras, series, estados);
    }

    private static IEnumerable<(ElementoOperativoDto Elemento, IReadOnlyList<SeriePanelDto> Series)> Agrupar(
        InstantaneaConsultaDto datos, string agrupacion)
    {
        if (agrupacion == "Global")
        {
            yield return (new("Global", Guid.Empty, "Todo el ámbito autorizado", Guid.Empty, "Todos"), datos.Series);
            yield break;
        }
        foreach (var elemento in datos.Elementos.Where(elemento => elemento.Tipo == agrupacion).OrderBy(elemento => elemento.Id))
            yield return (elemento, datos.Series.Where(serie => agrupacion switch
            {
                "Panel" => serie.Panel.Id == elemento.Id,
                "Nodo" => serie.Panel.NodoId == elemento.Id,
                _ => serie.Panel.ParqueId == elemento.Id
            }).ToArray());
    }

    private static T[] Paginar<T>(IReadOnlyList<T> datos, ConsultaOperativaDto consulta) =>
        datos.Skip((consulta.Pagina - 1) * consulta.TamanoPagina!.Value).Take(consulta.TamanoPagina.Value).ToArray();
    private static int? Siguiente(ConsultaOperativaDto consulta, int total) =>
        (long)consulta.Pagina * consulta.TamanoPagina < total ? consulta.Pagina + 1 : null;
}
