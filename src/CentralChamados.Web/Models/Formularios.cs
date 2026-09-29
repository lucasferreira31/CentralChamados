using System.ComponentModel.DataAnnotations;
using CentralChamados.Persistence;
using CentralChamados.Domain;
namespace CentralChamados.Web.Models;

public sealed class AberturaForm
{
    [EnumDataType(typeof(PrioridadeChamado), ErrorMessage = "Selecione uma prioridade válida.")]
    public PrioridadeChamado Prioridade { get; set; } = PrioridadeChamado.Normal;
    [Required(ErrorMessage = "Informe o título."), StringLength(100, ErrorMessage = "Use até 100 caracteres.")]
    public string Titulo { get; set; } = "";
    [Required(ErrorMessage = "Descreva o problema."), StringLength(2000, ErrorMessage = "Use até 2000 caracteres.")]
    public string Descricao { get; set; } = "";
}
public sealed class AcaoForm
{
    [StringLength(2000, ErrorMessage = "Use até 2000 caracteres.")]
    public string? Texto { get; set; }
}
public sealed record DetalhesView(ChamadoConsulta Chamado, AcaoForm Acao,
    bool PodeIniciar, bool PodeResolver, bool PodeReabrir, bool PodeComentar, string Comentario = "");
public sealed class ComentarioForm
{
    [Required(ErrorMessage = "Escreva um comentário."), StringLength(2000, ErrorMessage = "Use até 2000 caracteres.")]
    public string Comentario { get; set; } = "";
}
public sealed class ConsultaForm
{
    [StringLength(100)]
    public string? Busca { get; set; }
    [EnumDataType(typeof(StatusChamado))]
    public StatusChamado? Status { get; set; }
    [EnumDataType(typeof(PrioridadeChamado))]
    public PrioridadeChamado? Prioridade { get; set; }
    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;
}
public sealed record ListaView(PaginaChamados Resultado);
public sealed class CadastroForm
{
    [Required(ErrorMessage = "Informe seu nome."), StringLength(100, ErrorMessage = "Use até 100 caracteres.")]
    public string Nome { get; set; } = "";
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido."),
     StringLength(256, ErrorMessage = "Use até 256 caracteres.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe uma senha."), StringLength(128, MinimumLength = 10, ErrorMessage = "Use entre 10 e 128 caracteres."),
     DataType(DataType.Password)]
    public string Senha { get; set; } = "";
    [Required(ErrorMessage = "Confirme a senha."), Compare(nameof(Senha), ErrorMessage = "As senhas não coincidem."), DataType(DataType.Password)]
    public string ConfirmarSenha { get; set; } = "";
}
public sealed class LoginForm
{
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe sua senha."), DataType(DataType.Password)]
    public string Senha { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
