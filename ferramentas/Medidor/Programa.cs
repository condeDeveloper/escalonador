using Conde.Escalonador;

var pedido = args.Length > 0 ? args[0] : "tudo";
var tudo = pedido == "tudo";

if (tudo || pedido == "juiz") Juiz();
if (tudo || pedido == "politicas") Politicas();
if (tudo || pedido == "comboio") Comboio();
if (tudo || pedido == "quantum") Quanta();
if (tudo || pedido == "fome") Fomes();

static void Juiz()
{
    Console.WriteLine();
    Console.WriteLine("  O JUIZ: TODAS AS ORDENS POSSIVEIS CONTRA O QUE CADA POLITICA FEZ");
    Console.WriteLine("  (espera media, em 50 cargas sorteadas de cada tamanho)");
    Console.WriteLine();
    Console.WriteLine("  chegadas       tarefas   ordens olhadas      otimo   menor primeiro       FIFO   a pior ordem");

    var anteriorJuiz = "";
    foreach (var l in Medidas.Juiz())
    {
        if (l.Chegadas != anteriorJuiz) { Console.WriteLine(); anteriorJuiz = l.Chegadas; }

        Console.WriteLine($"  {l.Chegadas,-14} {l.Tarefas,7} {l.Ordens,16:N0} {l.Otimo,10:F2} {l.MenorPrimeiro,16:F2} {l.Fifo,10:F2} {l.Pior,14:F2}");
    }

    Console.WriteLine();
    Console.WriteLine("  com todas na fila, o menor primeiro BATE com o otimo na virgula: e o teorema");
    Console.WriteLine("  com chegadas espalhadas ele NAO bate, e o teorema nunca disse que batia");
    Console.WriteLine();
}

static void Politicas()
{
    Console.WriteLine();
    Console.WriteLine("  AS SEIS POLITICAS EM QUATRO CARGAS");
    Console.WriteLine();
    Console.WriteLine("  carga          politica              espera   retorno   resposta   pior espera   trocas");

    var anterior = "";
    foreach (var l in Medidas.Politicas())
    {
        if (l.Carga != anterior) { Console.WriteLine(); anterior = l.Carga; }

        Console.WriteLine($"  {l.Carga,-14} {l.Politica,-20} {l.Espera,7:F1} {l.Retorno,9:F1} {l.Resposta,10:F1} {l.PiorEspera,13} {l.Trocas,8}");
    }

    Console.WriteLine();
}

static void Comboio()
{
    Console.WriteLine();
    Console.WriteLine("  O EFEITO COMBOIO: UMA TAREFA DE 100 PASSOS NA FRENTE DE 20 DE 1 PASSO");
    Console.WriteLine();
    Console.WriteLine("  politica              espera media   retorno medio");

    foreach (var l in Medidas.Comboio())
        Console.WriteLine($"  {l.Politica,-20} {l.Espera,13:F1} {l.Retorno,15:F1}");

    Console.WriteLine();
}

static void Quanta()
{
    Console.WriteLine();
    Console.WriteLine("  O QUANTUM DO ROUND-ROBIN: A TROCA ENTRE RESPOSTA E RETORNO");
    Console.WriteLine();
    Console.WriteLine("  quantum   resposta media   retorno medio   trocas de contexto");

    foreach (var l in Medidas.Quanta())
        Console.WriteLine($"  {l.Quantum,7} {l.Resposta,16:F1} {l.Retorno,15:F1} {l.Trocas,20:N0}");

    Console.WriteLine();
}

static void Fomes()
{
    Console.WriteLine();
    Console.WriteLine("  A MEDIA OTIMA COM UMA VITIMA");
    Console.WriteLine();
    Console.WriteLine("  politica              espera media   pior espera   desequilibrio");

    foreach (var l in Medidas.Fomes())
        Console.WriteLine($"  {l.Politica,-20} {l.Espera,13:F1} {l.PiorEspera,13} {l.Desequilibrio,15:F1}x");

    Console.WriteLine();
    Console.WriteLine("  A PRIORIDADE E O ENVELHECIMENTO");
    Console.WriteLine();
    Console.WriteLine("  politica                                      espera   pior espera   pior da prioridade baixa");

    foreach (var l in Medidas.Prioridades())
        Console.WriteLine($"  {l.Politica,-42} {l.Espera,9:F1} {l.PiorEspera,13} {l.PiorEsperaDaBaixa,26}");

    Console.WriteLine();
}
