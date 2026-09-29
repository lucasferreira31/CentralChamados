using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace CentralChamados.Persistence;

public sealed class FabricaChamadosDb(string caminhoBanco) : IDbContextFactory<ChamadosDbContext>
{
    public ChamadosDbContext CreateDbContext()
    {
        var caminho = Path.GetFullPath(caminhoBanco);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        var cs = new SqliteConnectionStringBuilder
        { DataSource = caminho, ForeignKeys = true, Pooling = false, DefaultTimeout = 3 }.ToString();
        return new ChamadosDbContext(new DbContextOptionsBuilder<ChamadosDbContext>().UseSqlite(cs).Options);
    }
}
public sealed class FabricaDesignTime : IDesignTimeDbContextFactory<ChamadosDbContext>
{
    public ChamadosDbContext CreateDbContext(string[] args)
        => new FabricaChamadosDb(Environment.GetEnvironmentVariable("CHAMADOS_DB")
            ?? Path.Combine(Directory.GetCurrentDirectory(), ".dados", "chamados.db")).CreateDbContext();
}
