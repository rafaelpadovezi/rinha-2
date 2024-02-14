using Microsoft.EntityFrameworkCore;

namespace Rinha;

public record Cliente
{
    public int Id { get; set; }
    public int Limite { get; set; }
    public int SaldoInicial { get; set; }
    public List<Transacao> Transacoes { get; set; } = [];
}

public record Transacao
{
    public int Id { get; set; }
    public int Valor { get; set; }
    public int ClienteId { get; set; }
    public char Tipo { get; set; }
    public string Descricao { get; set; } = "";
    public DateTime RealizadoEm { get; set; } = DateTime.UtcNow;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Transacao> Transacaos => Set<Transacao>();

    // https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics?tabs=with-di%2Cexpression-api-with-constant#compiled-queries
    public static readonly Func<AppDbContext, int, Task<SaldoDto?>> GetSaldoCliente
        = EF.CompileAsyncQuery(
            (AppDbContext context, int id) => context.Clientes
                .Where(x => x.Id == id)
                .Select(x => new SaldoDto
                {
                    Total = x.SaldoInicial,
                    Limite = x.Limite
                })
                .FirstOrDefault());

    public static readonly Func<AppDbContext, int, IAsyncEnumerable<TransacaoDto>> GetUltimasTransacoes
        = EF.CompileAsyncQuery(
            (AppDbContext context, int id) => context.Transacaos
                .Where(x => x.ClienteId == id)
                .OrderByDescending(x => x.Id)
                .Take(10)
                .Select(x => new TransacaoDto
                {
                    Valor = x.Valor,
                    Tipo = x.Tipo,
                    Descricao = x.Descricao,
                    RealizadoEm = x.RealizadoEm
                }));
    
    public static readonly Func<AppDbContext, int, Task<Cliente?>> GetCliente
        = EF.CompileAsyncQuery(
            (AppDbContext context, int id) => context.Clientes
                .AsNoTracking()
                .SingleOrDefault(x => x.Id == id));
}