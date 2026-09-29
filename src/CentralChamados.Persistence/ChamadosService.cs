using CentralChamados.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace CentralChamados.Persistence;

public sealed record UsuarioConsulta(Guid Id, string Nome);
public sealed record EventoConsulta(int Sequencia, DateTimeOffset Em, Guid AutorId, string Autor, string Descricao);
public sealed record ChamadoResumo(Guid Id, string Titulo, StatusChamado Status, Guid SolicitanteId,
    string SolicitanteAtual, Guid? TecnicoId, string? TecnicoAtual, PrioridadeChamado Prioridade = PrioridadeChamado.Normal);
public sealed record ChamadoConsulta(ChamadoResumo Resumo, string Descricao, string? Solucao, IReadOnlyList<EventoConsulta> Historico);

/// <summary>Operações locais. IDs são vínculos persistentes, ainda não autenticação.</summary>
public sealed partial class ChamadosService(IDbContextFactory<ChamadosDbContext> fabrica, TimeProvider? relogio = null)
{
    private DateTimeOffset Agora => (relogio ?? TimeProvider.System).GetUtcNow();
    public async Task<Guid> CadastrarUsuarioAsync(string nome, CancellationToken ct = default)
    {
        var usuario = new Usuario(nome);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        return usuario.Id;
    }
    public async Task RenomearUsuarioAsync(Guid id, string nome, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        await using var transacao = await IniciarTransacao(db, ct);
        (await UsuarioAsync(db, id, ct)).Renomear(nome);
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
    }
    public Task<Guid> AbrirAsync(string titulo, string descricao, Guid solicitanteId, CancellationToken ct = default)
        => AbrirAsync(titulo, descricao, solicitanteId, PrioridadeChamado.Normal, ct);
    public async Task<Guid> AbrirAsync(string titulo, string descricao, Guid solicitanteId, PrioridadeChamado prioridade, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        await using var transacao = await IniciarTransacao(db, ct);
        var solicitante = await UsuarioAsync(db, solicitanteId, ct);
        var chamado = new Chamado(titulo, descricao, solicitante, Agora, prioridade);
        db.Chamados.Add(chamado);
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return chamado.Id;
    }
    public Task IniciarAsync(Guid id, Guid tecnicoId, CancellationToken ct = default)
        => AlterarAsync(id, tecnicoId, (c, u) => c.IniciarAtendimento(u, Agora), ct);
    public Task ResolverAsync(Guid id, Guid tecnicoId, string solucao, CancellationToken ct = default)
        => AlterarAsync(id, tecnicoId, (c, u) => c.Resolver(u, solucao, Agora), ct);
    public Task ReabrirAsync(Guid id, Guid solicitanteId, string motivo, CancellationToken ct = default)
        => AlterarAsync(id, solicitanteId, (c, u) => c.Reabrir(u, motivo, Agora), ct);

    public Task ComentarAsync(Guid id, Guid autorId, string comentario, CancellationToken ct = default)
        => AlterarAsync(id, autorId, (c, u) => c.Comentar(u, comentario, Agora), ct);

    private async Task AlterarAsync(Guid id, Guid autorId, Action<Chamado, Usuario> alterar, CancellationToken ct)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        await using var transacao = await IniciarTransacao(db, ct);
        var chamado = await db.Chamados.Include(c => c.Historico.OrderBy(e => e.Sequencia))
            .SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException("Chamado não encontrado.");
        var autor = await UsuarioAsync(db, autorId, ct);
        alterar(chamado, autor);
        // Estado e novo evento pertencem à mesma transação.
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<UsuarioConsulta>> ListarUsuariosAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.Usuarios.AsNoTracking().OrderBy(u => u.Nome).ThenBy(u => u.Id)
            .Select(u => new UsuarioConsulta(u.Id, u.Nome)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<ChamadoResumo>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await Resumos(db).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<ChamadoResumo>> ListarDoSolicitanteAsync(Guid solicitanteId, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await Resumos(db, solicitanteId: solicitanteId).ToListAsync(ct);
    }
    public async Task<ChamadoConsulta?> ConsultarAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        // Uma leitura transacional mantém resumo, solução e eventos consistentes entre si.
        await using var transacao = await IniciarTransacao(db, ct, deferred: true);
        var resumo = await Resumos(db, id).SingleOrDefaultAsync(ct);
        if (resumo is null) return null;
        var chamado = await db.Chamados.AsNoTracking().Include(c => c.Historico.OrderBy(e => e.Sequencia))
            .SingleAsync(c => c.Id == id, ct);
        return new ChamadoConsulta(resumo, chamado.Descricao, chamado.Solucao,
            chamado.Historico.OrderBy(e => e.Sequencia).Select(e =>
                new EventoConsulta(e.Sequencia, e.Em, e.AutorId, e.Autor, e.Descricao)).ToList().AsReadOnly());
    }
    private static IQueryable<ChamadoResumo> Resumos(ChamadosDbContext db, Guid? id = null, Guid? solicitanteId = null)
    {
        IQueryable<Chamado> consulta = db.Chamados.AsNoTracking();
        if (id.HasValue) consulta = consulta.Where(c => c.Id == id.Value);
        if (solicitanteId.HasValue) consulta = consulta.Where(c => c.SolicitanteId == solicitanteId.Value);
        // Filtra e ordena entidades antes de projetar para o record de leitura.
        return Projetar(db, consulta.OrderBy(c => c.Titulo).ThenBy(c => c.Id));
    }
    private static IQueryable<ChamadoResumo> Projetar(ChamadosDbContext db, IQueryable<Chamado> consulta)
    {
        return consulta.Select(c =>
            new ChamadoResumo(c.Id, c.Titulo, c.Status, c.SolicitanteId,
                db.Usuarios.Where(u => u.Id == c.SolicitanteId).Select(u => u.Nome).Single(),
                c.TecnicoId, db.Usuarios.Where(u => u.Id == c.TecnicoId).Select(u => u.Nome).SingleOrDefault(), c.Prioridade));
    }
    private static async Task<Usuario> UsuarioAsync(ChamadosDbContext db, Guid id, CancellationToken ct)
        => await db.Usuarios.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
    private static async Task<SqliteTransaction> IniciarTransacao(ChamadosDbContext db, CancellationToken ct, bool deferred = false)
    {
        await db.Database.OpenConnectionAsync(ct);
        var transacao = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: deferred);
        try { await db.Database.UseTransactionAsync(transacao, ct); return transacao; }
        catch { await transacao.DisposeAsync(); throw; }
    }
}
