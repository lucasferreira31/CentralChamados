namespace CentralChamados.Domain;

public enum PrioridadeChamado { Baixa, Normal, Alta, Urgente }
public enum StatusChamado { Aberto, EmAtendimento, Resolvido }
public class RegraDeNegocioException(string mensagem) : Exception(mensagem);

/// <summary>Identidade estável e nome histórico do autor, capturados na transição.</summary>
public sealed class EventoChamado
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int Sequencia { get; private set; }
    public DateTimeOffset Em { get; private set; }
    public Guid AutorId { get; private set; }
    public string Autor { get; private set; } = "";
    public string Descricao { get; private set; } = "";
    private EventoChamado() { }
    internal EventoChamado(int sequencia, DateTimeOffset em, Usuario autor, string descricao)
    {
        Sequencia = sequencia; Em = em; AutorId = autor.Id; Autor = autor.Nome; Descricao = descricao;
    }
}

public sealed class Chamado
{
    private readonly List<EventoChamado> historico = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Titulo { get; private set; } = "";
    public string Descricao { get; private set; } = "";
    public Guid SolicitanteId { get; private set; }
    public string Solicitante { get; private set; } = "";
    public Guid? TecnicoId { get; private set; }
    public string? Tecnico { get; private set; }
    public StatusChamado Status { get; private set; } = StatusChamado.Aberto;
    public PrioridadeChamado Prioridade { get; private set; } = PrioridadeChamado.Normal;
    public string? Solucao { get; private set; }
    public IReadOnlyList<EventoChamado> Historico => historico.AsReadOnly();
    private Chamado() { } // Materialização pelo EF Core.

    public Chamado(string titulo, string descricao, Usuario solicitante, DateTimeOffset agora, PrioridadeChamado prioridade = PrioridadeChamado.Normal)
    {
        ArgumentNullException.ThrowIfNull(solicitante);
        if (!Enum.IsDefined(prioridade)) throw new RegraDeNegocioException("Prioridade inválida.");
        Prioridade = prioridade;
        Titulo = TextoObrigatorio(titulo, 100, "Título");
        Descricao = TextoObrigatorio(descricao, 2000, "Descrição");
        SolicitanteId = solicitante.Id;
        Solicitante = solicitante.Nome;
        Registrar(solicitante, "Chamado aberto.", agora);
    }
    public void IniciarAtendimento(Usuario tecnico, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(tecnico);
        if (Status != StatusChamado.Aberto)
            throw new RegraDeNegocioException("Somente chamados abertos podem iniciar atendimento.");
        ValidarHorario(agora);
        TecnicoId = tecnico.Id; Tecnico = tecnico.Nome; Status = StatusChamado.EmAtendimento;
        Registrar(tecnico, "Atendimento iniciado.", agora);
    }
    public void Resolver(Usuario tecnico, string solucao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(tecnico);
        if (Status != StatusChamado.EmAtendimento)
            throw new RegraDeNegocioException("O chamado precisa estar em atendimento para ser resolvido.");
        if (TecnicoId != tecnico.Id)
            throw new RegraDeNegocioException("Somente o técnico responsável pode resolver este chamado.");
        var texto = TextoObrigatorio(solucao, 2000, "Solução");
        ValidarHorario(agora);
        Solucao = texto; Status = StatusChamado.Resolvido;
        Registrar(tecnico, $"Chamado resolvido: {texto}", agora);
    }
    public void Reabrir(Usuario solicitante, string motivo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(solicitante);
        if (Status != StatusChamado.Resolvido)
            throw new RegraDeNegocioException("Somente chamados resolvidos podem ser reabertos.");
        if (SolicitanteId != solicitante.Id)
            throw new RegraDeNegocioException("Somente o solicitante pode reabrir este chamado.");
        var texto = TextoObrigatorio(motivo, 2000, "Motivo");
        ValidarHorario(agora);
        Status = StatusChamado.Aberto; TecnicoId = null; Tecnico = null; Solucao = null;
        Registrar(solicitante, $"Chamado reaberto: {texto}", agora);
    }
    public void Comentar(Usuario autor, string comentario, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(autor);
        if (autor.Id != SolicitanteId && autor.Id != TecnicoId)
            throw new RegraDeNegocioException("Somente o solicitante ou o técnico responsável pode comentar.");
        var texto = TextoObrigatorio(comentario, 2000, "Comentário");
        ValidarHorario(agora);
        Registrar(autor, $"Comentário: {texto}", agora);
    }
    private void Registrar(Usuario autor, string descricao, DateTimeOffset agora)
        => historico.Add(new EventoChamado(historico.Count, agora, autor, descricao));
    private void ValidarHorario(DateTimeOffset agora)
    {
        if (agora < historico[^1].Em)
            throw new RegraDeNegocioException("O evento não pode ocorrer antes do último registro.");
    }
    private static string TextoObrigatorio(string? texto, int limite, string campo)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > limite)
            throw new RegraDeNegocioException($"{campo} é obrigatório e deve ter até {limite} caracteres.");
        return texto.Trim();
    }
}
