using System.Threading.RateLimiting;
using CentralChamados.Persistence;
using CentralChamados.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
var banco = builder.Configuration["CHAMADOS_DB"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".dados", "chamados.db");
builder.Services.AddSingleton<IDbContextFactory<ChamadosDbContext>>(new FabricaChamadosDb(banco));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ChamadosDbContext>>().CreateDbContext());
builder.Services.AddScoped<ChamadosService>();
builder.Services.AddScoped<CadastroService>();
builder.Services.AddIdentity<Conta, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    // Envio e confirmação de e-mail ainda não fazem parte desta demonstração.
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<ChamadosDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CentralChamados.Sessao";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/Conta/Entrar";
    options.AccessDeniedPath = "/Conta/AcessoNegado";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("contas", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (args.Contains("--init"))
{
    using var scope = app.Services.CreateScope();
    await AdministracaoIdentity.InicializarAsync(scope.ServiceProvider);
    Console.WriteLine($"Banco e perfis preparados: {Path.GetFullPath(banco)}");
    return;
}
var promover = Array.IndexOf(args, "--promover-tecnico");
if (promover >= 0)
{
    if (promover + 1 >= args.Length || args[promover + 1].StartsWith("--"))
    {
        Console.Error.WriteLine("Informe o e-mail de uma conta já cadastrada após --promover-tecnico.");
        Environment.ExitCode = 1;
        return;
    }
    using var scope = app.Services.CreateScope();
    var sucesso = await AdministracaoIdentity.PromoverTecnicoAsync(scope.ServiceProvider, args[promover + 1]);
    Console.WriteLine(sucesso ? "Perfil Técnico concedido. Entre novamente para usar a nova permissão." : "Conta não encontrada.");
    Environment.ExitCode = sucesso ? 0 : 1;
    return;
}
app.UseExceptionHandler("/Conta/Erro");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Chamados}/{action=Index}/{id?}");
app.Run();
public partial class Program { }
