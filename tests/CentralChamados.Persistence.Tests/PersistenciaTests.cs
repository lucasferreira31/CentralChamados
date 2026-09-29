using CentralChamados.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace CentralChamados.Persistence.Tests;

public sealed partial class PersistenciaTests : IAsyncLifetime
{
    private readonly string pasta = Directory.CreateTempSubdirectory("CentralChamados-tests-").FullName;
    private FabricaChamadosDb Fabrica => new(Path.Combine(pasta, "teste.db"));
    private readonly RelogioTeste relogio = new();
    private ChamadosService Service => new(Fabrica, relogio);
    private Guid lucas, ana;
    public async Task InitializeAsync()
    {
        await using var db = Fabrica.CreateDbContext();
        await db.Database.MigrateAsync();
        lucas = await Service.CadastrarUsuarioAsync("Lucas");
        ana = await Service.CadastrarUsuarioAsync("Ana");
    }
    public Task DisposeAsync()
    {
        foreach (var arquivo in Directory.GetFiles(pasta)) File.Delete(arquivo);
        Directory.Delete(pasta);
        return Task.CompletedTask;
    }
    private Task<Guid> Abrir() => Service.AbrirAsync("Impressora", "Falha ao imprimir.", lucas);
    private sealed class RelogioTeste : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    [Fact]
    public async Task MigrationPodeRepetirEOutraFabricaLeOsDados()
    {
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await using (var db = Fabrica.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        var outra = new ChamadosService(new FabricaChamadosDb(Path.Combine(pasta, "teste.db")), relogio);
        var salvo = await outra.ConsultarAsync(id);
        Assert.NotNull(salvo);
        Assert.Equal(lucas, salvo.Resumo.SolicitanteId);
        Assert.Equal(ana, salvo.Resumo.TecnicoId);
        Assert.Equal(StatusChamado.EmAtendimento, salvo.Resumo.Status);
        Assert.Equal(2, salvo.Historico.Count);
        Assert.Equal(id, Assert.Single(await outra.ListarAsync()).Id);
    }

    [Fact]
    public async Task RenomearTecnicoMantemVinculoEHistoricoAnterior()
    {
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await Service.RenomearUsuarioAsync(ana, "Ana Silva");
        await Service.ResolverAsync(id, ana, "Cabo substituído.");
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(ana, salvo.Resumo.TecnicoId);
        Assert.Equal("Ana Silva", salvo.Resumo.TecnicoAtual);
        Assert.Equal("Ana", salvo.Historico[1].Autor);
        Assert.Equal("Ana Silva", salvo.Historico[2].Autor);
        Assert.Equal(ana, salvo.Historico[2].AutorId);
        Assert.Equal(StatusChamado.Resolvido, salvo.Resumo.Status);
    }

    [Fact]
    public async Task RenomearSolicitantePermiteReabrirPeloMesmoId()
    {
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await Service.ResolverAsync(id, ana, "Solução antiga.");
        await Service.RenomearUsuarioAsync(lucas, "Lucas Ferreira");
        await Service.ReabrirAsync(id, lucas, "O problema voltou.");
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal("Lucas Ferreira", salvo.Resumo.SolicitanteAtual);
        Assert.Equal("Lucas", salvo.Historico[0].Autor);
        Assert.Equal("Lucas Ferreira", salvo.Historico[^1].Autor);
        Assert.Null(salvo.Resumo.TecnicoId);
        Assert.Null(salvo.Resumo.TecnicoAtual);
        Assert.Null(salvo.Solucao);
        Assert.Contains(salvo.Historico, e => e.Descricao.Contains("Solução antiga."));
        var bruno = await Service.CadastrarUsuarioAsync("Bruno");
        await Service.IniciarAsync(id, bruno);
        Assert.Equal(bruno, (await Service.ConsultarAsync(id))!.Resumo.TecnicoId);
    }

    [Fact]
    public async Task NomesIguaisNaoPermitemUsarIdentidadeAlheia()
    {
        var outraAna = await Service.CadastrarUsuarioAsync("Ana");
        var outroLucas = await Service.CadastrarUsuarioAsync("Lucas");
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.ResolverAsync(id, outraAna, "Solução."));
        await Service.ResolverAsync(id, ana, "Solução.");
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.ReabrirAsync(id, outroLucas, "Motivo."));
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(StatusChamado.Resolvido, salvo.Resumo.Status);
        Assert.Equal(3, salvo.Historico.Count);
    }

    [Fact]
    public async Task EventosNoMesmoInstanteMantemSequenciaAposRecarregar()
    {
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await Service.ResolverAsync(id, ana, "Solução.");
        await Service.ReabrirAsync(id, lucas, "Motivo.");
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(new[] { 0, 1, 2, 3 }, salvo.Historico.Select(e => e.Sequencia));
        Assert.All(salvo.Historico, e => Assert.Equal(relogio.Agora, e.Em));
        Assert.StartsWith("Chamado reaberto:", salvo.Historico[^1].Descricao);
    }

    [Fact]
    public async Task RelogioAnteriorNaoAlteraEstadoNemHistorico()
    {
        var id = await Abrir();
        relogio.Agora = relogio.Agora.AddMinutes(-1);
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.IniciarAsync(id, ana));
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(StatusChamado.Aberto, salvo.Resumo.Status);
        Assert.Null(salvo.Resumo.TecnicoId);
        Assert.Single(salvo.Historico);
    }

    [Fact]
    public async Task ParticipanteOuChamadoInexistenteNaoGravaAlteracao()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.AbrirAsync("A", "B", Guid.NewGuid()));
        Assert.Empty(await Service.ListarAsync());
        var id = await Abrir();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.IniciarAsync(id, Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.IniciarAsync(Guid.NewGuid(), ana));
        Assert.Null(await Service.ConsultarAsync(Guid.NewGuid()));
        Assert.Single((await Service.ConsultarAsync(id))!.Historico);
    }

    [Fact]
    public async Task SolucaoInvalidaNaoAlteraChamadoPersistido()
    {
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.ResolverAsync(id, ana, " "));
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(StatusChamado.EmAtendimento, salvo.Resumo.Status);
        Assert.Null(salvo.Solucao);
        Assert.Equal(2, salvo.Historico.Count);
    }

    [Fact]
    public async Task FalhaNoEventoDesfazMudancaDoChamadoEPermiteNovaTentativa()
    {
        var id = await Abrir();
        await Service.IniciarAsync(id, ana);
        await using (var db = Fabrica.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER falhar_evento BEFORE INSERT ON Eventos WHEN NEW.Sequencia = 2 BEGIN SELECT RAISE(ABORT, 'falha no histórico'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service.ResolverAsync(id, ana, "Solução."));
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(StatusChamado.EmAtendimento, salvo.Resumo.Status);
        Assert.Null(salvo.Solucao);
        Assert.Equal(2, salvo.Historico.Count);
        await using (var db = Fabrica.CreateDbContext()) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER falhar_evento");
        await Service.ResolverAsync(id, ana, "Solução.");
        Assert.Equal(StatusChamado.Resolvido, (await Service.ConsultarAsync(id))!.Resumo.Status);
    }

    [Fact]
    public async Task FalhaNaAberturaNaoDeixaChamadoSemEvento()
    {
        await using (var db = Fabrica.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER falhar_abertura BEFORE INSERT ON Eventos BEGIN SELECT RAISE(ABORT, 'falha'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Abrir());
        Assert.Empty(await Service.ListarAsync());
        await using var check = Fabrica.CreateDbContext();
        Assert.Empty(await check.Eventos.ToListAsync());
    }

    [Fact]
    public async Task BancoRecusaRemoverParticipanteReferenciado()
    {
        await Abrir();
        await using var db = Fabrica.CreateDbContext();
        var erro = await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Usuarios WHERE Id = {lucas}"));
        Assert.Equal(19, erro.SqliteErrorCode);
        Assert.Equal(2, (await Service.ListarUsuariosAsync()).Count);
    }

    [Fact]
    public async Task BancoRecusaEstadoInconsistenteESequenciaDuplicada()
    {
        var id = await Abrir();
        await using var db = Fabrica.CreateDbContext();
        var estado = await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Chamados SET Status = 'Resolvido' WHERE Id = {id}"));
        Assert.Equal(19, estado.SqliteErrorCode);
        var duplicado = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Eventos (Id,ChamadoId,Sequencia,Em,AutorId,Autor,Descricao) SELECT {Guid.NewGuid()},ChamadoId,Sequencia,Em,AutorId,Autor,Descricao FROM Eventos"));
        Assert.Equal(2067, duplicado.SqliteExtendedErrorCode);
        Assert.Single((await Service.ConsultarAsync(id))!.Historico);
    }

    [Fact]
    public async Task DoisTecnicosTentandoAssumirNaoSobrescrevemResponsavel()
    {
        var id = await Abrir();
        var bruno = await Service.CadastrarUsuarioAsync("Bruno");
        using var barreira = new Barrier(2);
        Task<Guid?> Assumir(Guid tecnico) => Task.Factory.StartNew(async () =>
        {
            if (!barreira.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Barreira do teste.");
            try { await Service.IniciarAsync(id, tecnico); return (Guid?)tecnico; }
            catch (RegraDeNegocioException) { return null; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        var resultados = await Task.WhenAll(Assumir(ana), Assumir(bruno));
        var vencedor = Assert.Single(resultados, r => r.HasValue);
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(vencedor, salvo.Resumo.TecnicoId);
        Assert.Equal(2, salvo.Historico.Count);
    }

    [Fact]
    public async Task ResultadoDeConsultaNaoMudaAposNovaTransicao()
    {
        var id = await Abrir();
        var antes = (await Service.ConsultarAsync(id))!;
        await Service.IniciarAsync(id, ana);
        Assert.Equal(StatusChamado.Aberto, antes.Resumo.Status);
        Assert.Single(antes.Historico);
        Assert.Equal(2, (await Service.ConsultarAsync(id))!.Historico.Count);
    }
}
