using CentralChamados.Domain;
namespace CentralChamados.Tests;
public sealed class OperacaoTests
{
    private readonly Usuario autor = new("Lucas");
    private readonly Usuario tecnico = new("Ana");
    private readonly DateTimeOffset agora = DateTimeOffset.UtcNow;
    [Fact]
    public void PrioridadePadraoNormalEInvalidaRejeitada()
    {
        Assert.Equal(PrioridadeChamado.Normal, new Chamado("Rede", "Teste", autor, agora).Prioridade);
        Assert.Throws<RegraDeNegocioException>(() => new Chamado("Rede", "Teste", autor, agora, (PrioridadeChamado)99));
    }
    [Fact]
    public void ComentarioMantemEstadoPrioridadeENomeHistorico()
    {
        var c = new Chamado("Rede", "Teste", autor, agora, PrioridadeChamado.Alta);
        c.Comentar(autor, "  Mais informações  ", agora);
        autor.Renomear("Lucas Ferreira");
        Assert.Equal("Comentário: Mais informações", c.Historico[1].Descricao);
        Assert.Equal("Lucas", c.Historico[1].Autor);
        Assert.Equal(autor.Id, c.Historico[1].AutorId);
        Assert.Equal(StatusChamado.Aberto, c.Status);
        Assert.Equal(PrioridadeChamado.Alta, c.Prioridade);
    }
    [Fact]
    public void TecnicoComentaResolvidoMasPerdeVinculoNaReabertura()
    {
        var c = new Chamado("Rede", "Teste", autor, agora);
        Assert.Throws<RegraDeNegocioException>(() => c.Comentar(tecnico, "Teste", agora));
        c.IniciarAtendimento(tecnico, agora);
        c.Resolver(tecnico, "Corrigido", agora);
        c.Comentar(tecnico, "Orientação adicional", agora);
        Assert.Equal(StatusChamado.Resolvido, c.Status);
        Assert.Equal("Corrigido", c.Solucao);
        c.Reabrir(autor, "Voltou", agora);
        Assert.Throws<RegraDeNegocioException>(() => c.Comentar(tecnico, "Sem atribuição", agora));
        Assert.Equal(5, c.Historico.Count);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ComentarioVazioNaoGeraEvento(string texto)
    {
        var c = new Chamado("Rede", "Teste", autor, agora);
        Assert.Throws<RegraDeNegocioException>(() => c.Comentar(autor, texto, agora));
        Assert.Single(c.Historico);
    }
    [Fact]
    public void ComentarioLongoOuAnteriorNaoGeraEvento()
    {
        var c = new Chamado("Rede", "Teste", autor, agora);
        Assert.Throws<RegraDeNegocioException>(() => c.Comentar(autor, new string('a', 2001), agora));
        Assert.Throws<RegraDeNegocioException>(() => c.Comentar(autor, "Teste", agora.AddSeconds(-1)));
        Assert.Single(c.Historico);
    }
}
