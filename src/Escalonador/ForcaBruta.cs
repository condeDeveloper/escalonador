namespace Conde.Escalonador;

/// <summary>
/// O JUIZ deste repositorio: TODAS as ordens possiveis, uma por uma.
///
/// Comparar duas politicas diz qual das duas e melhor. So a enumeracao de todas
/// as ordens diz se alguma delas e OTIMA, e e essa a pergunta que vale.
///
/// Ela e fatorial e nao da para melhorar, e isso nao e defeito: ela so roda em
/// cargas de menos de dez tarefas, que e exatamente o tamanho em que uma politica
/// ruim ainda parece razoavel. Com oito tarefas sao quarenta mil ordens, e o juiz
/// olha todas.
///
/// O que ela enumera sao as ordens SEM PREEMPCAO, em que cada tarefa roda inteira
/// na vez dela e o processador so fica ocioso esperando a proxima chegar. Esse e o
/// espaco em que o teorema do menor primeiro vale, e e o espaco em que a
/// comparacao faz sentido.
/// </summary>
public static class ForcaBruta
{
    public sealed record Melhor(List<int> Ordem, double EsperaMedia, double RetornoMedio, long OrdensOlhadas);

    /// <summary>A ordem que minimiza o tempo medio de espera, achada por enumeracao.</summary>
    public static Melhor MenorEsperaMedia(IReadOnlyList<Tarefa> tarefas, int limite = 9)
    {
        ArgumentNullException.ThrowIfNull(tarefas);
        if (tarefas.Count > limite)
            throw new ArgumentException($"a enumeracao so cabe em ate {limite} tarefas", nameof(tarefas));

        List<int>? melhorOrdem = null;
        var melhorEspera = double.MaxValue;
        var melhorRetorno = 0.0;
        long olhadas = 0;

        foreach (var ordem in Ordens(tarefas.Select(t => t.Id).ToList()))
        {
            olhadas++;

            var execucao = Simulacao.NaOrdem(tarefas, ordem);
            var espera = execucao.EsperaMedia();

            if (espera >= melhorEspera) continue;

            melhorEspera = espera;
            melhorRetorno = execucao.RetornoMedio();
            melhorOrdem = [.. ordem];
        }

        return new Melhor(melhorOrdem ?? [], melhorEspera, melhorRetorno, olhadas);
    }

    /// <summary>A PIOR ordem, que mostra o tamanho do espaco de escolha.</summary>
    public static Melhor MaiorEsperaMedia(IReadOnlyList<Tarefa> tarefas, int limite = 9)
    {
        ArgumentNullException.ThrowIfNull(tarefas);
        if (tarefas.Count > limite)
            throw new ArgumentException($"a enumeracao so cabe em ate {limite} tarefas", nameof(tarefas));

        List<int>? piorOrdem = null;
        var piorEspera = double.MinValue;
        var piorRetorno = 0.0;
        long olhadas = 0;

        foreach (var ordem in Ordens(tarefas.Select(t => t.Id).ToList()))
        {
            olhadas++;

            var execucao = Simulacao.NaOrdem(tarefas, ordem);
            var espera = execucao.EsperaMedia();

            if (espera <= piorEspera) continue;

            piorEspera = espera;
            piorRetorno = execucao.RetornoMedio();
            piorOrdem = [.. ordem];
        }

        return new Melhor(piorOrdem ?? [], piorEspera, piorRetorno, olhadas);
    }

    /// <summary>Todas as permutacoes dos identificadores, uma por vez.</summary>
    public static IEnumerable<List<int>> Ordens(List<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            yield return [];
            yield break;
        }

        var atual = new List<int>();
        var usados = new bool[ids.Count];

        foreach (var ordem in Andar(ids, atual, usados)) yield return ordem;
    }

    private static IEnumerable<List<int>> Andar(List<int> ids, List<int> atual, bool[] usados)
    {
        if (atual.Count == ids.Count)
        {
            yield return atual;
            yield break;
        }

        for (var i = 0; i < ids.Count; i++)
        {
            if (usados[i]) continue;

            usados[i] = true;
            atual.Add(ids[i]);

            foreach (var ordem in Andar(ids, atual, usados)) yield return ordem;

            atual.RemoveAt(atual.Count - 1);
            usados[i] = false;
        }
    }

    /// <summary>
    /// Quantas ordens existem: o fatorial, que e por que o juiz nao escala.
    /// </summary>
    public static long QuantasOrdens(int tarefas)
    {
        long total = 1;
        for (var i = 2; i <= tarefas; i++) total *= i;

        return total;
    }

    /// <summary>
    /// A conferencia que NAO depende do simulador: as metricas recalculadas a
    /// partir da linha do tempo.
    ///
    /// Se o simulador anotasse o fim errado, as duas contas discordariam. Esta
    /// funcao e o jeito de o juiz nao confiar em quem ele julga.
    /// </summary>
    public static (double Espera, double Retorno, double Resposta) PelaLinhaDoTempo(Execucao execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);

        var fim = new Dictionary<int, int>();
        var primeira = new Dictionary<int, int>();
        var rodou = new Dictionary<int, int>();

        for (var t = 0; t < execucao.LinhaDoTempo.Count; t++)
        {
            var id = execucao.LinhaDoTempo[t];
            if (id == Execucao.Ocioso) continue;

            if (!primeira.ContainsKey(id)) primeira[id] = t;

            fim[id] = t + 1;
            rodou[id] = rodou.GetValueOrDefault(id) + 1;
        }

        var espera = 0.0;
        var retorno = 0.0;
        var resposta = 0.0;

        foreach (var tarefa in execucao.Tarefas)
        {
            retorno += fim[tarefa.Id] - tarefa.Chegada;
            espera += fim[tarefa.Id] - tarefa.Chegada - tarefa.Duracao;
            resposta += primeira[tarefa.Id] - tarefa.Chegada;
        }

        var quantas = execucao.Tarefas.Count;

        return (espera / quantas, retorno / quantas, resposta / quantas);
    }

    /// <summary>
    /// Cada tarefa rodou EXATAMENTE a duracao dela, nem mais nem menos.
    ///
    /// E a conferencia mais basica que existe e a que pega qualquer erro de
    /// contabilidade na preempcao.
    /// </summary>
    public static bool CadaTarefaRodouOQuePrecisava(Execucao execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);

        var rodou = new Dictionary<int, int>();

        foreach (var id in execucao.LinhaDoTempo)
        {
            if (id == Execucao.Ocioso) continue;
            rodou[id] = rodou.GetValueOrDefault(id) + 1;
        }

        foreach (var tarefa in execucao.Tarefas)
            if (rodou.GetValueOrDefault(tarefa.Id) != tarefa.Duracao) return false;

        return true;
    }

    /// <summary>
    /// Nenhuma tarefa rodou ANTES de chegar, que e o outro jeito de o simulador
    /// trapacear sem querer.
    /// </summary>
    public static bool NinguemRodouAntesDeChegar(Execucao execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);

        var porId = execucao.Tarefas.ToDictionary(t => t.Id);

        for (var t = 0; t < execucao.LinhaDoTempo.Count; t++)
        {
            var id = execucao.LinhaDoTempo[t];
            if (id == Execucao.Ocioso) continue;

            if (t < porId[id].Chegada) return false;
        }

        return true;
    }

    /// <summary>
    /// O processador nunca ficou ocioso com alguem pronto esperando.
    ///
    /// Essa e a propriedade que separa uma politica de uma politica preguicosa, e
    /// todas as daqui tem que cumprir.
    /// </summary>
    public static bool NaoFicouOciosoAToa(Execucao execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);

        var restante = execucao.Tarefas.ToDictionary(t => t.Id, t => t.Duracao);

        for (var t = 0; t < execucao.LinhaDoTempo.Count; t++)
        {
            var id = execucao.LinhaDoTempo[t];

            if (id != Execucao.Ocioso)
            {
                restante[id]--;
                continue;
            }

            foreach (var tarefa in execucao.Tarefas)
                if (tarefa.Chegada <= t && restante[tarefa.Id] > 0) return false;
        }

        return true;
    }
}
