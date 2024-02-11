using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Rinha.Controllers;

[ApiController]
[Route("[controller]s")]
public class ClienteController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClienteController(AppDbContext context)
    {
        _context = context;
    }
    
    // https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics?tabs=with-di%2Cexpression-api-with-constant#compiled-queries
    private static readonly Func<AppDbContext, int, Task<SaldoDto?>> GetSaldoCliente
        = EF.CompileAsyncQuery(
            (AppDbContext context, int id) => context.Clientes
                .Where(x => x.Id == id)
                .Select(x => new SaldoDto
                {
                    Total = x.SaldoInicial,
                    Limite = x.Limite
                })
                .FirstOrDefault());

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<TransacaoDto>> GetUltimasTransacoes
        = EF.CompileAsyncQuery(
            (AppDbContext context, int id) => context.Transacaos
                .Where(x => x.ClienteId == id)
                .OrderByDescending(x => x.Id)
                .Take(10)
                .Select(x => new TransacaoDto
                {
                    Valor = x.Tipo == 'c' ? x.Valor : x.Valor * -1,
                    Tipo = x.Tipo,
                    Descricao = x.Descricao,
                    RealizadoEm = x.RealizadoEm
                }));

    [HttpGet("{id:int}/extrato")]
    public async Task<IActionResult> Extrato(int id)
    {
        var saldo = await GetSaldoCliente(_context, id);
        if (saldo is null)
            return NotFound();
        var ultimasTransacoes = new List<TransacaoDto>(10);
        await foreach (var transacao in GetUltimasTransacoes(_context, id))
        {
            ultimasTransacoes.Add(transacao);
        }
        return Ok(new ExtratoDto
        {
            Saldo = saldo,
            UltimasTransacoes = ultimasTransacoes
        });
    }

    [HttpPost("{id:int}/transacoes")]
    public async Task<IActionResult> Post(int id, TransacaoRequestDto transacao)
    {
        var valorTransacao = transacao.Tipo == 'c' ? transacao.Valor : transacao.Valor * -1;

        using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        
        var cliente = await _context.Clientes.FromSql($"SELECT * FROM \"Clientes\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (cliente is null)
            return NotFound();
        cliente.SaldoInicial += valorTransacao;

        if (transacao.Tipo == 'd' && cliente.SaldoInicial * -1 > cliente.Limite)
        {
            return UnprocessableEntity("Limite excedido");
        }

        _context.Transacaos.Add(new Transacao
        {
            Valor = valorTransacao,
            Tipo = transacao.Tipo,
            Descricao = transacao.Descricao,
            ClienteId = id
        });
        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Ok(new
        {
            cliente.Limite,
            Saldo = cliente.SaldoInicial
        });
    }
}