using System.ComponentModel.DataAnnotations;

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

public record TransacaoDto
{
    public int Valor { get; set; }
    public char Tipo { get; set; }
    public string Descricao { get; set; } = "";
    public DateTime RealizadoEm { get; set; }
}

public record struct TransacaoRequestDto
{
    [Range(1, int.MaxValue)]
    public int Valor { get; set; }
    [AllowedValues('c', 'd')]
    public char Tipo { get; set; }
    [Required]
    [MaxLength(10)]
    public string Descricao { get; set; }
}