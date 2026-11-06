using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Asistente;
using SolarUY.Domain.Paneles;

namespace SolarUY.Infrastructure.Persistencia;

public sealed class ConsultaOperativaRepository(IDbContextFactory<SolarUyDbContext> contextFactory,
    AsistenteOptions opciones) : IConsultaOperativaRepository
{
    private readonly IDbContextFactory<SolarUyDbContext> _contextFactory = contextFactory;
    private readonly AsistenteOptions _opciones = opciones;

    public async Task<PaginaElementosDto> BuscarAsync(ConsultaOperativaDto consulta, CancellationToken cancellationToken)
    {
        try
        {
            await using var contexto = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var elementos = Proyectar(contexto, consulta.Tipo);
            elementos = FiltrarElementos(elementos, consulta);
            var total = await elementos.CountAsync(cancellationToken);
            var pagina = await elementos.OrderBy(elemento => elemento.Id)
                .Skip((consulta.Pagina - 1) * consulta.TamanoPagina!.Value)
                .Take(consulta.TamanoPagina.Value).ToArrayAsync(cancellationToken);
            return new(pagina, total);
        }
        catch (Exception error) when (EsFalloPersistencia(error))
        {
            throw new DependenciaAsistenteException(CodigoAsistente.FalloPersistencia);
        }
    }

    public async Task<InstantaneaConsultaDto> LeerAsync(ConsultaOperativaDto consulta, DateTimeOffset instanteUtc,
        PeriodoConsultaDto? periodo, PeriodoConsultaDto? referencia, bool historial, CancellationToken cancellationToken)
    {
        try
        {
            await using var contexto = await _contextFactory.CreateDbContextAsync(cancellationToken);
            // InnoDB: snapshot consistente de jerarquía, series y ambos intervalos sin bloquear escrituras.
            await using var transaccion = await contexto.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            var paneles = FiltrarPaneles(contexto, consulta);
            var elementos = await JerarquiaAsync(contexto, consulta, paneles, cancellationToken);
            var proyecciones = elementos.Where(elemento => elemento.Tipo == "Panel").ToArray();
            var muestras = contexto.Set<Medicion>().AsNoTracking()
                .Where(muestra => paneles.Select(panel => panel.ID).Contains(EF.Property<Guid>(muestra, "IdPanel")));
            var series = periodo is null
                ? await UltimasAsync(muestras, proyecciones, instanteUtc, cancellationToken)
                : await SeriesAsync(muestras, proyecciones, periodo, instanteUtc, consulta.Estado, cancellationToken);
            var anteriores = referencia is null ? [] :
                await SeriesAsync(muestras, proyecciones, referencia, instanteUtc, consulta.Estado, cancellationToken);
            MuestraOperativaDto[] pagina = [];
            CursorMedicionDto? continuacion = null;
            if (historial && periodo is not null)
            {
                var historicas = FiltrarPeriodo(muestras, periodo, instanteUtc, consulta.Estado);
                if (consulta.Cursor is { } cursor)
                {
                    var fecha = cursor.FechaUtc.UtcDateTime;
                    historicas = historicas.Where(muestra => muestra.FechaUtc > fecha ||
                        (muestra.FechaUtc == fecha && muestra.ID.CompareTo(cursor.Id) > 0));
                }
                var filas = await historicas.OrderBy(muestra => muestra.FechaUtc).ThenBy(muestra => muestra.ID)
                    .Take(consulta.TamanoPagina!.Value + 1)
                    .Select(muestra => new { muestra.ID, PanelId = EF.Property<Guid>(muestra, "IdPanel"), muestra.FechaUtc,
                        muestra.GeneracionActualW, muestra.Estado }).ToArrayAsync(cancellationToken);
                pagina = filas.Take(consulta.TamanoPagina.Value).Select(fila => new MuestraOperativaDto(
                    fila.ID, fila.PanelId, fila.FechaUtc, (decimal)fila.GeneracionActualW, fila.Estado)).ToArray();
                if (filas.Length > consulta.TamanoPagina.Value && pagina.Length > 0)
                    continuacion = new(pagina[^1].FechaUtc, pagina[^1].Id);
            }
            await transaccion.CommitAsync(cancellationToken);
            return new(elementos, series, anteriores, pagina, continuacion);
        }
        catch (Exception error) when (EsFalloPersistencia(error))
        {
            throw new DependenciaAsistenteException(CodigoAsistente.FalloPersistencia);
        }
    }

    private static bool EsFalloPersistencia(Exception error) =>
        error is DbException or DbUpdateException or InvalidOperationException or TimeoutException;

    private static IQueryable<ElementoOperativoDto> Proyectar(SolarUyDbContext contexto, string tipo) => tipo switch
    {
        "Parque" => contexto.ParquesSolares.AsNoTracking().Select(parque => new ElementoOperativoDto
        {
            Tipo = "Parque", Id = parque.ID, Nombre = parque.Nombre.Valor, ParqueId = parque.ID,
            Parque = parque.Nombre.Valor, Ubicacion = parque.Ubicacion
        }),
        "Nodo" => contexto.Nodos.AsNoTracking().Select(nodo => new ElementoOperativoDto
        {
            Tipo = "Nodo", Id = nodo.ID, Nombre = nodo.Nombre, ParqueId = nodo.Parque.ID,
            Parque = nodo.Parque.Nombre.Valor, NodoId = nodo.ID, Nodo = nodo.Nombre,
            Latitud = nodo.Latitud, Longitud = nodo.Longitud
        }),
        _ => contexto.Paneles.AsNoTracking().Select(panel => new ElementoOperativoDto
        {
            Tipo = "Panel", Id = panel.ID, Nombre = panel.Nombre.Valor, ParqueId = panel.Nodo.Parque.ID,
            Parque = panel.Nodo.Parque.Nombre.Valor, NodoId = panel.Nodo.ID, Nodo = panel.Nodo.Nombre,
            Modelo = panel.Modelo, PotenciaNominalW = panel.PotenciaNominalW, FechaInstalacion = panel.FechaInstalacion
        })
    };

    private static IQueryable<ElementoOperativoDto> FiltrarElementos(IQueryable<ElementoOperativoDto> elementos, ConsultaOperativaDto consulta)
    {
        if (consulta.Id.HasValue) elementos = elementos.Where(elemento => elemento.Id == consulta.Id);
        if (consulta.Ids.Length > 0) elementos = FiltrarIds(elementos, elemento => elemento.Id, consulta.Ids);
        if (!string.IsNullOrWhiteSpace(consulta.Nombre)) elementos = elementos.Where(elemento => elemento.Nombre.Contains(consulta.Nombre));
        if (consulta.ParqueId.HasValue) elementos = elementos.Where(elemento => elemento.ParqueId == consulta.ParqueId);
        if (consulta.NodoId.HasValue) elementos = elementos.Where(elemento => elemento.NodoId == consulta.NodoId);
        return elementos;
    }

    private static IQueryable<Panel> FiltrarPaneles(SolarUyDbContext contexto, ConsultaOperativaDto consulta)
    {
        var paneles = contexto.Paneles.AsNoTracking();
        if (consulta.ParqueId.HasValue) paneles = paneles.Where(panel => panel.Nodo.Parque.ID == consulta.ParqueId);
        if (consulta.NodoId.HasValue) paneles = paneles.Where(panel => panel.Nodo.ID == consulta.NodoId);
        var ids = consulta.Id.HasValue ? new[] { consulta.Id.Value } : consulta.Ids;
        if (ids.Length > 0) paneles = consulta.Tipo switch
        {
            "Parque" => FiltrarIds(paneles, panel => panel.Nodo.Parque.ID, ids),
            "Nodo" => FiltrarIds(paneles, panel => panel.Nodo.ID, ids),
            _ => FiltrarIds(paneles, panel => panel.ID, ids)
        };
        return paneles;
    }

    private static async Task<ElementoOperativoDto[]> JerarquiaAsync(SolarUyDbContext contexto, ConsultaOperativaDto consulta,
        IQueryable<Panel> paneles, CancellationToken cancellationToken)
    {
        var ids = consulta.Id.HasValue ? new[] { consulta.Id.Value } : consulta.Ids;
        var nodos = contexto.Nodos.AsNoTracking();
        if (consulta.ParqueId.HasValue) nodos = nodos.Where(nodo => nodo.Parque.ID == consulta.ParqueId);
        if (consulta.NodoId.HasValue) nodos = nodos.Where(nodo => nodo.ID == consulta.NodoId);
        if (ids.Length > 0) nodos = consulta.Tipo switch
        {
            "Parque" => FiltrarIds(nodos, nodo => nodo.Parque.ID, ids),
            "Nodo" => FiltrarIds(nodos, nodo => nodo.ID, ids),
            _ => nodos.Where(nodo => paneles.Select(panel => panel.Nodo.ID).Contains(nodo.ID))
        };
        var parques = Proyectar(contexto, "Parque");
        if (consulta.ParqueId.HasValue) parques = parques.Where(parque => parque.Id == consulta.ParqueId);
        if (consulta.Tipo == "Parque" && ids.Length > 0) parques = FiltrarIds(parques, parque => parque.Id, ids);
        else if (consulta.Tipo != "Global" || consulta.NodoId.HasValue)
            parques = parques.Where(parque => nodos.Select(nodo => nodo.Parque.ID).Contains(parque.Id));
        var resultado = new List<ElementoOperativoDto>();
        resultado.AddRange(await parques.ToArrayAsync(cancellationToken));
        resultado.AddRange(await Proyectar(contexto, "Nodo").Where(elemento => nodos.Select(nodo => nodo.ID).Contains(elemento.Id)).ToArrayAsync(cancellationToken));
        resultado.AddRange(await Proyectar(contexto, "Panel").Where(elemento => paneles.Select(panel => panel.ID).Contains(elemento.Id)).ToArrayAsync(cancellationToken));
        return resultado.ToArray();
    }

    private static IQueryable<T> FiltrarIds<T>(IQueryable<T> elementos, Expression<Func<T, Guid>> selector, Guid[] ids)
    {
        // El proveedor MySQL no asigna el mapeo char(36) al parámetro de una colección Guid.
        // Cada cierre conserva un parámetro escalar con su conversión; no se concatena SQL ni se incrustan IDs.
        Expression? predicado = null;
        foreach (var id in ids)
        {
            Expression<Func<Guid>> parametro = () => id;
            var igualdad = Expression.Equal(selector.Body, parametro.Body);
            predicado = predicado is null ? igualdad : Expression.OrElse(predicado, igualdad);
        }
        return predicado is null ? elementos : elementos.Where(Expression.Lambda<Func<T, bool>>(predicado, selector.Parameters));
    }

    private static IQueryable<Medicion> FiltrarPeriodo(IQueryable<Medicion> muestras, PeriodoConsultaDto periodo,
        DateTimeOffset instante, string? estado)
    {
        var desde = periodo.DesdeUtc.UtcDateTime;
        var hasta = periodo.HastaUtc.UtcDateTime;
        var limite = instante.UtcDateTime;
        var filtradas = muestras.Where(muestra => muestra.FechaUtc >= desde && muestra.FechaUtc < hasta && muestra.FechaUtc <= limite);
        if (estado is not null)
        {
            var filtro = Enum.Parse<EstadoPanel>(estado);
            filtradas = filtradas.Where(muestra => muestra.Estado == filtro);
        }
        return filtradas;
    }

    private static async Task<SeriePanelDto[]> UltimasAsync(IQueryable<Medicion> muestras, ElementoOperativoDto[] paneles,
        DateTimeOffset instante, CancellationToken cancellationToken)
    {
        var limite = instante.UtcDateTime;
        var vigentes = muestras.Where(muestra => muestra.FechaUtc <= limite);
        var ultimas = await vigentes.Where(muestra => muestra.ID == vigentes
            .Where(otra => EF.Property<Guid>(otra, "IdPanel") == EF.Property<Guid>(muestra, "IdPanel"))
            .OrderByDescending(otra => otra.FechaUtc).ThenByDescending(otra => otra.ID).Select(otra => otra.ID).FirstOrDefault())
            .Select(muestra => new { muestra.ID, PanelId = EF.Property<Guid>(muestra, "IdPanel"), muestra.FechaUtc,
                muestra.GeneracionActualW, muestra.Estado }).ToArrayAsync(cancellationToken);
        var porPanel = ultimas.ToDictionary(fila => fila.PanelId, fila => new MuestraOperativaDto(
            fila.ID, fila.PanelId, fila.FechaUtc, (decimal)fila.GeneracionActualW, fila.Estado));
        return paneles.Select(panel => new SeriePanelDto(panel, ResumenMediciones.Vacio,
            new(0, null, null, 0, false), [], porPanel.GetValueOrDefault(panel.Id))).ToArray();
    }

    private async Task<SeriePanelDto[]> SeriesAsync(IQueryable<Medicion> muestras, ElementoOperativoDto[] paneles,
        PeriodoConsultaDto periodo, DateTimeOffset instante, string? estado, CancellationToken cancellationToken)
    {
        var acumulados = paneles.ToDictionary(panel => panel.Id, _ => new AcumuladoSerie());
        // Stream de proyecciones: memoria proporcional a paneles, nunca al historial completo.
        await foreach (var fila in FiltrarPeriodo(muestras, periodo, instante, estado)
            .OrderBy(muestra => EF.Property<Guid>(muestra, "IdPanel")).ThenBy(muestra => muestra.FechaUtc).ThenBy(muestra => muestra.ID)
            .Select(muestra => new { PanelId = EF.Property<Guid>(muestra, "IdPanel"), muestra.FechaUtc,
                muestra.GeneracionActualW, muestra.Estado }).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var acumulado = acumulados[fila.PanelId];
            var fecha = new DateTimeOffset(fila.FechaUtc);
            acumulado.Resumen = acumulado.Resumen.Componer(ResumenMediciones.Calcular([(decimal)fila.GeneracionActualW]));
            if (acumulado.Ultima.HasValue) acumulado.Hueco = Math.Max(acumulado.Hueco, (fecha - acumulado.Ultima.Value).TotalSeconds);
            acumulado.Primera ??= fecha;
            acumulado.Ultima = fecha;
            if (acumulado.Estados.TryGetValue(fila.Estado, out var conteo))
                acumulado.Estados[fila.Estado] = conteo with { Cantidad = conteo.Cantidad + 1, UltimaFechaUtc = fecha };
            else acumulado.Estados[fila.Estado] = new(fila.Estado, 1, fecha, fecha);
        }
        var hastaEfectivo = periodo.HastaUtc < instante ? periodo.HastaUtc : instante;
        if (hastaEfectivo < periodo.DesdeUtc) hastaEfectivo = periodo.DesdeUtc;
        return paneles.Select(panel =>
        {
            var acumulado = acumulados[panel.Id];
            return new SeriePanelDto(panel, acumulado.Resumen, CoberturaConsultaService.Calcular(acumulado.Resumen.Cantidad,
                acumulado.Primera, acumulado.Ultima, acumulado.Hueco, periodo.DesdeUtc, hastaEfectivo, _opciones.HuecoSegundos),
                acumulado.Estados.Values.ToArray(), null);
        }).ToArray();
    }

    private sealed class AcumuladoSerie
    {
        public ResumenMediciones Resumen { get; set; } = ResumenMediciones.Vacio;
        public DateTimeOffset? Primera { get; set; }
        public DateTimeOffset? Ultima { get; set; }
        public double Hueco { get; set; }
        public Dictionary<EstadoPanel, ConteoEstadoObservado> Estados { get; } = [];
    }
}
