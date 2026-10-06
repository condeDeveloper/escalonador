namespace Conde.Escalonador;

/// <summary>O estado de uma tarefa durante a simulacao.</summary>
public sealed class Andamento
{
    public Andamento(Tarefa tarefa)
    {
        ArgumentNullException.ThrowIfNull(tarefa);

        Tarefa = tarefa;
        Restante = tarefa.Duracao;
        PrimeiraVez = -1;
        Fim = -1;
    }

    public Tarefa Tarefa { get; }

    public int Restante { get; set; }

    /// <summary>O instante em que ela rodou pela primeira vez.</summary>
    public int PrimeiraVez { get; set; }

    public int Fim { get; set; }

    /// <summary>Quanto tempo ela ficou pronta e nao rodou, seguido.</summary>
    public int EsperandoAgora { get; set; }

    public bool Acabou => Restante == 0;
}

/// <summary>
/// Uma POLITICA de escalonamento: dada a lista de quem esta pronto, qual roda
/// agora.
///
/// Tudo o que distingue as politicas daqui cabe nessa pergunta, e e de proposito:
/// as diferencas enormes de resultado vem de uma escolha por passo, nao de
/// estruturas diferentes.
/// </summary>
public abstract record Politica(string Nome)
{
    /// <summary>A politica tira a tarefa que esta rodando antes de ela acabar.</summary>
    public virtual bool Preemptiva => false;

    /// <summary>Quantos passos a tarefa escolhida roda antes de a escolha ser refeita.</summary>
    public virtual int Fatia => int.MaxValue;

    /// <summary>
    /// Qual das prontas roda agora.
    ///
    /// A lista chega ordenada por ordem de chegada, e o desempate e sempre pelo
    /// menor identificador: sem isso, duas execucoes da mesma carga poderiam dar
    /// resultados diferentes e a medida nao seria comparavel.
    /// </summary>
    public abstract Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora);

    public sealed override string ToString() => Nome;

    /// <summary>
    /// FIFO: a ordem de chegada, e quem comeca termina.
    ///
    /// E a politica mais justa que existe no sentido de nao privilegiar ninguem, e
    /// e a pior em tempo medio de espera quando as duracoes variam. O efeito
    /// comboio e dela.
    /// </summary>
    public static Politica Fifo() => new PorChegada();

    /// <summary>
    /// MENOR PRIMEIRO, sem preempcao: entre as prontas, a mais curta.
    ///
    /// Ela e PROVADAMENTE otima para o tempo medio de espera, e a prova e de uma
    /// linha: se uma tarefa longa esta na frente de uma curta, trocar as duas
    /// diminui a espera da curta mais do que aumenta a da longa.
    ///
    /// E ela precisa saber a duracao de antemao, que ninguem sabe, e ela mata de
    /// fome as tarefas longas. As duas coisas sao medidas aqui.
    /// </summary>
    public static Politica MenorPrimeiro() => new PorMenorDuracao();

    /// <summary>
    /// MENOR RESTANTE PRIMEIRO, com preempcao: a cada passo, a que falta menos.
    ///
    /// E a versao preemptiva da anterior, e ela e otima entre TODAS as politicas
    /// preemptivas para o tempo medio de espera. Ela interrompe uma tarefa no meio
    /// quando chega uma mais curta.
    /// </summary>
    public static Politica MenorRestante() => new PorMenorRestante();

    /// <summary>
    /// ROUND-ROBIN: cada uma roda uma fatia e volta para o fim da fila.
    ///
    /// Ela e a unica daqui que nao precisa saber nada sobre o futuro, e ela da o
    /// melhor tempo de RESPOSTA de longe. O preco e o tempo de RETORNO, que fica o
    /// pior de todos, e as trocas de contexto.
    /// </summary>
    public static Politica RoundRobin(int fatia = 4) => new PorRodizio(fatia);

    /// <summary>
    /// POR PRIORIDADE, com preempcao: o numero maior ganha.
    ///
    /// Ela e o que todo sistema de verdade tem, e e a que mata de fome com mais
    /// facilidade: uma tarefa de prioridade baixa num sistema movimentado pode nao
    /// rodar nunca.
    /// </summary>
    public static Politica PorPrioridade() => new PorNumeroDePrioridade();

    /// <summary>
    /// POR PRIORIDADE COM ENVELHECIMENTO: quem espera demais sobe.
    ///
    /// E o conserto da fome, e ele e uma linha: somar a espera na prioridade
    /// efetiva. Nenhuma tarefa fica parada para sempre, porque a prioridade dela
    /// cresce sem limite.
    /// </summary>
    public static Politica ComEnvelhecimento(int aCada = 10) => new PorPrioridadeQueEnvelhece(aCada);

    public static IEnumerable<Politica> Todas()
    {
        yield return Fifo();
        yield return MenorPrimeiro();
        yield return MenorRestante();
        yield return RoundRobin(1);
        yield return RoundRobin(4);
        yield return RoundRobin(16);
    }

    private sealed record PorChegada() : Politica("FIFO")
    {
        public override Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora) =>
            prontas.OrderBy(p => p.Tarefa.Chegada).ThenBy(p => p.Tarefa.Id).First();
    }

    private sealed record PorMenorDuracao() : Politica("menor primeiro")
    {
        public override Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora) =>
            prontas.OrderBy(p => p.Tarefa.Duracao).ThenBy(p => p.Tarefa.Id).First();
    }

    private sealed record PorMenorRestante() : Politica("menor restante")
    {
        public override bool Preemptiva => true;

        public override int Fatia => 1;

        public override Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora) =>
            prontas.OrderBy(p => p.Restante).ThenBy(p => p.Tarefa.Id).First();
    }

    private sealed record PorRodizio(int Quantum) : Politica($"round-robin de {Quantum}")
    {
        public override bool Preemptiva => true;

        public override int Fatia => Quantum;

        public override Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora) => prontas[0];
    }

    private sealed record PorNumeroDePrioridade() : Politica("por prioridade")
    {
        public override bool Preemptiva => true;

        public override int Fatia => 1;

        public override Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora) =>
            prontas.OrderByDescending(p => p.Tarefa.Prioridade).ThenBy(p => p.Tarefa.Id).First();
    }

    private sealed record PorPrioridadeQueEnvelhece(int ACada) : Politica($"prioridade com envelhecimento a cada {ACada}")
    {
        public override bool Preemptiva => true;

        public override int Fatia => 1;

        public override Andamento Escolher(IReadOnlyList<Andamento> prontas, int agora) =>
            prontas
                .OrderByDescending(p => p.Tarefa.Prioridade + (agora - p.Tarefa.Chegada) / ACada)
                .ThenBy(p => p.Tarefa.Id)
                .First();
    }
}
