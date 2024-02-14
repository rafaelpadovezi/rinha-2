namespace Rinha;

public record ExtratoDto
{
    public SaldoDto Saldo { get; set; }
    public List<TransacaoDto> UltimasTransacoes { get; set; }
}

public record SaldoDto
{
    public int Total { get; set; }
    public DateTime DataExtrato { get; set; } = DateTime.Now;
    public int Limite { get; set; }
}

public record struct ResultadoSaldo(int Saldo, int Limite);

public record struct TransacaoDto
{
    public int Valor { get; set; }
    public char Tipo { get; set; }
    public string Descricao { get; set; }
    public DateTime RealizadoEm { get; set; }
}

public record struct TransacaoRequestDto
{
    public object Valor { get; set; }
    public char Tipo { get; set; }
    public string Descricao { get; set; }
}