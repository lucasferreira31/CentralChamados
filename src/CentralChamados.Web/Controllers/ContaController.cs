using CentralChamados.Persistence;
using CentralChamados.Web.Models;
using CentralChamados.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace CentralChamados.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContaController(CadastroService cadastro, SignInManager<Conta> login) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Cadastrar() => View(new CadastroForm());
    [AllowAnonymous, HttpPost, EnableRateLimiting("contas")]
    public async Task<IActionResult> Cadastrar(CadastroForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await cadastro.CadastrarAsync(form.Nome, form.Email, form.Senha, ct);
            if (resultado.Succeeded)
            {
                TempData["Sucesso"] = "Conta criada. Entre para abrir e acompanhar seus chamados.";
                return RedirectToAction(nameof(Entrar));
            }
            foreach (var erro in resultado.Errors)
                if (erro.Code.StartsWith("Password"))
                    ModelState.AddModelError(nameof(form.Senha), "Use uma senha com pelo menos 10 caracteres, maiúscula, minúscula, número e símbolo.");
                else
                    ModelState.AddModelError("", "Não foi possível cadastrar com os dados informados. Se já possui uma conta, entre.");
        }
        // Não devolve senhas no HTML, nem mesmo quando outro campo está inválido.
        form.Senha = form.ConfirmarSenha = "";

        return View(form);
    }
    [AllowAnonymous, HttpGet]
    public IActionResult Entrar(string? returnUrl = null) => View(new LoginForm { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost, EnableRateLimiting("contas")]
    public async Task<IActionResult> Entrar(LoginForm form)
    {
        if (ModelState.IsValid)
        {
            var resultado = await login.PasswordSignInAsync(form.Email.Trim(), form.Senha,
                isPersistent: false, lockoutOnFailure: true);
            if (resultado.Succeeded)
                return Url.IsLocalUrl(form.ReturnUrl) ? LocalRedirect(form.ReturnUrl!) : RedirectToAction("Index", "Chamados");
            ModelState.AddModelError("", "Não foi possível entrar. Confira os dados ou aguarde alguns minutos após várias tentativas.");
        }
        form.Senha = "";
        return View(form);
    }
    [Authorize, HttpPost]
    public async Task<IActionResult> Sair()
    {
        await login.SignOutAsync();
        return RedirectToAction(nameof(Entrar));
    }
    [AllowAnonymous, HttpGet]
    public IActionResult AcessoNegado() { Response.StatusCode = 403; return View(); }
    [AllowAnonymous, IgnoreAntiforgeryToken]
    public IActionResult Erro() { Response.StatusCode = 500; return View(); }
}
