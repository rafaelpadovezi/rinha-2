using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rinha;
using Rinha.CompiledModels;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(builder.Configuration.GetConnectionString("AppDbContext"))
    .EnableThreadSafetyChecks(false)
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .UseModel(AppDbContextModel.Instance)
    .Options;
var pooledDbContextFactory = new PooledDbContextFactory<AppDbContext>(options); 

var app = builder.Build();

using (var context = pooledDbContextFactory.CreateDbContext())
{
    await AppDbContext.EfCustomWarmUp(context);
}

app.MapGet("/", () => Results.Ok(new { Message = "It's up" }));

app.MapGet("/clientes/{id:int}/extrato", async (int id) =>
{
    await using var context = pooledDbContextFactory.CreateDbContext();
    var saldo = await AppDbContext.GetSaldoCliente(context, id);
    if (saldo is null)
        return Results.NotFound();
    var ultimasTransacoes = new List<TransacaoDto>(10);
    await foreach (var transacao in AppDbContext.GetUltimasTransacoes(context, id))
    {
        ultimasTransacoes.Add(transacao);
    }

    return Results.Ok(new ExtratoDto
    {
        Saldo = saldo,
        UltimasTransacoes = ultimasTransacoes
    });
});

app.MapPost("/clientes/{id:int}/transacoes", async (int id, TransacaoRequestDto transacao) =>
{
    if (transacao.Tipo != 'c' && transacao.Tipo != 'd')
        return Results.UnprocessableEntity("Tipo inválido");
    if (!int.TryParse(transacao.Valor?.ToString(), out var valor))
        return Results.UnprocessableEntity("Valor inválido");
    if (string.IsNullOrEmpty(transacao.Descricao) || transacao.Descricao.Length > 10)
        return Results.UnprocessableEntity("Descrição inválida");

    await using var context = pooledDbContextFactory.CreateDbContext();
    var valorTransacao = transacao.Tipo == 'c' ? valor : valor * -1;

    await using var dbTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    var updated = await AppDbContext.TryUpdateSaldoCliente(context, id, valorTransacao);

    var cliente = await AppDbContext.GetCliente(context, id);
    if (cliente is null)
        return Results.NotFound();
    if (!updated)
        return Results.UnprocessableEntity("Limite excedido");
    context.Transacaos.Add(new Transacao
    {
        Valor = valor,
        Tipo = transacao.Tipo,
        Descricao = transacao.Descricao,
        ClienteId = id
    });
    await context.SaveChangesAsync();
    await dbTransaction.CommitAsync();

    return Results.Ok(new ResultadoSaldo(cliente.SaldoInicial, cliente.Limite));
});

app.Run();

[JsonSerializable(typeof(ExtratoDto))]
[JsonSerializable(typeof(SaldoDto))]
[JsonSerializable(typeof(ResultadoSaldo))]
[JsonSerializable(typeof(TransacaoDto))]
[JsonSerializable(typeof(TransacaoRequestDto))]
internal partial class AppJsonSerializerContext : JsonSerializerContext {}