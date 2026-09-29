using CentralChamados.Persistence;
using Microsoft.EntityFrameworkCore;

var comando = args.FirstOrDefault() ?? "ajuda";
if (comando is not ("init" or "demo" or "listar"))
{
    Console.WriteLine("Uso: dotnet run --project src/CentralChamados.BancoDemo -- init|demo|listar [caminhoBanco]");
    return;
}
var caminho = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), ".dados", "chamados.db");
try
{
    var fabrica = new FabricaChamadosDb(caminho);
    var service = new ChamadosService(fabrica);
    if (comando == "init")
    {
        await using var db = fabrica.CreateDbContext();
        await db.Database.MigrateAsync();
        Console.WriteLine($"Banco inicializado: {Path.GetFullPath(caminho)}");
        return;
    }
    if (comando == "demo")
    {
        // IDs exclusivos para esta execução: nomes não são identificadores.
        var solicitante = await service.CadastrarUsuarioAsync("Lucas (demonstração)");
        var tecnico = await service.CadastrarUsuarioAsync("Ana (demonstração)");
        var id = await service.AbrirAsync("Impressora indisponível", "Falha ao imprimir relatórios.", solicitante);
        await service.IniciarAsync(id, tecnico);
        await service.RenomearUsuarioAsync(tecnico, "Ana Silva (demonstração)");
        await service.ResolverAsync(id, tecnico, "Conexão de rede restabelecida.");
        await service.ReabrirAsync(id, solicitante, "O problema voltou.");
        Console.WriteLine($"Criado e reaberto: {id}. O histórico preserva o nome anterior da técnica.");
    }
    foreach (var resumo in await service.ListarAsync())
    {
        var detalhe = (await service.ConsultarAsync(resumo.Id))!;
        Console.WriteLine($"{resumo.Id} | {resumo.Status} | {resumo.Titulo} | {resumo.SolicitanteAtual}");
        foreach (var evento in detalhe.Historico)
            Console.WriteLine($"  {evento.Sequencia}: {evento.Em:O} | {evento.Autor} | {evento.Descricao}");
    }
}
catch (Exception ex) when (ex is CentralChamados.Domain.RegraDeNegocioException or KeyNotFoundException
    or Microsoft.Data.Sqlite.SqliteException or DbUpdateException)
{
    Console.Error.WriteLine("Não foi possível concluir. Execute init antes de usar o banco e confira os dados. " + ex.Message);
    Environment.ExitCode = 1;
}
