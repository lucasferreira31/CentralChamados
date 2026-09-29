using CentralChamados.Domain;
using Microsoft.EntityFrameworkCore;
namespace CentralChamados.Persistence;

public sealed record FiltroChamados(string? Busca = null, StatusChamado? Status = null,
    PrioridadeChamado? Prioridade = null, int Pagina = 1);
public sealed record ContagensChamados(int Abertos, int EmAtendimento, int Resolvidos)
{
    public int Total => Abertos + EmAtendimento + Resolvidos;
}
public sealed record PaginaChamados(IReadOnlyList<ChamadoResumo> Itens, ContagensChamados Contagens,
    int Pagina, int TotalPaginas, FiltroChamados Filtro);

public sealed partial class ChamadosService
{
    public const int TamanhoPagina = 10;
    /// <summary>O escopo vem da conta autenticada no controller, nunca de um parâmetro HTTP.</summary>
    public async Task<PaginaChamados> PesquisarAsync(FiltroChamados filtro, Guid? solicitanteId,
        CancellationToken ct = default)
    {
        var busca = filtro.Busca?.Trim();
        if (busca?.Length > 100 || filtro.Pagina < 1 ||
            (filtro.Status.HasValue && !Enum.IsDefined(filtro.Status.Value)) ||
            (filtro.Prioridade.HasValue && !Enum.IsDefined(filtro.Prioridade.Value)))
            throw new RegraDeNegocioException("Filtros inválidos.");
        await using var db = await fabrica.CreateDbContextAsync(ct);
        // Mantém contagens e página na mesma fotografia do banco.
        await using var transacao = await IniciarTransacao(db, ct, deferred: true);
        IQueryable<Chamado> consulta = db.Chamados.AsNoTracking();
        if (solicitanteId.HasValue) consulta = consulta.Where(c => c.SolicitanteId == solicitanteId.Value);
        if (!string.IsNullOrEmpty(busca)) consulta = consulta.Where(c => c.Titulo.Contains(busca));
        if (filtro.Status.HasValue) consulta = consulta.Where(c => c.Status == filtro.Status.Value);
        if (filtro.Prioridade.HasValue) consulta = consulta.Where(c => c.Prioridade == filtro.Prioridade.Value);
        var grupos = await consulta.GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Quantidade = g.Count() }).ToListAsync(ct);
        int Quantidade(StatusChamado status) => grupos.SingleOrDefault(g => g.Status == status)?.Quantidade ?? 0;
        var contagens = new ContagensChamados(Quantidade(StatusChamado.Aberto),
            Quantidade(StatusChamado.EmAtendimento), Quantidade(StatusChamado.Resolvido));
        var paginas = Math.Max(1, (int)Math.Ceiling(contagens.Total / (double)TamanhoPagina));
        var pagina = Math.Min(filtro.Pagina, paginas);
        var ordenada = consulta.OrderByDescending(c => c.Prioridade).ThenBy(c => c.Titulo).ThenBy(c => c.Id)
            .Skip((pagina - 1) * TamanhoPagina).Take(TamanhoPagina);
        var itens = await Projetar(db, ordenada).ToListAsync(ct);
        return new PaginaChamados(itens.AsReadOnly(), contagens, pagina, paginas,
            filtro with { Busca = busca, Pagina = pagina });
    }
}
