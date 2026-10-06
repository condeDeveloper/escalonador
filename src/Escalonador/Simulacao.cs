namespace Conde.Escalonador;

/// <summary>
/// O RESULTADO de uma execucao: quem rodou em cada passo e quando cada tarefa
/// acabou.
///
/// A linha do tempo esta aqui inteira, com um identificador por passo, e nao e
/// enfeite: ela e o que torna o resultado conferivel. Toda metrica e recalculada
/// a partir dela nos testes, sem confiar no que o simulador anotou.
/// </summary>
public sealed record Execucao(
    string Politica,
    IReadOnlyList<int> LinhaDoTempo,
    IReadOnlyDictionary<int, int> Fim,
    IReadOnlyDictionary<int, int> PrimeiraVez,
    int Trocas,
    IReadOnlyList<Tarefa> Tarefas)
{
    public const int Ocioso = -1;

    public int Duracao => LinhaDoTempo.Count;

    /// <summary>O tempo de RETORNO: do momento em que chegou ate acabar.</summary>
    public int Retorno(Tarefa tarefa) => Fim[tarefa.Id] - tarefa.Chegada;

    /// <summary>
    /// O tempo de ESPERA: quanto tempo ela existiu sem estar rodando.
    ///
    /// E o retorno menos o trabalho dela, e e a metrica que o menor primeiro
    /// minimiza.
    /// </summary>
    public int Espera(Tarefa tarefa) => Retorno(tarefa) - tarefa.Duracao;

    /// <summary>
    /// O tempo de RESPOSTA: ate a primeira vez que ela rodou.
    ///
    /// E a metrica que importa para quem esta esperando na frente de uma tela, e e
    /// a unica em que o round-robin ganha de todo mundo.
    /// </summary>
    public int Resposta(Tarefa tarefa) => PrimeiraVez[tarefa.Id] - tarefa.Chegada;

    public double RetornoMedio() => Tarefas.Average(Retorno);

    public double EsperaMedia() => Tarefas.Average(t => (double)Espera(t));

    public double RespostaMedia() => Tarefas.Average(t => (double)Resposta(t));

    public int PiorEspera() => Tarefas.Max(Espera);

    public int PiorResposta() => Tarefas.Max(Resposta);

    /// <summary>
    /// O DESEQUILIBRIO: a pior espera dividida pela media.
    ///
    /// E a medida de justica que importa, porque a media esconde a vitima: uma
    /// politica com media otima e uma tarefa esperando mil vezes a media e uma
    /// politica que mata de fome.
    /// </summary>
    public double Desequilibrio()
    {
        var media = EsperaMedia();
        return media <= 0 ? 1 : PiorEspera() / media;
    }

    public string Desenho(int ate = 80)
    {
        var passos = LinhaDoTempo.Take(ate).Select(id => id == Ocioso ? "." : (id % 36).ToString("x"));

        return string.Concat(passos);
    }
}

/// <summary>
/// A SIMULACAO: um processador, um passo de cada vez.
///
/// Nao ha thread, nao ha relogio e nao ha sleep. A cada passo o simulador pergunta
/// a politica quem roda, roda um passo, e anota. O resultado e exatamente o mesmo
/// nos tres sistemas, porque nao depende de escalonador nenhum por baixo: o
/// assunto e o escalonador de cima.
/// </summary>
public static class Simulacao
{
    public static Execucao Rodar(IReadOnlyList<Tarefa> tarefas, Politica politica, int limite = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(politica);

        // a fila comeca na ordem de chegada: e ela que o round-robin gira, e uma
        // tarefa que acaba a fatia vai para TRAS de todo mundo que chegou no
        // meio, que e o comportamento do rodizio de verdade
        var andamentos = tarefas
            .OrderBy(t => t.Chegada)
            .ThenBy(t => t.Id)
            .Select(t => new Andamento(t))
            .ToList();

        var linha = new List<int>();
        var trocas = 0;

        var agora = 0;
        var rodando = (Andamento?)null;
        var fatiaRestante = 0;
        var faltam = andamentos.Count;

        while (faltam > 0 && agora < limite)
        {
            var precisaEscolher = rodando is null || rodando.Acabou || fatiaRestante == 0;

            if (precisaEscolher)
            {
                // quem gastou a fatia volta para o FIM da fila
                if (rodando is not null && !rodando.Acabou && politica.Fatia != int.MaxValue)
                {
                    andamentos.Remove(rodando);
                    andamentos.Add(rodando);
                }

                var prontas = andamentos.Where(a => !a.Acabou && a.Tarefa.Chegada <= agora).ToList();

                if (prontas.Count == 0)
                {
                    // ninguem chegou ainda: o processador fica ocioso
                    linha.Add(Execucao.Ocioso);
                    rodando = null;
                    agora++;

                    continue;
                }

                var escolhida = politica.Escolher(prontas, agora);

                if (rodando is not null && !ReferenceEquals(rodando, escolhida)) trocas++;

                rodando = escolhida;
                fatiaRestante = politica.Fatia;
            }

            if (rodando!.PrimeiraVez < 0) rodando.PrimeiraVez = agora;

            rodando.Restante--;
            if (fatiaRestante != int.MaxValue) fatiaRestante--;

            linha.Add(rodando.Tarefa.Id);
            agora++;

            if (rodando.Acabou)
            {
                rodando.Fim = agora;
                faltam--;
                fatiaRestante = 0;
            }
        }

        return new Execucao(
            politica.Nome,
            linha,
            andamentos.ToDictionary(a => a.Tarefa.Id, a => a.Fim),
            andamentos.ToDictionary(a => a.Tarefa.Id, a => a.PrimeiraVez),
            trocas,
            tarefas);
    }

    /// <summary>
    /// A execucao de uma ORDEM dada, sem preempcao: cada tarefa roda inteira na vez
    /// dela.
    ///
    /// E a peca que a forca bruta usa, e ela e separada da simulacao de proposito:
    /// o juiz nao pode depender do mesmo codigo que ele julga.
    /// </summary>
    public static Execucao NaOrdem(IReadOnlyList<Tarefa> tarefas, IReadOnlyList<int> ordem)
    {
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(ordem);

        var porId = tarefas.ToDictionary(t => t.Id);
        var linha = new List<int>();
        var fim = new Dictionary<int, int>();
        var primeira = new Dictionary<int, int>();

        var agora = 0;

        foreach (var id in ordem)
        {
            var tarefa = porId[id];

            while (agora < tarefa.Chegada)
            {
                linha.Add(Execucao.Ocioso);
                agora++;
            }

            primeira[id] = agora;

            for (var i = 0; i < tarefa.Duracao; i++)
            {
                linha.Add(id);
                agora++;
            }

            fim[id] = agora;
        }

        return new Execucao("ordem dada", linha, fim, primeira, Math.Max(0, ordem.Count - 1), tarefas);
    }
}
