namespace CentralChamados.Domain;

/// <summary>Participante do atendimento. Ainda não representa uma conta autenticada.</summary>
public sealed class Usuario
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Nome { get; private set; } = "";
    private Usuario() { }
    public Usuario(string nome) => Renomear(nome);
    public void Renomear(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length > 100)
            throw new RegraDeNegocioException("Nome é obrigatório e deve ter até 100 caracteres.");
        Nome = nome.Trim();
    }
}
