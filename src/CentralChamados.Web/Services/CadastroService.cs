using CentralChamados.Domain;
using CentralChamados.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CentralChamados.Web.Services;

public sealed class CadastroService(ChamadosDbContext db, UserManager<Conta> contas)
{
    public async Task<IdentityResult> CadastrarAsync(string nome, string email, string senha, CancellationToken ct)
    {
        // O participante, a conta e o perfil são criados juntos ou inteiramente desfeitos.
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var participante = new Usuario(nome);
        db.Usuarios.Add(participante);
        var conta = new Conta { Id = Guid.NewGuid(), UserName = email.Trim(), Email = email.Trim(),
            ParticipanteId = participante.Id };
        var resultado = await contas.CreateAsync(conta, senha);
        if (!resultado.Succeeded) return resultado;
        resultado = await contas.AddToRoleAsync(conta, Perfis.Solicitante);
        if (!resultado.Succeeded) return resultado;
        await transacao.CommitAsync(ct);
        return IdentityResult.Success;
    }
}
