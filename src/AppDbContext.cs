using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Rinha;

public record Cliente
{
    public int Id { get; set; }
    public int Limite { get; set; }
    public int SaldoInicial { get; set; }
    public List<Transacao> Transacoes { get; set; }
}

public record Transacao
{
    public int Id { get; set; }
    public int Valor { get; set; }
    public int ClienteId { get; set; }
    public char Tipo { get; set; }
    public string Descricao { get; set; } = "";
    public DateTime RealizadoEm { get; set; }
}

[RequiresUnreferencedCode("Not compatible with trimming")]
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
    
    public static async Task<bool> TryUpdateSaldoCliente(AppDbContext context, int id, int valorTransacao)
    {
        var result = await context.Clientes
            .Where(x => x.Id == id)
            .Where(x => x.SaldoInicial + valorTransacao >= x.Limite * -1 || valorTransacao > 0)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(e => e.SaldoInicial, e => e.SaldoInicial + valorTransacao));
        return result > 0;
    }
    
    /// <summary>
    /// Executa algumas queries para garantir a inicialização do EF Core.
    ///
    /// A primeira execução de algumas funcionalides do EF Core pode ser lenta. Compiled queries
    /// ajuda otimizar mas tem suporte limitado.
    /// </summary>
    /// <param name="appDbContext"></param>
    public static async Task EfCustomWarmUp(AppDbContext appDbContext)
    {
        try
        {
            await TryUpdateSaldoCliente(appDbContext, 0, 1);
            await appDbContext.AddAsync(new Transacao
            {
                ClienteId = 0,
                Descricao = "Teste",
                Tipo = 'c',
                Valor = 100
            });
            await appDbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
        }
    }
}