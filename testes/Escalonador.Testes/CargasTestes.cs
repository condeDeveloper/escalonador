using Conde.Escalonador;
using Xunit;

namespace Conde.Escalonador.Testes;

/// <summary>
/// As CARGAS tambem precisam de teste.
///
/// Toda tabela deste repositorio e "esta politica, nesta carga". Se a carga nao
/// fosse o que o nome dela diz, as conclusoes seriam sobre outra coisa. O exemplo
/// que mais importa: uma carga com todas as duracoes iguais nao separa politica
/// nenhuma, e uma carga de cauda longa separa todas.
/// </summary>
public class CargasTestes
{
    [Fact]
    public void AMesmaSementeDaAMesmaCarga()
    {
        Assert.Equal(Cargas.CaudaLonga(50), Cargas.CaudaLonga(50));
        Assert.Equal(Cargas.ComChegadas(50), Cargas.ComChegadas(50));
        Assert.Equal(Cargas.ComPrioridade(50), Cargas.ComPrioridade(50));
        Assert.Equal(Cargas.Minuscula(6, 3), Cargas.Minuscula(6, 3));
    }

    [Fact]
    public void TodaCargaTemIdentificadoresDistintosEDuracaoPositiva()
    {
        foreach (var (nome, tarefas) in Cargas.Todas())
        {
            Assert.Equal(tarefas.Count, tarefas.Select(t => t.Id).Distinct().Count());
            Assert.All(tarefas, t => Assert.True(t.Duracao > 0, nome));
            Assert.All(tarefas, t => Assert.True(t.Chegada >= 0, nome));
        }
    }

    /// <summary>
    /// A UNIFORME tem todas as duracoes quase iguais: e a carga que nao separa
    /// nada, e ela esta aqui exatamente por isso.
    /// </summary>
    [Fact]
    public void AUniformeTemDuracoesQuaseIguais()
    {
        var tarefas = Cargas.Uniforme(50);

        Assert.True(tarefas.Max(t => t.Duracao) - tarefas.Min(t => t.Duracao) <= 2);
        Assert.All(tarefas, t => Assert.Equal(0, t.Chegada));
    }

    /// <summary>
    /// A CAUDA LONGA tem a mediana pequena e o maximo dezenas de vezes maior, que
    /// e a forma de toda carga de verdade.
    /// </summary>
    [Fact]
    public void ACaudaLongaTemMedianaPequenaEMaximoGrande()
    {
        var tarefas = Cargas.CaudaLonga(200);
        var ordenadas = tarefas.Select(t => t.Duracao).OrderBy(d => d).ToList();

        var mediana = ordenadas[ordenadas.Count / 2];

        Assert.True(mediana <= 5, $"a mediana foi {mediana}");
        Assert.True(ordenadas[^1] > 10 * mediana, $"o maximo foi {ordenadas[^1]}");
        Assert.InRange(tarefas.Count(t => t.Duracao >= 50) / (double)tarefas.Count, 0.03, 0.2);
    }

    /// <summary>
    /// O COMBOIO tem uma tarefa longa na frente e muitas curtas atras, todas
    /// chegando juntas.
    /// </summary>
    [Fact]
    public void OComboioTemUmaLongaNaFrente()
    {
        var tarefas = Cargas.Comboio(20);

        Assert.Equal(21, tarefas.Count);
        Assert.Equal(100, tarefas[0].Duracao);
        Assert.All(tarefas.Skip(1), t => Assert.Equal(1, t.Duracao));
        Assert.All(tarefas, t => Assert.Equal(0, t.Chegada));
    }

    /// <summary>
    /// A carga COM PRIORIDADE e SATURADA de proposito: chega mais trabalho do que
    /// da para fazer na janela. Sem isso a fome nao existe.
    /// </summary>
    [Fact]
    public void ACargaComPrioridadeEhSaturada()
    {
        var tarefas = Cargas.ComPrioridade(250);
        var trabalho = tarefas.Sum(t => t.Duracao);
        var janela = tarefas.Max(t => t.Chegada);

        Assert.True(trabalho > janela, $"trabalho {trabalho}, janela {janela}");
        Assert.InRange(tarefas.Count(t => t.Prioridade == 0) / (double)tarefas.Count, 0.03, 0.2);
        Assert.Equal(5, tarefas.Select(t => t.Prioridade).Distinct().Count());
    }

    /// <summary>
    /// E com a janela larga ela deixa de ser saturada, que e o controle do
    /// experimento da fome.
    /// </summary>
    [Fact]
    public void ComAJanelaLargaElaDeixaDeSerSaturada()
    {
        var tarefas = Cargas.ComPrioridade(250, janela: 3000);

        Assert.True(tarefas.Sum(t => t.Duracao) < tarefas.Max(t => t.Chegada));
    }

    /// <summary>
    /// As cargas MINUSCULAS cabem na forca bruta, e a de chegadas juntas tem todo
    /// mundo no instante zero.
    /// </summary>
    [Fact]
    public void AsCargasMinusculasCabemNaForcaBruta()
    {
        for (ulong semente = 1; semente <= 20; semente++)
        {
            var juntas = Cargas.MinusculaJunta(8, semente);
            var espalhadas = Cargas.Minuscula(8, semente);

            Assert.All(juntas, t => Assert.Equal(0, t.Chegada));
            Assert.Contains(espalhadas, t => t.Chegada > 0);

            Assert.Equal(8, juntas.Count);
            Assert.Equal(8, espalhadas.Count);
        }
    }

    [Fact]
    public void AsChegadasSaemOrdenadas()
    {
        foreach (var tarefas in new[] { Cargas.ComChegadas(100), Cargas.ComPrioridade(100), Cargas.Minuscula(9, 5) })
            for (var i = 1; i < tarefas.Count; i++)
                Assert.True(tarefas[i].Chegada >= tarefas[i - 1].Chegada);
    }
}
