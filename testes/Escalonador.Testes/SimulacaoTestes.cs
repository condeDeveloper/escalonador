using Conde.Escalonador;
using Xunit;

namespace Conde.Escalonador.Testes;

/// <summary>
/// O simulador tambem precisa de juiz.
///
/// Todo numero deste repositorio sai dele. Se ele contasse um passo a mais ou
/// deixasse uma tarefa rodar antes de chegar, todas as tabelas estariam erradas e
/// pareceriam plausiveis. Por isso cada execucao e conferida contra a LINHA DO
/// TEMPO, que e o registro de quem rodou em cada passo, e as metricas sao
/// recalculadas a partir dela sem confiar no que o simulador anotou.
/// </summary>
public class SimulacaoTestes
{
    private static IEnumerable<(string Nome, List<Tarefa> Tarefas)> Cenarios()
    {
        foreach (var carga in Cargas.Todas()) yield return carga;

        yield return ("prioridade", Cargas.ComPrioridade(60));
        yield return ("cauda longa com chegadas", Cargas.CaudaLonga(40, chegadaAte: 100));
    }

    private static IEnumerable<Politica> Politicas()
    {
        foreach (var p in Politica.Todas()) yield return p;

        yield return Politica.PorPrioridade();
        yield return Politica.ComEnvelhecimento();
    }

    [Fact]
    public void OSorteioEhDeterminista()
    {
        var a = new Sorteio(7);
        var b = new Sorteio(7);

        for (var i = 0; i < 1000; i++) Assert.Equal(a.Proximo(), b.Proximo());
    }

    /// <summary>
    /// A fracao precisa cobrir os DOIS LADOS do meio: um deslocamento de 33 em vez
    /// de 32 deixaria o gerador com 31 bits e "uma em cada dez tem prioridade
    /// zero" viraria "todas tem", com os numeros continuando plausiveis.
    /// </summary>
    [Fact]
    public void AFracaoCobreOsDoisLadosDoMeio()
    {
        var sorteio = new Sorteio(7);
        var acima = 0;
        var maior = 0.0;

        for (var i = 0; i < 10_000; i++)
        {
            var f = sorteio.Fracao();

            Assert.InRange(f, 0.0, 1.0);
            if (f > 0.5) acima++;
            if (f > maior) maior = f;
        }

        Assert.InRange(acima, 4_700, 5_300);
        Assert.True(maior > 0.999, $"o maior sorteio foi {maior}");
    }

    /// <summary>
    /// CADA TAREFA RODOU exatamente a duracao dela, nem mais nem menos, em toda
    /// politica e em toda carga.
    ///
    /// E a conferencia mais basica que existe e a que pega qualquer erro de
    /// contabilidade na preempcao.
    /// </summary>
    [Fact]
    public void CadaTarefaRodaExatamenteADuracaoDela()
    {
        foreach (var (nome, tarefas) in Cenarios())
            foreach (var politica in Politicas())
                Assert.True(
                    ForcaBruta.CadaTarefaRodouOQuePrecisava(Simulacao.Rodar(tarefas, politica)),
                    $"{nome} com {politica.Nome}");
    }

    [Fact]
    public void NinguemRodaAntesDeChegar()
    {
        foreach (var (nome, tarefas) in Cenarios())
            foreach (var politica in Politicas())
                Assert.True(
                    ForcaBruta.NinguemRodouAntesDeChegar(Simulacao.Rodar(tarefas, politica)),
                    $"{nome} com {politica.Nome}");
    }

    /// <summary>
    /// O processador nunca fica OCIOSO com alguem pronto esperando: nenhuma das
    /// politicas daqui e preguicosa.
    /// </summary>
    [Fact]
    public void OProcessadorNuncaFicaOciosoAToa()
    {
        foreach (var (nome, tarefas) in Cenarios())
            foreach (var politica in Politicas())
                Assert.True(
                    ForcaBruta.NaoFicouOciosoAToa(Simulacao.Rodar(tarefas, politica)),
                    $"{nome} com {politica.Nome}");
    }

    /// <summary>
    /// AS METRICAS recalculadas a partir da linha do tempo batem com as que o
    /// simulador anotou.
    ///
    /// Esta e a conferencia que torna o simulador conferivel: as duas contas sao
    /// escritas em lugares diferentes e precisam dar o mesmo numero.
    /// </summary>
    [Fact]
    public void AsMetricasBatemComALinhaDoTempo()
    {
        foreach (var (nome, tarefas) in Cenarios())
            foreach (var politica in Politicas())
            {
                var execucao = Simulacao.Rodar(tarefas, politica);
                var (espera, retorno, resposta) = ForcaBruta.PelaLinhaDoTempo(execucao);

                Assert.Equal(espera, execucao.EsperaMedia(), 9);
                Assert.Equal(retorno, execucao.RetornoMedio(), 9);
                Assert.Equal(resposta, execucao.RespostaMedia(), 9);
            }
    }

    /// <summary>
    /// A DURACAO total e a mesma em toda politica, sempre que todo mundo chega
    /// junto: o trabalho e o mesmo, so a ordem muda.
    /// </summary>
    [Fact]
    public void OTrabalhoTotalNaoMudaComAPolitica()
    {
        var tarefas = Cargas.CaudaLonga(40);
        var total = tarefas.Sum(t => t.Duracao);

        foreach (var politica in Politicas())
            Assert.Equal(total, Simulacao.Rodar(tarefas, politica).Duracao);
    }

    /// <summary>
    /// A ESPERA nunca e negativa, e a RESPOSTA nunca passa da espera: ninguem
    /// espera para comecar mais do que espera no total.
    /// </summary>
    [Fact]
    public void AsMetricasRespeitamAsDesigualdadesObvias()
    {
        foreach (var (nome, tarefas) in Cenarios())
            foreach (var politica in Politicas())
            {
                var execucao = Simulacao.Rodar(tarefas, politica);

                foreach (var tarefa in tarefas)
                {
                    Assert.True(execucao.Espera(tarefa) >= 0, $"{nome}/{politica.Nome}: {tarefa}");
                    Assert.True(execucao.Resposta(tarefa) >= 0, $"{nome}/{politica.Nome}: {tarefa}");
                    Assert.True(execucao.Resposta(tarefa) <= execucao.Espera(tarefa), $"{nome}/{politica.Nome}: {tarefa}");
                    Assert.Equal(execucao.Espera(tarefa) + tarefa.Duracao, execucao.Retorno(tarefa));
                }
            }
    }

    /// <summary>
    /// A execucao NA ORDEM dada, que e a peca da forca bruta, concorda com o
    /// simulador quando a ordem e a mesma.
    ///
    /// Elas sao escritas separadas de proposito: o juiz nao pode depender do mesmo
    /// codigo que ele julga. Esta e a conferencia de que as duas falam do mesmo
    /// mundo.
    /// </summary>
    [Fact]
    public void AExecucaoNaOrdemConcordaComOSimulador()
    {
        for (ulong semente = 1; semente <= 100; semente++)
        {
            var tarefas = Cargas.MinusculaJunta(6, semente);

            var peloSimulador = Simulacao.Rodar(tarefas, Politica.Fifo());
            var naOrdem = Simulacao.NaOrdem(tarefas, tarefas.Select(t => t.Id).ToList());

            Assert.Equal(peloSimulador.EsperaMedia(), naOrdem.EsperaMedia(), 9);
            Assert.Equal(peloSimulador.LinhaDoTempo, naOrdem.LinhaDoTempo);
        }
    }

    [Fact]
    public void UmaTarefaSoNaoEsperaNada()
    {
        var tarefas = new List<Tarefa> { new(0, 0, 10) };

        foreach (var politica in Politicas())
        {
            var execucao = Simulacao.Rodar(tarefas, politica);

            Assert.Equal(0, execucao.EsperaMedia());
            Assert.Equal(10, execucao.RetornoMedio());
            Assert.Equal(0, execucao.Trocas);
        }
    }

    /// <summary>
    /// O processador fica OCIOSO quando ninguem chegou ainda, e a linha do tempo
    /// registra isso.
    /// </summary>
    [Fact]
    public void OOciosoApareceNaLinhaDoTempo()
    {
        var tarefas = new List<Tarefa> { new(0, 5, 3) };
        var execucao = Simulacao.Rodar(tarefas, Politica.Fifo());

        Assert.Equal(8, execucao.Duracao);
        Assert.Equal(5, execucao.LinhaDoTempo.Count(id => id == Execucao.Ocioso));
        Assert.Equal(0, execucao.Espera(tarefas[0]));
        Assert.Equal("....." + "000", execucao.Desenho());
    }
}
