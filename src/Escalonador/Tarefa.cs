namespace Conde.Escalonador;

/// <summary>
/// O SORTEIO, escrito aqui em vez de vir da biblioteca.
///
/// A documentacao do sorteio de biblioteca nao promete a mesma sequencia entre
/// versoes nem entre sistemas, e a integracao continua roda em tres. Uma medida
/// de "quanto o round-robin perdeu em tempo de retorno" com cargas diferentes em
/// cada sistema nao e uma medida: sao tres medidas que ninguem pode comparar.
/// </summary>
public sealed class Sorteio
{
    private ulong estado;

    public Sorteio(ulong semente) => estado = semente == 0 ? 1 : semente;

    public uint Proximo()
    {
        estado = estado * 6364136223846793005UL + 1442695040888963407UL;
        return (uint)(estado >> 32);
    }

    public int Ate(int limite) => (int)(Proximo() % (uint)limite);

    public int Entre(int menor, int maior) => menor + Ate(maior - menor + 1);

    public double Fracao() => Proximo() / 4294967296.0;
}

/// <summary>
/// Uma TAREFA: quando ela chegou, quanto trabalho ela tem e que prioridade ela
/// pediu.
///
/// O tempo e inteiro, em passos, e nao em segundos. Isso nao e simplificacao: a
/// pergunta do escalonamento e sobre ORDEM, e medir ordem com relogio de parede e
/// medir o escalonador do sistema operacional por baixo em vez do algoritmo em
/// cima.
///
/// A duracao esta aqui, dentro da tarefa, e e importante dizer em voz alta que no
/// mundo real ELA NAO EXISTE: ninguem sabe quanto um processo vai durar antes de
/// ele rodar. Metade das medidas deste repositorio e sobre o que acontece quando
/// o escalonador finge que sabe.
/// </summary>
public sealed record Tarefa(int Id, int Chegada, int Duracao, int Prioridade = 0)
{
    public override string ToString() => $"t{Id}(chega {Chegada}, dura {Duracao})";
}

/// <summary>
/// As CARGAS: os conjuntos de tarefas que separam as politicas.
///
/// Uma carga com todas as tarefas do mesmo tamanho nao separa nada: FIFO, menor
/// primeiro e round-robin dao exatamente a mesma coisa. O que separa as politicas
/// e a VARIACAO de tamanho, e ela e enorme em qualquer sistema de verdade: num
/// servidor web, a requisicao mediana custa milissegundos e a do relatorio mensal
/// custa minutos.
/// </summary>
public static class Cargas
{
    /// <summary>Todas chegam juntas e duram quase o mesmo: o caso que nao separa nada.</summary>
    public static List<Tarefa> Uniforme(int quantas, ulong semente = 7)
    {
        var sorteio = new Sorteio(semente);

        return [.. Enumerable.Range(0, quantas).Select(i => new Tarefa(i, 0, sorteio.Entre(9, 11)))];
    }

    /// <summary>
    /// O EFEITO COMBOIO: uma tarefa longa na frente de muitas curtas.
    ///
    /// E o caso que derruba o FIFO, e ele e o caso comum: um relatorio pesado na
    /// frente de mil consultas de um milissegundo. Todas elas esperam o relatorio
    /// inteiro, e o tempo medio de espera multiplica.
    /// </summary>
    public static List<Tarefa> Comboio(int curtas, int duracaoLonga = 100, int duracaoCurta = 1)
    {
        var tarefas = new List<Tarefa> { new(0, 0, duracaoLonga) };

        for (var i = 1; i <= curtas; i++) tarefas.Add(new Tarefa(i, 0, duracaoCurta));

        return tarefas;
    }

    /// <summary>
    /// Duracoes de CAUDA LONGA: muitas curtas e poucas muito longas.
    ///
    /// E a forma de quase toda carga de verdade, e e onde a escolha da politica
    /// importa de verdade.
    /// </summary>
    public static List<Tarefa> CaudaLonga(int quantas, ulong semente = 11, int chegadaAte = 0)
    {
        var sorteio = new Sorteio(semente);
        var tarefas = new List<Tarefa>();

        for (var i = 0; i < quantas; i++)
        {
            var duracao = sorteio.Fracao() < 0.9 ? sorteio.Entre(1, 5) : sorteio.Entre(50, 200);
            tarefas.Add(new Tarefa(i, chegadaAte == 0 ? 0 : sorteio.Entre(0, chegadaAte), duracao));
        }

        return tarefas;
    }

    /// <summary>Tarefas que CHEGAM ao longo do tempo, que e o caso de verdade.</summary>
    public static List<Tarefa> ComChegadas(int quantas, ulong semente = 13, int ate = 200)
    {
        var sorteio = new Sorteio(semente);

        return [.. Enumerable.Range(0, quantas)
            .Select(i => new Tarefa(i, sorteio.Entre(0, ate), sorteio.Entre(1, 30)))
            .OrderBy(t => t.Chegada)
            .Select((t, i) => t with { Id = i })];
    }

    /// <summary>
    /// Tarefas com PRIORIDADE, num sistema SATURADO, que e onde a fome aparece.
    ///
    /// Saturado quer dizer que chega mais trabalho do que da para fazer: aqui sao
    /// umas setecentas unidades de trabalho chegando ao longo de quatrocentos
    /// passos. Sem isso a fome nao existe, porque a fila esvazia sozinha e todo
    /// mundo acaba rodando.
    ///
    /// Uma em cada dez tarefas tem prioridade zero, e sao elas que a politica por
    /// prioridade pura deixa para o fim.
    /// </summary>
    public static List<Tarefa> ComPrioridade(int quantas, ulong semente = 17, int janela = 400)
    {
        var sorteio = new Sorteio(semente);

        return [.. Enumerable.Range(0, quantas)
            .Select(i => new Tarefa(
                i,
                sorteio.Entre(0, janela),
                sorteio.Entre(1, 5),
                sorteio.Fracao() < 0.1 ? 0 : sorteio.Entre(1, 4)))
            .OrderBy(t => t.Chegada)
            .Select((t, i) => t with { Id = i })];
    }

    public static IEnumerable<(string Nome, List<Tarefa> Tarefas)> Todas()
    {
        yield return ("uniforme", Uniforme(20));
        yield return ("comboio", Comboio(20));
        yield return ("cauda longa", CaudaLonga(50));
        yield return ("com chegadas", ComChegadas(50));
    }

    /// <summary>
    /// Uma carga minuscula com TODAS CHEGANDO JUNTAS.
    ///
    /// Esta e a condicao do teorema do menor primeiro, e ela e facil de esquecer:
    /// so com todo mundo ja na fila a politica e otima. Com chegadas espalhadas ela
    /// deixa de ser, e a medida mostra por quanto.
    /// </summary>
    public static List<Tarefa> MinusculaJunta(int quantas, ulong semente)
    {
        var sorteio = new Sorteio(semente);

        return [.. Enumerable.Range(0, quantas).Select(i => new Tarefa(i, 0, sorteio.Entre(1, 20)))];
    }

    /// <summary>
    /// A mesma coisa com CHEGADAS ESPALHADAS, para a forca bruta caber.
    ///
    /// Oito tarefas dao quarenta mil ordens possiveis, e e exatamente o tamanho em
    /// que uma politica errada ainda parece razoavel.
    /// </summary>
    public static List<Tarefa> Minuscula(int quantas, ulong semente)
    {
        var sorteio = new Sorteio(semente);

        return [.. Enumerable.Range(0, quantas)
            .Select(i => new Tarefa(i, sorteio.Entre(0, 10), sorteio.Entre(1, 20)))
            .OrderBy(t => t.Chegada)
            .Select((t, i) => t with { Id = i })];
    }
}
