using Conde.Escalonador;
using Xunit;

namespace Conde.Escalonador.Testes;

/// <summary>
/// Os testes das TABELAS que o medidor imprime.
///
/// Eles existem porque numero em README que ninguem confere envelhece errado em
/// silencio: alguem troca uma constante, a tabela muda e o texto continua dizendo
/// a frase antiga. Aqui o teste afirma sobre o mesmo objeto que vira linha
/// impressa.
/// </summary>
public class MedidasTestes
{
    /// <summary>
    /// A linha principal: com todas chegando juntas, o menor primeiro bate com o
    /// otimo na virgula; com chegadas espalhadas, nao bate.
    /// </summary>
    [Fact]
    public void OJuizSeparaAsDuasCondicoesDoTeorema()
    {
        var linhas = Medidas.Juiz(20);

        Assert.Equal(6, linhas.Count);

        foreach (var l in linhas.Where(l => l.Chegadas == "todas juntas"))
            Assert.Equal(l.Otimo, l.MenorPrimeiro, 9);

        foreach (var l in linhas.Where(l => l.Chegadas == "espalhadas"))
            Assert.True(l.MenorPrimeiro > l.Otimo, $"{l.Tarefas} tarefas: otimo {l.Otimo:F2}, menor {l.MenorPrimeiro:F2}");
    }

    /// <summary>
    /// E o FIFO fica no meio do caminho entre o otimo e a pior ordem possivel, em
    /// toda linha.
    /// </summary>
    [Fact]
    public void OFifoFicaEntreOOtimoEAPiorOrdem()
    {
        foreach (var l in Medidas.Juiz(20))
        {
            Assert.True(l.Fifo > l.Otimo, $"{l.Chegadas}, {l.Tarefas} tarefas");
            Assert.True(l.Fifo < l.Pior, $"{l.Chegadas}, {l.Tarefas} tarefas");
            Assert.Equal(ForcaBruta.QuantasOrdens(l.Tarefas), l.Ordens);
        }
    }

    [Fact]
    public void ATabelaDePoliticasTemUmaLinhaPorParDeCargaEPolitica()
    {
        var linhas = Medidas.Politicas();

        Assert.Equal(24, linhas.Count);
        Assert.Equal(4, linhas.Select(l => l.Carga).Distinct().Count());
        Assert.Equal(6, linhas.Select(l => l.Politica).Distinct().Count());
    }

    /// <summary>
    /// A frase do texto: na carga uniforme nenhuma politica se destaca, e e por
    /// isso que medir com carga uniforme nao descobre nada.
    /// </summary>
    [Fact]
    public void NaCargaUniformeNenhumaPoliticaSeDestaca()
    {
        var linhas = Medidas.Politicas().Where(l => l.Carga == "uniforme").ToList();

        var melhor = linhas.Min(l => l.Espera);
        var pior = linhas.Max(l => l.Espera);

        Assert.True(pior < melhor * 2.5, $"melhor {melhor:F1}, pior {pior:F1}");
    }

    /// <summary>
    /// A frase do texto: no comboio, o FIFO custa dez vezes o menor primeiro.
    /// </summary>
    [Fact]
    public void NoComboioOFifoCustaDezVezesOMenorPrimeiro()
    {
        var linhas = Medidas.Comboio();

        Assert.Equal(6, linhas.Count);

        var fifo = linhas.Single(l => l.Politica == "FIFO").Espera;
        var menor = linhas.Single(l => l.Politica == "menor primeiro").Espera;
        var rodizio = linhas.Single(l => l.Politica == "round-robin de 1").Espera;

        Assert.True(fifo > 10 * menor);
        Assert.True(rodizio < fifo / 5);
    }

    /// <summary>
    /// A frase do texto: quantum pequeno da resposta boa e gasta trocas de
    /// contexto; quantum grande vira FIFO.
    /// </summary>
    [Fact]
    public void OQuantumEhUmaTrocaEntreRespostaETrocasDeContexto()
    {
        var linhas = Medidas.Quanta();

        Assert.Equal(8, linhas.Count);

        for (var i = 1; i < linhas.Count; i++)
        {
            Assert.True(linhas[i].Resposta > linhas[i - 1].Resposta, $"quantum {linhas[i].Quantum}");
            Assert.True(linhas[i].Trocas <= linhas[i - 1].Trocas, $"quantum {linhas[i].Quantum}");
        }

        Assert.True(linhas[^1].Resposta > 10 * linhas[0].Resposta);
        Assert.True(linhas[0].Trocas > 10 * linhas[^1].Trocas);
    }

    /// <summary>
    /// A frase do texto: a melhor media vem com o pior desequilibrio.
    /// </summary>
    [Fact]
    public void AMelhorMediaVemComOPiorDesequilibrio()
    {
        var linhas = Medidas.Fomes();

        Assert.Equal(4, linhas.Count);

        var melhorMedia = linhas.MinBy(l => l.Espera)!;
        var piorDesequilibrio = linhas.MaxBy(l => l.Desequilibrio)!;

        Assert.Equal(melhorMedia.Politica, piorDesequilibrio.Politica);

        var fifo = linhas.Single(l => l.Politica == "FIFO");
        Assert.True(fifo.Desequilibrio < melhorMedia.Desequilibrio);
    }

    /// <summary>
    /// A frase do texto: o envelhecimento corta a pior espera da prioridade baixa
    /// pela metade e cobra uns poucos por cento da media.
    /// </summary>
    [Fact]
    public void OEnvelhecimentoCortaAPiorEsperaECobraPouco()
    {
        var linhas = Medidas.Prioridades();

        Assert.Equal(3, linhas.Count);

        var pura = linhas[0];
        var envelhece = linhas[1];

        Assert.True(envelhece.PiorEsperaDaBaixa < pura.PiorEsperaDaBaixa * 0.7,
            $"pura {pura.PiorEsperaDaBaixa}, com envelhecimento {envelhece.PiorEsperaDaBaixa}");

        Assert.True(envelhece.Espera < pura.Espera * 1.15,
            $"a media foi de {pura.Espera:F1} para {envelhece.Espera:F1}");

        // e envelhecer mais depressa ajuda mais a vitima
        Assert.True(linhas[2].PiorEsperaDaBaixa <= envelhece.PiorEsperaDaBaixa);
    }
}
