using System.Net;
using System.Text.RegularExpressions;
using CentralChamados.Domain;
using CentralChamados.Persistence;
using CentralChamados.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CentralChamados.Web.Tests;

public sealed partial class FluxoWebTests : IAsyncLifetime
{
    private const string SenhaTeste = "SomenteTeste!2026";
    private readonly string pasta = Directory.CreateTempSubdirectory("chamados-web-").FullName;
    private WebApplicationFactory<Program> app = null!;
    private FabricaChamadosDb fabrica = null!;
    private ChamadosService servico = null!;
    private readonly List<HttpClient> clientes = [];
    public async Task InitializeAsync()
    {
        fabrica = new FabricaChamadosDb(Path.Combine(pasta, "teste.db"));
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<ChamadosDbContext>>();
                services.AddSingleton<IDbContextFactory<ChamadosDbContext>>(fabrica);
            });
        });
        using var scope = app.Services.CreateScope();
        await AdministracaoIdentity.InicializarAsync(scope.ServiceProvider);
        servico = new ChamadosService(fabrica);
    }
    private HttpClient Cliente()
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        clientes.Add(client);
        return client;
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string formulario,
        string destino, params (string, string)[] campos)
    {
        var html = await client.GetStringAsync(formulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return await client.PostAsync(destino, new FormUrlEncodedContent(campos
            .Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2))
            .Append(new("__RequestVerificationToken", WebUtility.HtmlDecode(token)))));
    }
    private async Task<(HttpClient Client, Conta Conta)> Pessoa(string email, bool tecnico = false)
    {
        using var scope = app.Services.CreateScope();
        var cadastro = scope.ServiceProvider.GetRequiredService<CadastroService>();
        Assert.True((await cadastro.CadastrarAsync("Pessoa de teste", email, SenhaTeste, default)).Succeeded);
        if (tecnico) Assert.True(await AdministracaoIdentity.PromoverTecnicoAsync(scope.ServiceProvider, email));
        var conta = (await scope.ServiceProvider.GetRequiredService<UserManager<Conta>>().FindByEmailAsync(email))!;
        var client = Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await Entrar(client, email)).StatusCode);
        return (client, conta);
    }
    private static Task<HttpResponseMessage> Entrar(HttpClient client, string email,
        string senha = SenhaTeste, string? returnUrl = null)
        => Post(client, "/Conta/Entrar", "/Conta/Entrar", ("Email", email),
            ("Senha", senha), ("ReturnUrl", returnUrl ?? ""));
    private static void Negado(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Conta/AcessoNegado", response.Headers.Location!.ToString());
    }
    [Fact]
    public async Task CadastroPublicoCriaSolicitanteEHashIgnorandoPerfilEParticipanteForjados()
    {
        var existente = await servico.CadastrarUsuarioAsync("Participante antigo");
        var client = Cliente();
        var response = await Post(client, "/Conta/Cadastrar", "/Conta/Cadastrar", ("Nome", "Lucas"),
            ("Email", "lucas@example.test"), ("Senha", SenhaTeste), ("ConfirmarSenha", SenhaTeste),
            ("Perfil", "Tecnico"), ("ParticipanteId", existente.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<Conta>>();
        var conta = (await manager.FindByEmailAsync("lucas@example.test"))!;
        Assert.NotEqual(existente, conta.ParticipanteId);
        Assert.Equal(new[] { Perfis.Solicitante }, await manager.GetRolesAsync(conta));
        Assert.NotEqual(SenhaTeste, conta.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(conta, SenhaTeste));
        Assert.Equal(2, (await servico.ListarUsuariosAsync()).Count);
    }
    [Fact]
    public async Task CadastroDuplicadoNaoDeixaParticipanteOrfao()
    {
        await Pessoa("lucas@example.test");
        var client = Cliente();
        var response = await Post(client, "/Conta/Cadastrar", "/Conta/Cadastrar", ("Nome", "Duplicado"),
            ("Email", "LUCAS@example.test"), ("Senha", SenhaTeste), ("ConfirmarSenha", SenhaTeste));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        Assert.Single(await servico.ListarUsuariosAsync());
        await using var db = fabrica.CreateDbContext();
        Assert.Single(await db.Users.ToListAsync());
    }
    [Theory]
    [InlineData("curta", "curta", "Senha")]
    [InlineData(SenhaTeste, "Diferente!2026", "ConfirmarSenha")]
    public async Task CadastroInvalidoNaoGravaNemDevolveSenha(string senha, string confirmacao, string campo)
    {
        var client = Cliente();
        var response = await Post(client, "/Conta/Cadastrar", "/Conta/Cadastrar", ("Nome", "Lucas"),
            ("Email", "lucas@example.test"), ("Senha", senha), ("ConfirmarSenha", confirmacao));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-valmsg-for=\"" + campo + "\"", html);
        Assert.DoesNotContain("value=\"" + senha + "\"", html);
        Assert.Contains("field-validation-error", html);
        Assert.Empty(await servico.ListarUsuariosAsync());
    }
    [Fact]
    public async Task FalhaAoGravarPerfilDesfazContaEParticipante()
    {
        await using (var db = fabrica.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_perfil BEFORE INSERT ON AspNetUserRoles BEGIN SELECT RAISE(ABORT, 'falha simulada'); END;");
        var client = Cliente();
        var response = await Post(client, "/Conta/Cadastrar", "/Conta/Cadastrar", ("Nome", "Lucas"),
            ("Email", "lucas@example.test"), ("Senha", SenhaTeste), ("ConfirmarSenha", SenhaTeste));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("falha simulada", html);
        Assert.DoesNotContain("SqliteException", html);
        await using var check = fabrica.CreateDbContext();
        Assert.Empty(await check.Users.ToListAsync());
        Assert.Empty(await check.Usuarios.ToListAsync());
    }
    [Fact]
    public async Task FluxoCompletoUsaContasAutenticadasEPreservaHistorico()
    {
        var (lucas, solicitante) = await Pessoa("lucas@example.test");
        var (ana, tecnica) = await Pessoa("ana@example.test", tecnico: true);
        var abertura = await Post(lucas, "/Chamados/Abrir", "/Chamados/Abrir",
            ("Titulo", "Impressora indisponível"), ("Descricao", "Não imprime."),
            ("SolicitanteId", tecnica.ParticipanteId.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, abertura.StatusCode);
        var chamado = Assert.Single(await servico.ListarAsync());
        Assert.Equal(solicitante.ParticipanteId, chamado.SolicitanteId);
        var url = "/Chamados/Detalhes/" + chamado.Id;
        Assert.Equal(HttpStatusCode.Redirect, (await Post(ana, url, "/Chamados/Iniciar/" + chamado.Id,
            ("AutorId", solicitante.ParticipanteId.ToString()))).StatusCode);
        Assert.Equal(tecnica.ParticipanteId, (await servico.ConsultarAsync(chamado.Id))!.Resumo.TecnicoId);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(ana, url, "/Chamados/Resolver/" + chamado.Id,
            ("Texto", "Driver atualizado."))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(lucas, url, "/Chamados/Reabrir/" + chamado.Id,
            ("Texto", "Problema voltou."))).StatusCode);
        var salvo = (await servico.ConsultarAsync(chamado.Id))!;
        Assert.Equal(StatusChamado.Aberto, salvo.Resumo.Status);
        Assert.Equal(4, salvo.Historico.Count);
        Assert.Null(salvo.Solucao);
        Assert.Contains("Driver atualizado.", await lucas.GetStringAsync(url));
    }
    [Fact]
    public async Task SolicitanteNaoListaNemConsultaNemReabreChamadoAlheio()
    {
        var (lucas, dono) = await Pessoa("lucas@example.test");
        var (outro, _) = await Pessoa("outro@example.test");
        var id = await servico.AbrirAsync("Titulo confidencial", "Descrição privada", dono.ParticipanteId);
        Assert.Contains("Titulo confidencial", await lucas.GetStringAsync("/"));
        var html = await outro.GetStringAsync("/");
        Assert.DoesNotContain("Titulo confidencial", html);
        Assert.DoesNotContain(id.ToString(), html);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync("/Chamados/Detalhes/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(outro, "/", "/Chamados/Reabrir/" + id,
            ("AutorId", dono.ParticipanteId.ToString()), ("Texto", "Forjado"))).StatusCode);
        Assert.Single((await servico.ConsultarAsync(id))!.Historico);
    }
    [Theory]
    [InlineData("Iniciar")]
    [InlineData("Resolver")]
    public async Task SolicitanteNaoExecutaAcaoDeTecnicoMesmoNoProprioChamado(string acao)
    {
        var (client, dono) = await Pessoa("lucas@example.test");
        var id = await servico.AbrirAsync("Rede", "Sem conexão", dono.ParticipanteId);
        Negado(await Post(client, "/", "/Chamados/" + acao + "/" + id, ("Texto", "OK")));
        Assert.Single((await servico.ConsultarAsync(id))!.Historico);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Conta/AcessoNegado")).StatusCode);
    }
    [Fact]
    public async Task OutroTecnicoNaoResolveNemReabreENaoPodeFalsificarAutor()
    {
        var (_, dono) = await Pessoa("lucas@example.test");
        var (_, tecnica) = await Pessoa("ana@example.test", true);
        var (outro, _) = await Pessoa("bruno@example.test", true);
        var id = await servico.AbrirAsync("Rede", "Sem conexão", dono.ParticipanteId);
        await servico.IniciarAsync(id, tecnica.ParticipanteId);
        var url = "/Chamados/Detalhes/" + id;
        Assert.Contains("Rede", await outro.GetStringAsync("/"));
        Negado(await Post(outro, url, "/Chamados/Resolver/" + id,
            ("AutorId", tecnica.ParticipanteId.ToString()), ("Texto", "Forjado")));
        await servico.ResolverAsync(id, tecnica.ParticipanteId, "Solução");
        Negado(await Post(outro, url, "/Chamados/Reabrir/" + id,
            ("AutorId", dono.ParticipanteId.ToString()), ("Texto", "Forjado")));
        Assert.Equal(3, (await servico.ConsultarAsync(id))!.Historico.Count);
    }
    [Fact]
    public async Task FormularioInvalidoEEstadoDesatualizadoNaoAlteramHistorico()
    {
        var (client, conta) = await Pessoa("ana@example.test", true);
        var vazio = await Post(client, "/Chamados/Abrir", "/Chamados/Abrir");
        var html = WebUtility.HtmlDecode(await vazio.Content.ReadAsStringAsync());
        Assert.Contains("Informe o título.", html);
        Assert.Contains("Descreva o problema.", html);
        Assert.Empty(await servico.ListarAsync());
        var id = await servico.AbrirAsync("Rede", "Sem conexão", conta.ParticipanteId);
        await servico.IniciarAsync(id, conta.ParticipanteId);
        var url = "/Chamados/Detalhes/" + id;
        var solucao = await Post(client, url, "/Chamados/Resolver/" + id, ("Texto", " "));
        Assert.Contains("Descreva a solução.", WebUtility.HtmlDecode(await solucao.Content.ReadAsStringAsync()));
        var repetido = await Post(client, url, "/Chamados/Iniciar/" + id);
        Assert.Contains("validation-summary-errors", await repetido.Content.ReadAsStringAsync());
        Assert.Equal(2, (await servico.ConsultarAsync(id))!.Historico.Count);
    }
    [Theory]
    [InlineData("/")]
    [InlineData("/Chamados/Abrir")]
    [InlineData("/Chamados/Detalhes/00000000-0000-0000-0000-000000000001")]
    public async Task AnonimoPrecisaEntrar(string url)
    {
        var response = await Cliente().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Conta/Entrar", response.Headers.Location!.ToString());
    }
    [Theory]
    [InlineData("/Conta/Entrar")]
    [InlineData("/Conta/Cadastrar")]
    [InlineData("/Conta/Sair")]
    [InlineData("/Chamados/Abrir")]
    [InlineData("/Chamados/Iniciar")]
    [InlineData("/Chamados/Resolver")]
    [InlineData("/Chamados/Reabrir")]
    [InlineData("/Chamados/Comentar")]
    public async Task PostSemAntifalsificacaoERejeitado(string url)
    {
        var (client, _) = await Pessoa("ana@example.test", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(url, new FormUrlEncodedContent([]))).StatusCode);
        Assert.Empty(await servico.ListarAsync());
    }
    [Fact]
    public async Task SairEncerraSessaoENaoAceitaGet()
    {
        var (client, _) = await Pessoa("lucas@example.test");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/Conta/Sair")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/", "/Conta/Sair")).StatusCode);
        Assert.Contains("/Conta/Entrar", (await client.GetAsync("/")).Headers.Location!.ToString());
    }
    [Fact]
    public async Task LoginNaoRedirecionaParaSiteExternoECookieTemProtecoes()
    {
        await Pessoa("lucas@example.test");
        var client = Cliente();
        var response = await Entrar(client, "lucas@example.test", returnUrl: "https://externo.example");
        Assert.Equal("/", response.Headers.Location!.ToString());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("CentralChamados.Sessao="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var pagina = await client.GetAsync("/");
        Assert.Contains("no-store", pagina.Headers.CacheControl!.ToString());
    }
    [Fact]
    public async Task LoginRespeitaRetornoLocal()
    {
        await Pessoa("lucas@example.test");
        var response = await Entrar(Cliente(), "lucas@example.test", returnUrl: "/Chamados/Abrir");
        Assert.Equal("/Chamados/Abrir", response.Headers.Location!.ToString());
    }
    [Fact]
    public async Task CincoSenhasErradasBloqueiamNovasTentativasTemporariamente()
    {
        await Pessoa("lucas@example.test");
        var client = Cliente();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await Entrar(client, "lucas@example.test", "Errada!2026")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Entrar(client, "lucas@example.test")).StatusCode);
        using var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<Conta>>();
        Assert.True(await manager.IsLockedOutAsync((await manager.FindByEmailAsync("lucas@example.test"))!));
    }
    [Fact]
    public async Task PromocaoExigeNovoLoginEPerfisSaoIdempotentes()
    {
        var (client, _) = await Pessoa("ana@example.test");
        using (var scope = app.Services.CreateScope())
        {
            await AdministracaoIdentity.InicializarAsync(scope.ServiceProvider);
            Assert.True(await AdministracaoIdentity.PromoverTecnicoAsync(scope.ServiceProvider, "ana@example.test"));
        }
        Assert.Contains("/Conta/Entrar", (await client.GetAsync("/")).Headers.Location!.ToString());
        await Entrar(client, "ana@example.test");
        Assert.Contains("Fila de atendimento", await client.GetStringAsync("/"));
        await using var db = fabrica.CreateDbContext();
        Assert.Equal(2, await db.Roles.CountAsync());
    }
    [Fact]
    public async Task ChamadoInexistenteRetorna404ParaContaAutenticada()
    {
        var (client, _) = await Pessoa("lucas@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Chamados/Detalhes/" + Guid.NewGuid())).StatusCode);
    }
    [Fact]
    public async Task ConteudoDoUsuarioEHtmlCodificado()
    {
        var (client, conta) = await Pessoa("lucas@example.test");
        var id = await servico.AbrirAsync("<img src=x onerror=alert(1)>", "<script>alert(2)</script>", conta.ParticipanteId);
        var html = await client.GetStringAsync("/Chamados/Detalhes/" + id);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;img", html);
    }
    [Fact]
    public async Task MigrationAtualizaBancoAntigoSemPerderChamadosNemCriarContasAutomaticamente()
    {
        var antiga = new FabricaChamadosDb(Path.Combine(pasta, "antigo.db"));
        await using (var db = antiga.CreateDbContext())
            await db.GetService<IMigrator>().MigrateAsync("20260925222528_Inicial");
        var legado = new ChamadosService(antiga);
        var participante = await legado.CadastrarUsuarioAsync("Lucas antigo");
        var id = Guid.NewGuid();
        // Insere usando o esquema histórico, que ainda não tinha a coluna Prioridade.
        await using (var db = antiga.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Chamados (Id,Titulo,Descricao,SolicitanteId,Solicitante,Status) VALUES ({id}, {"Chamado antigo"}, {"Preservar"}, {participante}, {"Lucas antigo"}, {"Aberto"})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Eventos (Id,ChamadoId,Sequencia,Em,AutorId,Autor,Descricao) VALUES ({Guid.NewGuid()}, {id}, 0, {DateTimeOffset.UtcNow.UtcTicks}, {participante}, {"Lucas antigo"}, {"Chamado aberto."})");
        }
        await using (var db = antiga.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Users.ToListAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }
        Assert.Equal(participante, (await legado.ConsultarAsync(id))!.Resumo.SolicitanteId);
        Assert.Equal(PrioridadeChamado.Normal, (await legado.ConsultarAsync(id))!.Resumo.Prioridade);
        Assert.Single((await legado.ConsultarAsync(id))!.Historico);
    }
    public async Task DisposeAsync()
    {
        foreach (var client in clientes) client.Dispose();
        await app.DisposeAsync();
        foreach (var arquivo in Directory.GetFiles(pasta)) File.Delete(arquivo);
        Directory.Delete(pasta);
    }
}
