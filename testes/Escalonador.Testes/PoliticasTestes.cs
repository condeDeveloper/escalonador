using Conde.Escalonador;
using Xunit;

namespace Conde.Escalonador.Testes;

public class PoliticasTestes
{
    /// <summary>
    /// Com todas as tarefas do MESMO TAMANHO, as politicas empatam na espera.
    ///
    /// E por isso que medir escalonamento com carga uniforme nao descobre nada: o
    /// que separa as politicas e a VARIACAO de duracao.
    /// </summary>
    [Fact]
    public void ComTarefasDoMesmoTamanhoAsPoliticasEmpatam()
    {
        var tarefas = Enumerable.Range(0, 20).Select(i => new Tarefa(i, 0, 10)).ToList();

        var fifo = Simulacao.Rodar(tarefas, Politica.Fifo()).EsperaMedia();
        var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();

        Assert.Equal(fifo, menor, 9);
    }

    /// <summary>
    /// O EFEITO COMBOIO: uma tarefa de cem passos na frente de vinte de um passo.
    ///
    /// No FIFO, todas as vinte esperam o relatorio inteiro, e a espera media e dez
    /// vezes a do menor primeiro. E o caso comum, nao o exotico: uma consulta
    /// pesada na frente de mil consultas de um milissegundo.
    /// </summary>
    [Fact]
    public void OComboioDerrubaOFifo()
    {
        var tarefas = Cargas.Comboio(20);

        var fifo = Simulacao.Rodar(tarefas, Politica.Fifo()).EsperaMedia();
        var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();

        Assert.True(fifo > 10 * menor, $"FIFO {fifo:F1}, menor primeiro {menor:F1}");
    }

    /// <summary>
    /// E o ROUND-ROBIN sobrevive ao comboio sem saber nada sobre as duracoes: ele
    /// nao precisa adivinhar o futuro, so reparte o processador.
    /// </summary>
    [Fact]
    public void ORoundRobinSobreviveAoComboioSemSaberAsDuracoes()
    {
        var tarefas = Cargas.Comboio(20);

        var fifo = Simulacao.Rodar(tarefas, Politica.Fifo()).EsperaMedia();
        var rodizio = Simulacao.Rodar(tarefas, Politica.RoundRobin(1)).EsperaMedia();
        var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();

        Assert.True(rodizio < fifo / 5, $"rodizio {rodizio:F1}, FIFO {fifo:F1}");
        Assert.True(rodizio >= menor, "o rodizio nao pode ganhar do otimo");
    }

    /// <summary>
    /// O ROUND-ROBIN tem o melhor tempo de RESPOSTA de longe, e e a unica metrica
    /// em que ele ganha.
    /// </summary>
    [Fact]
    public void ORoundRobinGanhaNaResposta()
    {
        var tarefas = Cargas.CaudaLonga(50);
        var rodizio = Simulacao.Rodar(tarefas, Politica.RoundRobin(1)).RespostaMedia();

        foreach (var politica in new[] { Politica.Fifo(), Politica.MenorPrimeiro(), Politica.MenorRestante() })
            Assert.True(rodizio < Simulacao.Rodar(tarefas, politica).RespostaMedia(), politica.Nome);
    }

    /// <summary>
    /// E ele PERDE no retorno, que e o outro lado do mesmo trato: repartir o
    /// processador atrasa todo mundo um pouco em vez de atrasar alguns muito.
    /// </summary>
    [Fact]
    public void ORoundRobinPerdeNoRetorno()
    {
        var tarefas = Cargas.CaudaLonga(50);

        var rodizio = Simulacao.Rodar(tarefas, Politica.RoundRobin(1)).RetornoMedio();
        var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).RetornoMedio();

        Assert.True(rodizio > 1.5 * menor, $"rodizio {rodizio:F1}, menor primeiro {menor:F1}");
    }

    /// <summary>
    /// O QUANTUM GRANDE vira FIFO, literalmente: com uma fatia maior que a maior
    /// tarefa, o rodizio da a mesma linha do tempo.
    /// </summary>
    [Fact]
    public void OQuantumGrandeViraFifo()
    {
        var tarefas = Cargas.CaudaLonga(40);
        var maior = tarefas.Max(t => t.Duracao);

        var fifo = Simulacao.Rodar(tarefas, Politica.Fifo());
        var rodizio = Simulacao.Rodar(tarefas, Politica.RoundRobin(maior + 1));

        Assert.Equal(fifo.LinhaDoTempo, rodizio.LinhaDoTempo);
    }

    /// <summary>
    /// E o QUANTUM PEQUENO custa uma troca de contexto por passo: o preco da
    /// resposta boa esta na coluna do lado.
    /// </summary>
    [Fact]
    public void OQuantumPequenoCustaUmaTrocaPorPasso()
    {
        var tarefas = Cargas.CaudaLonga(50);

        var anteriorTrocas = int.MaxValue;
        var anteriorResposta = 0.0;

        foreach (var quantum in new[] { 1, 2, 4, 8, 16, 32, 64 })
        {
            var execucao = Simulacao.Rodar(tarefas, Politica.RoundRobin(quantum));

            Assert.True(execucao.Trocas <= anteriorTrocas, $"quantum {quantum} trocou mais");
            Assert.True(execucao.RespostaMedia() >= anteriorResposta, $"quantum {quantum} respondeu mais rapido");

            anteriorTrocas = execucao.Trocas;
            anteriorResposta = execucao.RespostaMedia();
        }
    }

    /// <summary>
    /// O MENOR RESTANTE, com preempcao, nunca perde do MENOR PRIMEIRO em espera
    /// media: ele e a versao preemptiva do mesmo criterio.
    /// </summary>
    [Fact]
    public void OMenorRestanteNuncaPerdeDoMenorPrimeiro()
    {
        foreach (var (nome, tarefas) in Cargas.Todas())
        {
            var preemptivo = Simulacao.Rodar(tarefas, Politica.MenorRestante()).EsperaMedia();
            var simples = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();

            Assert.True(preemptivo <= simples + 1e-9, $"{nome}: preemptivo {preemptivo:F2}, simples {simples:F2}");
        }
    }

    /// <summary>
    /// E com todas chegando juntas os dois sao IGUAIS: sem chegada nova, nao ha o
    /// que interromper.
    /// </summary>
    [Fact]
    public void ComTodasJuntasOsDoisSaoIguais()
    {
        for (ulong semente = 1; semente <= 50; semente++)
        {
            var tarefas = Cargas.MinusculaJunta(8, semente);

            Assert.Equal(
                Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia(),
                Simulacao.Rodar(tarefas, Politica.MenorRestante()).EsperaMedia(),
                9);
        }
    }

    /// <summary>
    /// Nenhuma politica ganha NAS TRES metricas ao mesmo tempo, e e isso que faz do
    /// escalonamento uma escolha e nao um problema resolvido.
    /// </summary>
    [Fact]
    public void NenhumaPoliticaGanhaNasTresMetricas()
    {
        var tarefas = Cargas.CaudaLonga(50);

        var execucoes = Politica.Todas().Select(p => (p.Nome, Execucao: Simulacao.Rodar(tarefas, p))).ToList();

        var melhorEspera = execucoes.MinBy(e => e.Execucao.EsperaMedia()).Nome;
        var melhorResposta = execucoes.MinBy(e => e.Execucao.RespostaMedia()).Nome;

        Assert.NotEqual(melhorEspera, melhorResposta);
    }

    [Fact]
    public void CadaPoliticaTemNomeESeImprimePeloNome()
    {
        foreach (var politica in Politica.Todas())
        {
            Assert.False(string.IsNullOrWhiteSpace(politica.Nome));
            Assert.Equal(politica.Nome, politica.ToString());
        }

        Assert.Equal(6, Politica.Todas().Count());
    }
}
