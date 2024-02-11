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
}