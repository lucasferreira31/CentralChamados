using Microsoft.AspNetCore.Identity;
namespace CentralChamados.Persistence;

/// <summary>A conta autentica; o participante preserva os vínculos e o histórico do domínio.</summary>
public sealed class Conta : IdentityUser<Guid>
{
    public Guid ParticipanteId { get; set; }
}
public static class Perfis
{
    public const string Solicitante = "Solicitante";
    public const string Tecnico = "Tecnico";
}
