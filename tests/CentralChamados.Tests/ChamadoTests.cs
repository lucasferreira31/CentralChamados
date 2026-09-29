using CentralChamados.Domain;
namespace CentralChamados.Tests;

public class ChamadoTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Usuario Lucas = new("Lucas");
    private readonly Usuario Ana = new("Ana");
    private readonly Usuario Bruno = new("Bruno");
    private Chamado Novo() => new("Impressora", "Falha ao imprimir.", Lucas, Inicio);

    [Fact]
    public void CicloCompletoRegistraHistoricoEmOrdem()
    {
        var chamado = Novo();
        chamado.IniciarAtendimento(Ana, Inicio.AddMinutes(1));
        chamado.Resolver(Ana, "Cabo substituído.", Inicio.AddMinutes(2));
        Assert.Equal(StatusChamado.Resolvido, chamado.Status);
        Assert.Equal("Cabo substituído.", chamado.Solucao);
        Assert.Equal(3, chamado.Historico.Count);
        Assert.Equal(Ana.Nome, chamado.Historico[^1].Autor);
        Assert.Equal(Ana.Id, chamado.Historico[^1].AutorId);
    }

    [Fact]
    public void NaoPermiteResolverSemIniciarAtendimento()
    {
        var chamado = Novo();
        Assert.Throws<RegraDeNegocioException>(() => chamado.Resolver(Ana, "Solução", Inicio));
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Single(chamado.Historico);
    }

    [Fact]
    public void OutroTecnicoNaoPodeResolver()
    {
        var chamado = Novo();
        chamado.IniciarAtendimento(Ana, Inicio);
        Assert.Throws<RegraDeNegocioException>(() => chamado.Resolver(Bruno, "Solução", Inicio));
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
        Assert.Null(chamado.Solucao);
        Assert.Equal(2, chamado.Historico.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ExigeDescricaoDaSolucao(string solucao)
    {
        var chamado = Novo();
        chamado.IniciarAtendimento(Ana, Inicio);
        Assert.Throws<RegraDeNegocioException>(() => chamado.Resolver(Ana, solucao, Inicio));
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
    }

    [Fact]
    public void NaoPermiteTomarAtendimentoDeOutroTecnico()
    {
        var chamado = Novo();
        chamado.IniciarAtendimento(Ana, Inicio);
        Assert.Throws<RegraDeNegocioException>(() => chamado.IniciarAtendimento(Bruno, Inicio));
        Assert.Equal(Ana.Id, chamado.TecnicoId);
    }

    [Fact]
    public void ReaberturaPreservaSolucaoAnteriorNoHistorico()
    {
        var chamado = Novo();
        chamado.IniciarAtendimento(Ana, Inicio);
        chamado.Resolver(Ana, "Cabo substituído.", Inicio);
        chamado.Reabrir(Lucas, "O problema voltou.", Inicio.AddHours(1));
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Null(chamado.Tecnico);
        Assert.Null(chamado.Solucao);
        Assert.Contains(chamado.Historico, e => e.Descricao.Contains("Cabo substituído."));
        chamado.IniciarAtendimento(Bruno, Inicio.AddHours(2));
        Assert.Equal(Bruno.Id, chamado.TecnicoId);
    }

    [Fact]
    public void OutraPessoaNaoPodeReabrir()
    {
        var chamado = Novo();
        chamado.IniciarAtendimento(Ana, Inicio);
        chamado.Resolver(Ana, "Solução.", Inicio);
        Assert.Throws<RegraDeNegocioException>(() => chamado.Reabrir(Bruno, "Motivo.", Inicio));
        Assert.Equal(StatusChamado.Resolvido, chamado.Status);
    }

    [Fact]
    public void ReabrirChamadoAbertoEInvalido()
    {
        var chamado = Novo();
        Assert.Throws<RegraDeNegocioException>(() => chamado.Reabrir(Lucas, "Motivo", Inicio));
    }

    [Fact]
    public void EventoAnteriorNaoMudaEstado()
    {
        var chamado = Novo();
        Assert.Throws<RegraDeNegocioException>(() => chamado.IniciarAtendimento(Ana, Inicio.AddMinutes(-1)));
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Null(chamado.Tecnico);
        Assert.Single(chamado.Historico);
    }

    [Theory]
    [InlineData("", "Descrição", "Lucas")]
    [InlineData("Título", "", "Lucas")]
    [InlineData("Título", "Descrição", " ")]
    public void CamposObrigatoriosSaoValidados(string titulo, string descricao, string solicitante)
        => Assert.Throws<RegraDeNegocioException>(() => new Chamado(titulo, descricao, new Usuario(solicitante), Inicio));
}
