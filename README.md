# escalonador

Escalonamento do zero em C# e .NET 8: FIFO, menor primeiro, menor restante,
round-robin, prioridade e envelhecimento. O juiz é a **enumeração de todas as
ordens possíveis**, e não há uma thread nem um relógio no repositório inteiro.

```
$ dotnet medidor.dll juiz

  chegadas       tarefas   ordens olhadas      otimo   menor primeiro       FIFO   a pior ordem

  todas juntas         4               24      11.04            11.04      15.76          20.39
  todas juntas         6              720      18.61            18.61      26.14          33.61
  todas juntas         8           40,320      25.36            25.36      36.55          47.37

  espalhadas           4               24       9.63            11.04      13.29          22.85
  espalhadas           6              720      15.80            17.20      23.13          36.80
  espalhadas           8           40,320      23.24            24.53      33.90          51.91
```

Comparar duas políticas diz qual das duas é melhor. Só a enumeração diz se alguma
delas é **ótima**, e é essa a pergunta que vale.

Com todas as tarefas já na fila, o menor primeiro bate com a melhor ordem possível
**na vírgula**, em toda carga. Isso é o teorema, e a prova dele cabe numa linha: se
uma tarefa longa está na frente de uma curta, trocar as duas diminui a espera da
curta mais do que aumenta a da longa.

Com chegadas espalhadas, ele **não** bate. E o teorema nunca disse que batia.

## Por que não há thread nem relógio aqui

A pergunta do escalonamento é sobre **ordem**. Medir ordem com relógio de parede é
medir o escalonador do sistema operacional por baixo, em vez do algoritmo em cima,
e o resultado muda com a máquina, com a carga do momento e com quem mais está
rodando.

Aqui o tempo é um inteiro, um passo de cada vez, e a simulação pergunta à política
quem roda. Os três sistemas da integração contínua dão exatamente os mesmos
números.

## Nenhuma política ganha nas três métricas

```
$ dotnet medidor.dll politicas

  carga          politica              espera   retorno   resposta   pior espera   trocas

  uniforme       FIFO                    96.6     106.8       96.6           193       19
  uniforme       menor primeiro          92.2     102.3       92.2           192       19
  uniforme       round-robin de 1       178.2     188.4        9.5           192      202

  cauda longa    FIFO                   435.9     454.2      435.9           908       49
  cauda longa    menor primeiro          82.8     101.0       82.8           713       49
  cauda longa    round-robin de 1       152.6     170.9       24.5           713      900
```

O primeiro bloco é por que o problema passa despercebido: com todas as tarefas do
mesmo tamanho, FIFO e menor primeiro dão quase a mesma coisa. **Medir
escalonamento com carga uniforme não descobre nada.** O que separa as políticas é
a variação de duração, e ela é enorme em qualquer sistema de verdade: num servidor
web, a requisição mediana custa milissegundos e o relatório mensal custa minutos.

Na cauda longa, o menor primeiro corta a espera média em cinco vezes e o
round-robin corta o tempo de **resposta** em dezoito. São métricas diferentes, e
escolher uma política é escolher qual delas importa.

## O efeito comboio

```
$ dotnet medidor.dll comboio

  politica              espera media   retorno medio
  FIFO                         104.3           110.0
  menor primeiro                10.0            15.7
  round-robin de 1              11.0            16.7
  round-robin de 16             25.2            31.0
```

Uma tarefa de cem passos na frente de vinte de um passo. No FIFO, todas as vinte
esperam o relatório inteiro, e a espera média é **dez vezes** a do menor primeiro.

E o round-robin sobrevive quase tão bem quanto o ótimo **sem saber nada sobre as
durações**. Essa é a diferença que mais importa na prática: o menor primeiro
precisa saber quanto cada tarefa vai durar, e ninguém sabe.

## O quantum é um botão, não uma resposta

```
$ dotnet medidor.dll quantum

  quantum   resposta media   retorno medio   trocas de contexto
        1             24.5           170.9                  900
        8             90.4           165.1                  142
       64            231.5           287.6                   57
      256            435.9           454.2                   49
```

Quantum pequeno dá a melhor resposta e gasta uma troca de contexto por passo.
Quantum grande vira FIFO, literalmente: com uma fatia maior que a maior tarefa, a
linha do tempo é **idêntica** à do FIFO, e o teste confere isso passo a passo.

## A média ótima com uma vítima

```
$ dotnet medidor.dll fome

  politica              espera media   pior espera   desequilibrio
  FIFO                         226.3           770             3.4x
  menor primeiro                48.0           638            13.3x
  round-robin de 4              88.7           638             7.2x
```

O desequilíbrio é a pior espera dividida pela média, e ele é a medida que a média
esconde. O menor primeiro tem a melhor média de todas e a pior vítima: uma tarefa
espera treze vezes o que a tarefa típica espera, e ela é sempre uma tarefa longa.

E a prioridade pura é pior:

```
  politica                                      espera   pior espera   pior da prioridade baixa
  por prioridade                                 154.5           662                        662
  prioridade com envelhecimento a cada 10        159.7           356                        356
  prioridade com envelhecimento a cada 3         160.0           347                        347
```

O envelhecimento é uma linha: somar a espera na prioridade efetiva. A pior espera
de uma tarefa de prioridade baixa cai pela metade e a média de todo mundo piora
três por cento. É o trato inteiro.

## O que as medidas me corrigiram

**O menor primeiro é ótimo só quando todo mundo já está na fila, e eu ia escrever
que era ótimo e ponto.** A condição está no enunciado do teorema e quase nunca vem
escrita junto quando alguém cita o resultado. Com chegadas espalhadas, às vezes
compensa deixar o processador **ocioso** esperando uma tarefa curta que está
prestes a chegar, e nenhuma política que só olha o presente faz isso. A enumeração
mostrou a diferença: 24,53 contra 23,24.

**O meu round-robin era FIFO disfarçado.** A fila de prontas era reordenada por
chegada antes de a política escolher, então o rodízio nunca girava: ele devolvia
sempre a mesma tarefa. A medida do comboio dava exatamente os mesmos 104,3 do
FIFO nos três quanta, o que é um resultado plausível (quantum grande vira FIFO) e
estava errado em todos eles. O conserto é a fila ser a fila: quem gasta a fatia
vai para o fim, atrás inclusive de quem chegou no meio.

**A fome não existe sem saturação, e a minha carga de prioridade não saturava.** A
primeira versão tinha oitenta tarefas chegando em cem passos, e a pior espera era
idêntica nas três políticas: alguém tem que terminar por último, e com trabalho
finito a fila esvazia sozinha. O envelhecimento parecia não fazer nada. Com a
carga saturada, que é mais trabalho chegando do que dá para fazer, a diferença
aparece inteira.

**"Pior espera" não é a medida da fome.** Ela mede quem terminou por último, que é
quase sempre uma consequência do tamanho da carga e não da política. A medida é a
pior espera **dentro do grupo que a política prejudica**: as tarefas longas no
menor primeiro, as de prioridade baixa na prioridade pura.

## Por que o juiz é a enumeração

A enumeração é fatorial e não dá para melhorar, e isso não é defeito: ela só roda
em cargas de menos de dez tarefas, que é exatamente o tamanho em que uma política
ruim ainda parece razoável. Com oito tarefas são quarenta mil ordens, e o juiz
olha todas.

E o juiz é escrito separado do simulador de propósito: quem julga não pode
depender do mesmo código que é julgado. Os dois são conferidos um contra o outro
num caso em que precisam concordar.

O simulador também tem juiz. Toda execução guarda a **linha do tempo** inteira, com
um identificador por passo, e os testes recalculam as métricas a partir dela sem
confiar no que o simulador anotou. Três propriedades são verificadas em toda
política e toda carga: cada tarefa rodou exatamente a duração dela, ninguém rodou
antes de chegar, e o processador nunca ficou ocioso com alguém pronto esperando.

O sorteio é de um gerador congruente escrito aqui, e não do sorteio da
biblioteca, cuja documentação não promete a mesma sequência entre versões nem
entre sistemas.

## As peças

| arquivo | o que faz |
| --- | --- |
| `Tarefa.cs` | as tarefas, o sorteio e as cargas que separam as políticas |
| `Politica.cs` | as seis políticas, cada uma uma escolha por passo |
| `Simulacao.cs` | um processador, um passo de cada vez, com a linha do tempo |
| `ForcaBruta.cs` | todas as ordens possíveis e as conferências: o juiz |
| `Medidas.cs` | as tabelas que o medidor imprime e que os testes conferem |

## Como rodar

```
dotnet test testes/Escalonador.Testes/Escalonador.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- tudo
```

As medidas aceitam `juiz`, `politicas`, `comboio`, `quantum`, `fome` e `tudo`.

## Licença

MIT.
