using Conde.Escalonador;
using Xunit;

namespace Conde.Escalonador.Testes;

public class ForcaBrutaTestes
{
    /// <summary>
    /// A enumeracao gera TODAS as ordens, cada uma uma vez: o fatorial.
    /// </summary>
    [Fact]
    public void AEnumeracaoGeraTodasAsOrdensUmaVez()
    {
        for (var quantas = 0; quantas <= 7; quantas++)
        {
            var ids = Enumerable.Range(0, quantas).ToList();
            var ordens = ForcaBruta.Ordens(ids).Select(o => string.Join(",", o)).ToList();

            Assert.Equal(ForcaBruta.QuantasOrdens(quantas), ordens.Count);
            Assert.Equal(ordens.Count, ordens.Distinct().Count());
        }
    }

    [Fact]
    public void CadaOrdemTemTodosOsIdentificadores()
    {
        var ids = Enumerable.Range(0, 6).ToList();

        foreach (var ordem in ForcaBruta.Ordens(ids))
        {
            Assert.Equal(6, ordem.Count);
            Assert.Equal(ids, ordem.OrderBy(x => x));
        }
    }

    /// <summary>
    /// O ACHADO PRINCIPAL: com TODAS CHEGANDO JUNTAS, o menor primeiro bate com a
    /// melhor ordem possivel, na virgula, em toda carga.
    ///
    /// Isso e o teorema, e a prova dele cabe numa linha: se uma tarefa longa esta
    /// na frente de uma curta, trocar as duas diminui a espera da curta mais do que
    /// aumenta a da longa. A enumeracao confere o teorema em vez de confiar nele.
    /// </summary>
    [Fact]
    public void ComTodasJuntasOMenorPrimeiroEhOtimo()
    {
        foreach (var quantas in new[] { 4, 5, 6, 7 })
            for (ulong semente = 1; semente <= 40; semente++)
            {
                var tarefas = Cargas.MinusculaJunta(quantas, semente);

                var otimo = ForcaBruta.MenorEsperaMedia(tarefas).EsperaMedia;
                var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();

                Assert.Equal(otimo, menor, 9);
            }
    }

    /// <summary>
    /// O ACHADO QUE ME CORRIGIU: com CHEGADAS ESPALHADAS ele NAO e otimo.
    ///
    /// Eu ia escrever que o menor primeiro e otimo, ponto. O teorema vale quando
    /// todas as tarefas ja estao na fila, e essa condicao e facil de esquecer
    /// porque quase nunca vem escrita junto do enunciado.
    ///
    /// Com chegadas espalhadas, as vezes compensa deixar o processador OCIOSO
    /// esperando uma tarefa curta que esta prestes a chegar, e nenhuma politica que
    /// so olha o presente faz isso.
    /// </summary>
    [Fact]
    public void ComChegadasEspalhadasEleDeixaDeSerOtimo()
    {
        var perdeu = 0;
        var total = 0;

        foreach (var quantas in new[] { 5, 6, 7 })
            for (ulong semente = 1; semente <= 40; semente++)
            {
                var tarefas = Cargas.Minuscula(quantas, semente);

                var otimo = ForcaBruta.MenorEsperaMedia(tarefas).EsperaMedia;
                var menor = Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();

                total++;
                if (menor > otimo + 1e-9) perdeu++;

                // e ele nunca fica MELHOR que o otimo: isso seria erro do juiz
                Assert.True(menor >= otimo - 1e-9, $"{quantas} tarefas, semente {semente}");
            }

        Assert.True(perdeu > total / 5, $"ele so perdeu em {perdeu} de {total}");
    }

    /// <summary>
    /// A MELHOR e a PIOR ordem sao mesmo o minimo e o maximo: nenhuma ordem
    /// enumerada fica fora do intervalo.
    /// </summary>
    [Fact]
    public void AMelhorEAPiorLimitamTodasAsOrdens()
    {
        for (ulong semente = 1; semente <= 30; semente++)
        {
            var tarefas = Cargas.Minuscula(5, semente);

            var melhor = ForcaBruta.MenorEsperaMedia(tarefas).EsperaMedia;
            var pior = ForcaBruta.MaiorEsperaMedia(tarefas).EsperaMedia;

            Assert.True(melhor <= pior);

            foreach (var ordem in ForcaBruta.Ordens(tarefas.Select(t => t.Id).ToList()))
            {
                var espera = Simulacao.NaOrdem(tarefas, ordem).EsperaMedia();

                Assert.InRange(espera, melhor - 1e-9, pior + 1e-9);
            }
        }
    }

    /// <summary>
    /// A ordem devolvida pela forca bruta REALMENTE da a espera que ela diz.
    /// </summary>
    [Fact]
    public void AOrdemDevolvidaDaAEsperaQueElaDiz()
    {
        for (ulong semente = 1; semente <= 50; semente++)
        {
            var tarefas = Cargas.Minuscula(6, semente);
            var melhor = ForcaBruta.MenorEsperaMedia(tarefas);

            var execucao = Simulacao.NaOrdem(tarefas, melhor.Ordem);

            Assert.Equal(melhor.EsperaMedia, execucao.EsperaMedia(), 9);
            Assert.Equal(melhor.RetornoMedio, execucao.RetornoMedio(), 9);
            Assert.True(ForcaBruta.CadaTarefaRodouOQuePrecisava(execucao));
            Assert.True(ForcaBruta.NinguemRodouAntesDeChegar(execucao));
        }
    }

    /// <summary>
    /// E a ordem otima e mesmo a do menor primeiro quando todos chegam juntos, com
    /// os identificadores na ordem das duracoes.
    /// </summary>
    [Fact]
    public void AOrdemOtimaEhADasDuracoesCrescentes()
    {
        for (ulong semente = 1; semente <= 50; semente++)
        {
            var tarefas = Cargas.MinusculaJunta(6, semente);
            var melhor = ForcaBruta.MenorEsperaMedia(tarefas);

            var duracoes = melhor.Ordem.Select(id => tarefas.Single(t => t.Id == id).Duracao).ToList();

            Assert.Equal(duracoes.OrderBy(d => d), duracoes);
        }
    }

    [Fact]
    public void CargaGrandeDemaisParaAEnumeracaoEhRecusada()
    {
        var tarefas = Cargas.Minuscula(12, 1);

        Assert.Throws<ArgumentException>(() => ForcaBruta.MenorEsperaMedia(tarefas));
        Assert.Throws<ArgumentException>(() => ForcaBruta.MaiorEsperaMedia(tarefas));
    }

    /// <summary>
    /// A conferencia da linha do tempo pega uma execucao ESTRAGADA: ela nao e
    /// decorativa.
    /// </summary>
    [Fact]
    public void AConferenciaPegaUmaExecucaoEstragada()
    {
        var tarefas = Cargas.MinusculaJunta(4, 1);
        var boa = Simulacao.Rodar(tarefas, Politica.Fifo());

        Assert.True(ForcaBruta.CadaTarefaRodouOQuePrecisava(boa));

        // tirando um passo da linha do tempo, a conferencia acusa
        var estragada = boa with { LinhaDoTempo = boa.LinhaDoTempo.Skip(1).ToList() };

        Assert.False(ForcaBruta.CadaTarefaRodouOQuePrecisava(estragada));
    }

    [Fact]
    public void AQuantidadeDeOrdensEhOFatorial()
    {
        Assert.Equal(1, ForcaBruta.QuantasOrdens(0));
        Assert.Equal(1, ForcaBruta.QuantasOrdens(1));
        Assert.Equal(24, ForcaBruta.QuantasOrdens(4));
        Assert.Equal(40_320, ForcaBruta.QuantasOrdens(8));
        Assert.Equal(362_880, ForcaBruta.QuantasOrdens(9));
    }
}
