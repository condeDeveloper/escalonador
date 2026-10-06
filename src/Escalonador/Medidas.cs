namespace Conde.Escalonador;

/// <summary>
/// As MEDIDAS: as tabelas que o medidor imprime e que os testes conferem.
///
/// Elas ficam aqui, e nao no medidor, para o teste poder afirmar sobre o MESMO
/// numero que aparece no texto. Tabela de README que ninguem testa envelhece
/// errado em silencio.
///
/// Nenhuma delas usa relogio. Todas sao contagem de passos, de trocas de contexto
/// e de ordens enumeradas.
/// </summary>
public static class Medidas
{
    public sealed record LinhaJuiz(string Chegadas, int Tarefas, long Ordens, double Otimo, double MenorPrimeiro, double Fifo, double Pior);

    /// <summary>
    /// O JUIZ: a melhor ordem possivel contra o que cada politica fez.
    ///
    /// Com todas chegando juntas, a coluna do menor primeiro bate com a do otimo na
    /// virgula, sempre: e o teorema. Com chegadas espalhadas ela NAO bate, e isso e
    /// o achado que o teorema sozinho nao conta.
    /// </summary>
    public static List<LinhaJuiz> Juiz(int cargas = 50)
    {
        var linhas = new List<LinhaJuiz>();

        foreach (var juntas in new[] { true, false })
            foreach (var quantas in new[] { 4, 6, 8 })
            {
                var otimo = 0.0;
                var menor = 0.0;
                var fifo = 0.0;
                var pior = 0.0;
                long ordens = 0;

                for (ulong semente = 1; semente <= (ulong)cargas; semente++)
                {
                    var tarefas = juntas
                        ? Cargas.MinusculaJunta(quantas, semente)
                        : Cargas.Minuscula(quantas, semente);

                    var melhor = ForcaBruta.MenorEsperaMedia(tarefas);
                    var piorDeTodas = ForcaBruta.MaiorEsperaMedia(tarefas);

                    otimo += melhor.EsperaMedia;
                    pior += piorDeTodas.EsperaMedia;
                    ordens = melhor.OrdensOlhadas;

                    menor += Simulacao.Rodar(tarefas, Politica.MenorPrimeiro()).EsperaMedia();
                    fifo += Simulacao.Rodar(tarefas, Politica.Fifo()).EsperaMedia();
                }

                linhas.Add(new LinhaJuiz(
                    juntas ? "todas juntas" : "espalhadas",
                    quantas,
                    ordens,
                    otimo / cargas,
                    menor / cargas,
                    fifo / cargas,
                    pior / cargas));
            }

        return linhas;
    }

    public sealed record LinhaPolitica(string Carga, string Politica, double Espera, double Retorno, double Resposta, int PiorEspera, int Trocas);

    /// <summary>
    /// As SEIS POLITICAS em quatro cargas, com as tres metricas lado a lado.
    ///
    /// E ter as tres lado a lado que mostra o assunto: nenhuma politica ganha nas
    /// tres, e escolher uma e escolher qual metrica importa.
    /// </summary>
    public static List<LinhaPolitica> Politicas()
    {
        var linhas = new List<LinhaPolitica>();

        foreach (var (nome, tarefas) in Cargas.Todas())
            foreach (var politica in Politica.Todas())
            {
                var execucao = Simulacao.Rodar(tarefas, politica);

                linhas.Add(new LinhaPolitica(
                    nome,
                    politica.Nome,
                    execucao.EsperaMedia(),
                    execucao.RetornoMedio(),
                    execucao.RespostaMedia(),
                    execucao.PiorEspera(),
                    execucao.Trocas));
            }

        return linhas;
    }

    public sealed record LinhaComboio(string Politica, double Espera, double Retorno);

    /// <summary>
    /// O EFEITO COMBOIO: uma tarefa de cem passos na frente de vinte de um passo.
    /// </summary>
    public static List<LinhaComboio> Comboio()
    {
        var tarefas = Cargas.Comboio(20);

        return [.. Politica.Todas().Select(p =>
        {
            var execucao = Simulacao.Rodar(tarefas, p);
            return new LinhaComboio(p.Nome, execucao.EsperaMedia(), execucao.RetornoMedio());
        })];
    }

    public sealed record LinhaQuantum(int Quantum, double Resposta, double Retorno, int Trocas);

    /// <summary>
    /// O QUANTUM do round-robin: a troca entre resposta e retorno.
    ///
    /// Quantum pequeno da resposta otima e gasta uma troca de contexto por passo.
    /// Quantum grande vira FIFO. Nao ha escolha certa, ha escolha.
    /// </summary>
    public static List<LinhaQuantum> Quanta()
    {
        var tarefas = Cargas.CaudaLonga(50);

        return [.. new[] { 1, 2, 4, 8, 16, 32, 64, 256 }.Select(q =>
        {
            var execucao = Simulacao.Rodar(tarefas, Politica.RoundRobin(q));
            return new LinhaQuantum(q, execucao.RespostaMedia(), execucao.RetornoMedio(), execucao.Trocas);
        })];
    }

    public sealed record LinhaFome(string Politica, double Espera, int PiorEspera, double Desequilibrio);

    /// <summary>
    /// A FOME: a media otima com uma vitima.
    ///
    /// A coluna do desequilibrio e a que importa: ela e a pior espera dividida pela
    /// media, e uma politica com media boa e desequilibrio de dezenas e uma
    /// politica que mata de fome.
    /// </summary>
    public static List<LinhaFome> Fomes()
    {
        var tarefas = Cargas.CaudaLonga(60, chegadaAte: 100);

        var politicas = new[]
        {
            Politica.Fifo(),
            Politica.MenorPrimeiro(),
            Politica.MenorRestante(),
            Politica.RoundRobin(4),
        };

        return [.. politicas.Select(p =>
        {
            var execucao = Simulacao.Rodar(tarefas, p);
            return new LinhaFome(p.Nome, execucao.EsperaMedia(), execucao.PiorEspera(), execucao.Desequilibrio());
        })];
    }

    public sealed record LinhaPrioridade(string Politica, double Espera, int PiorEspera, int PiorEsperaDaBaixa);

    /// <summary>
    /// A PRIORIDADE e o ENVELHECIMENTO: a fome, e o conserto de uma linha.
    /// </summary>
    public static List<LinhaPrioridade> Prioridades()
    {
        var tarefas = Cargas.ComPrioridade(250);
        var baixas = tarefas.Where(t => t.Prioridade == 0).ToList();

        var politicas = new[] { Politica.PorPrioridade(), Politica.ComEnvelhecimento(10), Politica.ComEnvelhecimento(3) };

        return [.. politicas.Select(p =>
        {
            var execucao = Simulacao.Rodar(tarefas, p);

            return new LinhaPrioridade(
                p.Nome,
                execucao.EsperaMedia(),
                execucao.PiorEspera(),
                baixas.Count == 0 ? 0 : baixas.Max(execucao.Espera));
        })];
    }
}
