using CentralChamados.Domain;
namespace CentralChamados.Tests;

public sealed class IdentidadeTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void RenomearPreservaIdEPermiteResolver()
    {
        var usuario = new Usuario("Lucas");
        var tecnico = new Usuario("Ana");
        var id = tecnico.Id;
        var chamado = new Chamado("Título", "Descrição", usuario, Inicio);
        chamado.IniciarAtendimento(tecnico, Inicio);
        tecnico.Renomear("Ana Silva");
        chamado.Resolver(tecnico, "Solução", Inicio);
        Assert.Equal(id, tecnico.Id);
        Assert.Equal("Ana", chamado.Historico[1].Autor);
        Assert.Equal("Ana Silva", chamado.Historico[2].Autor);
    }
    [Fact]
    public void HomonimoNaoPodeResolver()
    {
        var tecnico = new Usuario("Ana");
        var chamado = new Chamado("Título", "Descrição", new Usuario("Lucas"), Inicio);
        chamado.IniciarAtendimento(tecnico, Inicio);
        Assert.Throws<RegraDeNegocioException>(() => chamado.Resolver(new Usuario("Ana"), "Solução", Inicio));
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
    }
    [Fact]
    public void RenomeacaoInvalidaPreservaNomeAnterior()
    {
        var usuario = new Usuario(" Lucas ");
        Assert.Throws<RegraDeNegocioException>(() => usuario.Renomear(new string('A', 101)));
        Assert.Throws<RegraDeNegocioException>(() => usuario.Renomear(" "));
        Assert.Equal("Lucas", usuario.Nome);
    }
    [Fact]
    public void HistoricoNaoPodeSerAlteradoPelaColecaoPublica()
    {
        var chamado = new Chamado("Título", "Descrição", new Usuario("Lucas"), Inicio);
        Assert.Throws<NotSupportedException>(() => ((IList<EventoChamado>)chamado.Historico).Clear());
        Assert.Single(chamado.Historico);
    }
}
