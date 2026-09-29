using CentralChamados.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace CentralChamados.Persistence.Tests;
public sealed partial class PersistenciaTests
{
    [Theory]
    [InlineData(PrioridadeChamado.Baixa)]
    [InlineData(PrioridadeChamado.Normal)]
    [InlineData(PrioridadeChamado.Alta)]
    [InlineData(PrioridadeChamado.Urgente)]
    public async Task PrioridadeSobreviveAOutraConexao(PrioridadeChamado prioridade)
    {
        var id = await Service.AbrirAsync("Rede", "Teste", lucas, prioridade);
        Assert.Equal(prioridade, (await Service.ConsultarAsync(id))!.Resumo.Prioridade);
    }
    [Fact]
    public async Task FiltrosCombinadosEContagensRespeitamEscopo()
    {
        var certo = await Service.AbrirAsync("Rede principal", "A", lucas, PrioridadeChamado.Alta);
        var resolvido = await Service.AbrirAsync("Rede resolvida", "B", lucas, PrioridadeChamado.Alta);
        await Service.IniciarAsync(resolvido, ana);
        await Service.ResolverAsync(resolvido, ana, "Solução");
        await Service.AbrirAsync("Rede normal", "C", lucas);
        await Service.AbrirAsync("Impressora", "D", lucas, PrioridadeChamado.Alta);
        await Service.AbrirAsync("Rede alheia", "E", ana, PrioridadeChamado.Alta);
        var resultado = await Service.PesquisarAsync(new(" Rede ", StatusChamado.Aberto, PrioridadeChamado.Alta), lucas);
        Assert.Equal(certo, Assert.Single(resultado.Itens).Id);
        Assert.Equal(new ContagensChamados(1, 0, 0), resultado.Contagens);
        Assert.Equal("Rede", resultado.Filtro.Busca);
        Assert.Equal(new ContagensChamados(3, 0, 1), (await Service.PesquisarAsync(new(), lucas)).Contagens);
        Assert.Equal(5, (await Service.PesquisarAsync(new(), null)).Contagens.Total);
    }
    [Fact]
    public async Task PaginacaoNaoRepeteItensEOrdenaPrioridadeAntesDoTitulo()
    {
        for (var i = 0; i < 21; i++) await Service.AbrirAsync("Mesmo título", "Teste", lucas);
        var urgente = await Service.AbrirAsync("ZZ urgente", "Teste", lucas, PrioridadeChamado.Urgente);
        var a = await Service.PesquisarAsync(new(Pagina: 1), lucas);
        var b = await Service.PesquisarAsync(new(Pagina: 2), lucas);
        var c = await Service.PesquisarAsync(new(Pagina: int.MaxValue), lucas);
        Assert.Equal(urgente, a.Itens[0].Id);
        Assert.Equal(10, a.Itens.Count);
        Assert.Equal(10, b.Itens.Count);
        Assert.Equal(2, c.Itens.Count);
        Assert.Equal(3, c.Pagina);
        Assert.Equal(22, a.Contagens.Total);
        Assert.Equal(22, a.Itens.Concat(b.Itens).Concat(c.Itens).Select(x => x.Id).Distinct().Count());
        Assert.Equal(a.Itens.Select(x => x.Id), (await Service.PesquisarAsync(new(), lucas)).Itens.Select(x => x.Id));
    }
    [Fact]
    public async Task BuscaSemResultadoELiteraisNaoViraramCuringas()
    {
        await Service.AbrirAsync("Rede 100%_pronta", "Teste", lucas);
        await Service.AbrirAsync("Outro título", "Teste", lucas);
        Assert.Single((await Service.PesquisarAsync(new("%_"), lucas)).Itens);
        var vazio = await Service.PesquisarAsync(new("Inexistente", Pagina: 40), lucas);
        Assert.Empty(vazio.Itens);
        Assert.Equal(0, vazio.Contagens.Total);
        Assert.Equal(1, vazio.Pagina);
        Assert.Equal(2, (await Service.PesquisarAsync(new("   "), lucas)).Contagens.Total);
    }
    [Fact]
    public async Task ServicoRecusaFiltrosInvalidos()
    {
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.PesquisarAsync(new(Pagina: 0), lucas));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.PesquisarAsync(new(Status: (StatusChamado)99), lucas));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.PesquisarAsync(new(Prioridade: (PrioridadeChamado)99), lucas));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Service.PesquisarAsync(new(new string('a', 101)), lucas));
    }
    [Fact]
    public async Task ComentarioPersisteSemMudarEstado()
    {
        var id = await Abrir();
        await Service.ComentarAsync(id, lucas, "Detalhes");
        var antes = (await Service.ConsultarAsync(id))!;
        await Service.IniciarAsync(id, ana);
        await Service.ComentarAsync(id, ana, "Investigando");
        var salvo = (await Service.ConsultarAsync(id))!;
        Assert.Equal(StatusChamado.EmAtendimento, salvo.Resumo.Status);
        Assert.Equal(new[] { 0, 1, 2, 3 }, salvo.Historico.Select(e => e.Sequencia));
        Assert.Equal("Comentário: Investigando", salvo.Historico[^1].Descricao);
        Assert.Equal(2, antes.Historico.Count);
    }
    [Fact]
    public async Task FalhaNoComentarioNaoDeixaRegistroParcial()
    {
        var id = await Abrir();
        await using (var db = Fabrica.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_comentario BEFORE INSERT ON Eventos WHEN NEW.Sequencia = 1 BEGIN SELECT RAISE(ABORT, 'falha'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service.ComentarAsync(id, lucas, "Teste"));
        Assert.Single((await Service.ConsultarAsync(id))!.Historico);
    }
    [Fact]
    public async Task BancoRecusaPrioridadeInvalida()
    {
        var id = await Abrir();
        await using var db = Fabrica.CreateDbContext();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Chamados SET Prioridade = 99 WHERE Id = {id}"));
        Assert.Equal(PrioridadeChamado.Normal, (await Service.ConsultarAsync(id))!.Resumo.Prioridade);
    }
}
