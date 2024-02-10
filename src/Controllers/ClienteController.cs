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

    [HttpGet("{id:int}/extrato")]
    public async Task<IActionResult> Extrato(int id)
    {
        var saldo = await _context.Clientes
            .Where(x => x.Id == id)
            .Select(x => new SaldoDto
            {
                Total = x.SaldoInicial,
                Limite = x.Limite
            })
            .FirstOrDefaultAsync();
        if (saldo is null)
            return NotFound();
        var ultimasTransacoes = await _context.Transacaos
            .Where(x => x.ClienteId == id)
            .OrderByDescending(x => x.Id)
            .Take(10)
            .Select(x => new TransacaoDto
            {
                Valor = x.Tipo == 'c' ? x.Valor : x.Valor * -1,
                Tipo = x.Tipo,
                Descricao = x.Descricao,
                RealizadoEm = x.RealizadoEm
            })
            .ToListAsync();
        return Ok(new ExtratoDto
        {
            Saldo = saldo,
            UltimasTransacoes = ultimasTransacoes
        });
    }

    [HttpPost("{id:int}/transacoes")]
    public async Task<IActionResult> Post(int id, TransacaoRequestDto transacao)
    {
        var saldoFinal = 0;
        var limite = 0;
        var valorTransacao = transacao.Tipo == 'c' ? transacao.Valor : transacao.Valor * -1;

        using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        _context.Transacaos.Add(new Transacao
        {
            Valor = valorTransacao,
            Tipo = transacao.Tipo,
            Descricao = transacao.Descricao,
            ClienteId = id
        });
        await _context.SaveChangesAsync();

        var result = await _context.Clientes
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(e => e.SaldoInicial, e => e.SaldoInicial + valorTransacao));
        
        if (result == 0)
            return NotFound();

        if (transacao.Tipo == 'd')
        {
            var cliente = await _context.Clientes.FindAsync(id);
            saldoFinal = cliente.SaldoInicial;
            limite = cliente.Limite;
            if (cliente.SaldoInicial * -1 > cliente.Limite)
            {
                await dbTransaction.RollbackAsync();
                return UnprocessableEntity("Limite excedido");
            }
        }
        await dbTransaction.CommitAsync();

        if (transacao.Tipo == 'c')
        {
            var cliente = await _context.Clientes.FindAsync(id);
            saldoFinal = cliente.SaldoInicial;
            limite = cliente.Limite;
        }
        return Ok(new
        {
            Limite = limite,
            Saldo = saldoFinal
        });
    }
}