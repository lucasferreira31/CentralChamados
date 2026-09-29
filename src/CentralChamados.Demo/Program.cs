using CentralChamados.Domain;

Console.WriteLine("CENTRAL DE CHAMADOS | Etapa 1: ciclo de atendimento");
var lucas = new Usuario("Lucas");
var ana = new Usuario("Ana");
var inicio = DateTimeOffset.UtcNow;
var chamado = new Chamado("Impressora indisponível", "A equipe não consegue imprimir relatórios.", lucas, inicio);
chamado.IniciarAtendimento(ana, inicio.AddMinutes(1));
try
{
    chamado.Resolver(new Usuario("Outro técnico"), "Reiniciar equipamento", inicio.AddMinutes(2));
}
catch (RegraDeNegocioException erro)
{
    Console.WriteLine($"Operação recusada: {erro.Message}");
}
chamado.Resolver(ana, "Conexão de rede restabelecida.", inicio.AddMinutes(3));
Console.WriteLine($"Status: {chamado.Status}");
foreach (var evento in chamado.Historico)
    Console.WriteLine($"{evento.Em:HH:mm} | {evento.Autor} | {evento.Descricao}");
Console.WriteLine("Demonstração em memória. A interface web será construída em uma próxima etapa.");
