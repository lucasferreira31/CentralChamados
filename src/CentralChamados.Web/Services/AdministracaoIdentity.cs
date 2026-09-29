using CentralChamados.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CentralChamados.Web.Services;

public static class AdministracaoIdentity
{
    public static async Task InicializarAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ChamadosDbContext>();
        await db.Database.MigrateAsync();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var perfil in new[] { Perfis.Solicitante, Perfis.Tecnico })
            if (!await roles.RoleExistsAsync(perfil))
                ExigirSucesso(await roles.CreateAsync(new IdentityRole<Guid>(perfil)));
    }

    public static async Task<bool> PromoverTecnicoAsync(IServiceProvider services, string email)
    {
        var contas = services.GetRequiredService<UserManager<Conta>>();
        var conta = await contas.FindByEmailAsync(email.Trim());
        if (conta is null) return false;
        ExigirSucesso(await contas.AddToRoleAsync(conta, Perfis.Tecnico));
        // Exige novo login e impede que uma sessão antiga mantenha permissões desatualizadas.
        ExigirSucesso(await contas.UpdateSecurityStampAsync(conta));
        return true;
    }

    private static void ExigirSucesso(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new InvalidOperationException("Operação administrativa falhou: " +
                string.Join(", ", resultado.Errors.Select(e => e.Code)));
    }
}
