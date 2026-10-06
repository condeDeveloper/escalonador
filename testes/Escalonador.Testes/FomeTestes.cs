using Conde.Escalonador;
using Xunit;

namespace Conde.Escalonador.Testes;

public class FomeTestes
{
    /// <summary>
    /// O ACHADO QUE A MEDIA ESCONDE: o menor primeiro tem a melhor media e a pior
    /// vitima.
    ///
    /// O desequilibrio e a pior espera dividida pela media. Uma politica com media
    /// otima e desequilibrio de treze e uma politica que faz uma tarefa esperar
    /// treze vezes o que a tarefa tipica espera.
    /// </summary>
    [Fact]
    public void OMenorPrimeiroTemAMelhorMediaEOPiorDesequilibrio()
    {
        var tarefas = Cargas.CaudaLonga(60, chegadaAte: 100);

        var fifo = Simulacao.Rodar(tarefas, Politica.Fifo());
        var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro());

        Assert.True(menor.EsperaMedia() < fifo.EsperaMedia() / 2);
        Assert.True(menor.Desequilibrio() > 3 * fifo.Desequilibrio());
    }

    /// <summary>
    /// A VITIMA do menor primeiro e sempre uma tarefa LONGA: ela e a que nunca
    /// chega a vez.
    /// </summary>
    [Fact]
    public void AVitimaDoMenorPrimeiroEhUmaTarefaLonga()
    {
        var tarefas = Cargas.CaudaLonga(60, chegadaAte: 100);
        var execucao = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro());

        var vitima = tarefas.MaxBy(execucao.Espera)!;
        var mediana = tarefas.OrderBy(t => t.Duracao).ElementAt(tarefas.Count / 2).Duracao;

        Assert.True(vitima.Duracao > 5 * mediana, $"a vitima durava {vitima.Duracao}, a mediana e {mediana}");
    }

    /// <summary>
    /// E o ROUND-ROBIN tem o desequilibrio menor entre as que nao sao FIFO: ele
    /// nao escolhe vitima.
    /// </summary>
    [Fact]
    public void ORoundRobinNaoEscolheVitima()
    {
        var tarefas = Cargas.CaudaLonga(60, chegadaAte: 100);

        var rodizio = Simulacao.Rodar(tarefas, Politica.RoundRobin(4)).Desequilibrio();
        var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).Desequilibrio();

        Assert.True(rodizio < menor, $"rodizio {rodizio:F1}, menor primeiro {menor:F1}");
    }

    /// <summary>
    /// A PRIORIDADE PURA deixa as tarefas de prioridade baixa para o fim, num
    /// sistema saturado.
    /// </summary>
    [Fact]
    public void APrioridadePuraDeixaAsBaixasParaOFim()
    {
        var tarefas = Cargas.ComPrioridade(250);
        var execucao = Simulacao.Rodar(tarefas, Politica.PorPrioridade());

        var baixas = tarefas.Where(t => t.Prioridade == 0).ToList();
        var altas = tarefas.Where(t => t.Prioridade >= 3).ToList();

        Assert.NotEmpty(baixas);
        Assert.True(baixas.Average(execucao.Espera) > 3 * altas.Average(execucao.Espera));
    }

    /// <summary>
    /// O ENVELHECIMENTO conserta a fome com UMA LINHA: somar a espera na prioridade
    /// efetiva.
    ///
    /// A pior espera de uma tarefa de prioridade baixa cai pela metade, e a media
    /// de todo mundo piora uns tres por cento. E o trato inteiro.
    /// </summary>
    [Fact]
    public void OEnvelhecimentoConsertaAFomePorUmPrecoPequeno()
    {
        var tarefas = Cargas.ComPrioridade(250);
        var baixas = tarefas.Where(t => t.Prioridade == 0).ToList();

        var pura = Simulacao.Rodar(tarefas, Politica.PorPrioridade());
        var envelhece = Simulacao.Rodar(tarefas, Politica.ComEnvelhecimento(10));

        var piorPura = baixas.Max(pura.Espera);
        var piorEnvelhece = baixas.Max(envelhece.Espera);

        Assert.True(piorEnvelhece < piorPura * 0.7, $"pura {piorPura}, com envelhecimento {piorEnvelhece}");
        Assert.True(envelhece.EsperaMedia() < pura.EsperaMedia() * 1.15,
            $"a media foi de {pura.EsperaMedia():F1} para {envelhece.EsperaMedia():F1}");
    }

    /// <summary>
    /// E envelhecer MAIS DEPRESSA ajuda mais a vitima e cobra mais da media: e um
    /// botao, nao um conserto de graca.
    /// </summary>
    [Fact]
    public void EnvelhecerMaisDepressaAjudaMaisECobraMais()
    {
        var tarefas = Cargas.ComPrioridade(250);
        var baixas = tarefas.Where(t => t.Prioridade == 0).ToList();

        var devagar = Simulacao.Rodar(tarefas, Politica.ComEnvelhecimento(10));
        var depressa = Simulacao.Rodar(tarefas, Politica.ComEnvelhecimento(3));

        Assert.True(baixas.Max(depressa.Espera) <= baixas.Max(devagar.Espera));
        Assert.True(depressa.EsperaMedia() >= devagar.EsperaMedia());
    }

    /// <summary>
    /// Num sistema NAO saturado nao ha fome nenhuma, e e por isso que o problema so
    /// aparece quando o sistema esta cheio: a fila esvazia sozinha e todo mundo
    /// roda.
    /// </summary>
    [Fact]
    public void SemSaturacaoNaoHaFome()
    {
        // chegadas espalhadas por dois mil passos, com umas setecentas unidades de
        // trabalho: sobra processador o tempo todo
        var tarefas = Cargas.ComPrioridade(250, janela: 3000);

        var pura = Simulacao.Rodar(tarefas, Politica.PorPrioridade());
        var baixas = tarefas.Where(t => t.Prioridade == 0).ToList();
        var altas = tarefas.Where(t => t.Prioridade >= 3).ToList();

        Assert.True(baixas.Average(pura.Espera) < 3 * altas.Average(pura.Espera) + 5);
    }

    /// <summary>
    /// O DESEQUILIBRIO de uma carga uniforme e perto de um: ninguem e vitima
    /// quando todo mundo e igual.
    /// </summary>
    [Fact]
    public void ComCargaUniformeNinguemEhVitima()
    {
        var tarefas = Enumerable.Range(0, 20).Select(i => new Tarefa(i, 0, 10)).ToList();
        var execucao = Simulacao.Rodar(tarefas, Politica.RoundRobin(1));

        Assert.InRange(execucao.Desequilibrio(), 1.0, 1.2);
    }
}
