using System.Net;
using System.Text.RegularExpressions;
using CentralChamados.Domain;
namespace CentralChamados.Web.Tests;
public sealed partial class FluxoWebTests
{
    [Fact]
    public async Task AberturaAceitaPrioridadeERecusaValorInvalido()
    {
        var (client, _) = await Pessoa("lucas@example.test");
        var invalido = await Post(client, "/Chamados/Abrir", "/Chamados/Abrir",
            ("Titulo", "Rede"), ("Descricao", "Teste"), ("Prioridade", "99"));
        Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
        Assert.Contains("field-validation-error", await invalido.Content.ReadAsStringAsync());
        Assert.Empty(await servico.ListarAsync());
        var valido = await Post(client, "/Chamados/Abrir", "/Chamados/Abrir",
            ("Titulo", "Rede"), ("Descricao", "Teste"), ("Prioridade", "Urgente"));
        Assert.Equal(HttpStatusCode.Redirect, valido.StatusCode);
        Assert.Equal(PrioridadeChamado.Urgente, Assert.Single(await servico.ListarAsync()).Prioridade);
    }
    [Fact]
    public async Task PainelEPaginacaoNaoAceitamEscopoAlheio()
    {
        var (client, dono) = await Pessoa("lucas@example.test");
        var (_, outro) = await Pessoa("outro@example.test");
        for(var i = 0; i < 12; i++)
            await servico.AbrirAsync("Rede " + i.ToString("D2"), "Teste", dono.ParticipanteId, PrioridadeChamado.Alta);
        await servico.AbrirAsync("Rede secreta", "Teste", outro.ParticipanteId, PrioridadeChamado.Alta);
        await servico.AbrirAsync("Impressora", "Teste", dono.ParticipanteId);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/?Busca=Rede&Status=Aberto&Prioridade=Alta&SolicitanteId=" + outro.ParticipanteId));
        Assert.Contains("id=\"total-resultados\">12</strong>", html);
        Assert.Contains("id=\"total-abertos\">12</strong>", html);
        Assert.DoesNotContain("Rede secreta", html);
        Assert.DoesNotContain(">Impressora</a>", html);
        Assert.Equal(10, Regex.Matches(html, "data-chamado=").Count);
        var proxima = Regex.Match(html, "<a[^>]*href=\"([^\"]+)\"[^>]*>Próxima").Groups[1].Value;
        Assert.NotEmpty(proxima);
        Assert.Contains("Prioridade=Alta", proxima);
        Assert.Contains("Status=Aberto", proxima);
        Assert.Contains("Busca=Rede", proxima);
        var segunda = WebUtility.HtmlDecode(await client.GetStringAsync(proxima));
        Assert.Equal(2, Regex.Matches(segunda, "data-chamado=").Count);
        Assert.Contains("Página 2 de 2", segunda);
        Assert.Contains("id=\"total-resultados\">12</strong>", segunda);
    }
    [Theory]
    [InlineData("Status=99")]
    [InlineData("Prioridade=99")]
    [InlineData("Pagina=0")]
    [InlineData("Pagina=abc")]
    public async Task ParametroInvalidoNaoViraConsultaSemFiltro(string query)
    {
        var (client, _) = await Pessoa("lucas@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/?" + query)).StatusCode);
    }
    [Fact]
    public async Task ComentariosUsamSessaoECodificamHtml()
    {
        var (client, dono) = await Pessoa("lucas@example.test");
        var (tecnico, contaTecnica) = await Pessoa("ana@example.test", true);
        var id = await servico.AbrirAsync("Rede", "Teste", dono.ParticipanteId);
        var url = "/Chamados/Detalhes/" + id;
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, url, "/Chamados/Comentar/" + id,
            ("Comentario", "<script>alert(1)</script>"), ("AutorId", contaTecnica.ParticipanteId.ToString()))).StatusCode);
        Assert.Equal(dono.ParticipanteId, (await servico.ConsultarAsync(id))!.Historico[^1].AutorId);
        var html = await client.GetStringAsync(url);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        await servico.IniciarAsync(id, contaTecnica.ParticipanteId);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(tecnico, url, "/Chamados/Comentar/" + id, ("Comentario", "Investigando"))).StatusCode);
        Assert.Equal(4, (await servico.ConsultarAsync(id))!.Historico.Count);
    }
    [Fact]
    public async Task EstranhoETecnicoNaoAtribuidoNaoComentam()
    {
        var (_, dono) = await Pessoa("lucas@example.test");
        var (outro, _) = await Pessoa("outro@example.test");
        var (tecnico, _) = await Pessoa("ana@example.test", true);
        var id = await servico.AbrirAsync("Rede", "Teste", dono.ParticipanteId);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(outro, "/", "/Chamados/Comentar/" + id, ("Comentario", "Forjado"))).StatusCode);
        Negado(await Post(tecnico, "/", "/Chamados/Comentar/" + id, ("Comentario", "Forjado")));
        Assert.Single((await servico.ConsultarAsync(id))!.Historico);
    }
    [Fact]
    public async Task ComentarioVazioOuLongoNaoGrava()
    {
        var (client, dono) = await Pessoa("lucas@example.test");
        var id = await servico.AbrirAsync("Rede", "Teste", dono.ParticipanteId);
        foreach(var texto in new[] { " ", new string('a', 2001) })
        {
            var response = await Post(client, "/Chamados/Detalhes/" + id, "/Chamados/Comentar/" + id, ("Comentario", texto));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        }
        Assert.Single((await servico.ConsultarAsync(id))!.Historico);
    }
}
