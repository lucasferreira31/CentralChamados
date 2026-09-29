using CentralChamados.Domain;
using CentralChamados.Persistence;
using CentralChamados.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
namespace CentralChamados.Web.Controllers;

[Authorize(Roles = Perfis.Solicitante + "," + Perfis.Tecnico)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChamadosController(ChamadosService servico, UserManager<Conta> contas) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ConsultaForm filtro, CancellationToken ct)
    {
        var conta = await contas.GetUserAsync(User);
        if (conta is null) return Challenge();
        if (!ModelState.IsValid) return BadRequest("Filtros inválidos. Confira status, prioridade, busca (até 100 caracteres) e página (a partir de 1).");
        var consulta = new FiltroChamados(filtro.Busca, filtro.Status, filtro.Prioridade, filtro.Pagina);
        var resultado = await servico.PesquisarAsync(consulta,
            User.IsInRole(Perfis.Tecnico) ? null : conta.ParticipanteId, ct);
        return View(new ListaView(resultado));
    }
    [HttpGet]
    public IActionResult Abrir() => View(new AberturaForm());
    [HttpPost]
    public async Task<IActionResult> Abrir(AberturaForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var conta = await contas.GetUserAsync(User);
        if (conta is null) return Challenge();
        try
        {
            var id = await servico.AbrirAsync(form.Titulo, form.Descricao, conta.ParticipanteId, form.Prioridade, ct);
            TempData["Sucesso"] = "Chamado aberto.";
            return RedirectToAction(nameof(Detalhes), new { id });
        }
        catch (RegraDeNegocioException ex) { ModelState.AddModelError("", ex.Message); return View(form); }
    }
    [HttpGet]
    public async Task<IActionResult> Detalhes(Guid id, CancellationToken ct)
    {
        var conta = await contas.GetUserAsync(User);
        if (conta is null) return Challenge();
        return await MostrarDetalhes(id, new AcaoForm(), conta, ct);
    }
    [Authorize(Roles = Perfis.Tecnico), HttpPost]
    public Task<IActionResult> Iniciar(Guid id, AcaoForm form, CancellationToken ct)
        => Executar(id, form, "iniciar", ct);
    [Authorize(Roles = Perfis.Tecnico), HttpPost]
    public Task<IActionResult> Resolver(Guid id, AcaoForm form, CancellationToken ct)
        => Executar(id, form, "resolver", ct);
    [HttpPost]
    public Task<IActionResult> Reabrir(Guid id, AcaoForm form, CancellationToken ct)
        => Executar(id, form, "reabrir", ct);
    private async Task<IActionResult> Executar(Guid id, AcaoForm form, string acao, CancellationToken ct)
    {
        var conta = await contas.GetUserAsync(User);
        if (conta is null) return Challenge();
        var chamado = await servico.ConsultarAsync(id, ct);
        if (chamado is null || !PodeVer(chamado, conta)) return NotFound();
        if (acao == "reabrir" && chamado.Resumo.SolicitanteId != conta.ParticipanteId) return Forbid();
        if (acao == "resolver" && chamado.Resumo.TecnicoId != conta.ParticipanteId) return Forbid();
        if (acao != "iniciar" && string.IsNullOrWhiteSpace(form.Texto))
            ModelState.AddModelError(nameof(form.Texto), acao == "resolver" ? "Descreva a solução." : "Informe o motivo da reabertura.");
        if (ModelState.IsValid)
        {
            try
            {
                // IDs de autor enviados no formulário são ignorados: a conta autenticada define a identidade.
                if (acao == "iniciar") await servico.IniciarAsync(id, conta.ParticipanteId, ct);
                else if (acao == "resolver") await servico.ResolverAsync(id, conta.ParticipanteId, form.Texto!, ct);
                else await servico.ReabrirAsync(id, conta.ParticipanteId, form.Texto!, ct);
                TempData["Sucesso"] = "Chamado atualizado.";
                return RedirectToAction(nameof(Detalhes), new { id });
            }
            catch (RegraDeNegocioException ex) { ModelState.AddModelError("", ex.Message); }
            catch (KeyNotFoundException) { return NotFound(); }
        }
        return await MostrarDetalhes(id, form, conta, ct);
    }
    [HttpPost]
    public async Task<IActionResult> Comentar(Guid id, ComentarioForm form, CancellationToken ct)
    {
        var conta = await contas.GetUserAsync(User);
        if (conta is null) return Challenge();
        var chamado = await servico.ConsultarAsync(id, ct);
        if (chamado is null || !PodeVer(chamado, conta)) return NotFound();
        if (!PodeComentar(chamado, conta)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await servico.ComentarAsync(id, conta.ParticipanteId, form.Comentario, ct);
                TempData["Sucesso"] = "Comentário registrado no histórico.";
                return RedirectToAction(nameof(Detalhes), new { id });
            }
            catch (RegraDeNegocioException ex) { ModelState.AddModelError("", ex.Message); }
            catch (KeyNotFoundException) { return NotFound(); }
        }
        return await MostrarDetalhes(id, new AcaoForm(), conta, ct, form.Comentario);
    }
    private bool PodeComentar(ChamadoConsulta chamado, Conta conta)
        => chamado.Resumo.SolicitanteId == conta.ParticipanteId ||
            (User.IsInRole(Perfis.Tecnico) && chamado.Resumo.TecnicoId == conta.ParticipanteId);
    private bool PodeVer(ChamadoConsulta chamado, Conta conta)
        => User.IsInRole(Perfis.Tecnico) || chamado.Resumo.SolicitanteId == conta.ParticipanteId;
    private async Task<IActionResult> MostrarDetalhes(Guid id, AcaoForm form, Conta conta, CancellationToken ct, string comentario = "")
    {
        var chamado = await servico.ConsultarAsync(id, ct);
        if (chamado is null || !PodeVer(chamado, conta)) return NotFound();
        var r = chamado.Resumo;
        return View("Detalhes", new DetalhesView(chamado, form,
            User.IsInRole(Perfis.Tecnico) && r.Status == StatusChamado.Aberto,
            User.IsInRole(Perfis.Tecnico) && r.Status == StatusChamado.EmAtendimento && r.TecnicoId == conta.ParticipanteId,
            r.Status == StatusChamado.Resolvido && r.SolicitanteId == conta.ParticipanteId,
            PodeComentar(chamado, conta), comentario));
    }
}
