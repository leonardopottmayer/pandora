# Mensageria — Barramento de Eventos de Integração

> **Status:** Decidido. Tudo roda in-process, num deployable só. Não há broker nem workers
> separados.
> 🇺🇸 [English version](../en/messaging.md)
>
> Documento transversal: descreve uma decisão que nenhum módulo é dono sozinho.
> Módulos afetados: [Channels](../../modules/channels/pt-BR/product-plan.md) ·
> [Assistant](../../modules/assistant/pt-BR/product-plan.md) ·
> [Agenda](../../modules/agenda/pt-BR/product-plan.md)
> Ver também: [Como o Pandora está ligado ao Tars](tars-wiring.md) para as chamadas `AddTars*` concretas por trás de tudo abaixo.

---

## 1. Onde estamos

O Tars já abstrai o transporte. `IIntegrationEventBus` é implementado por `OutboxIntegrationEventBus`
(`Pottmayer.Tars.Messaging.EntityFrameworkCore`): publicar grava uma linha na própria transação do
produtor, e um relay por banco produtor drena para os handlers in-process num escopo de DI novo. O
roteamento é feito pelo `IntegrationEventName` lógico, não pelo tipo .NET. Ver
[tars/docs/messaging](../../../../tars/docs/messaging/overview.md) para o mecanismo em si e
[tars/docs/messaging/outbox.md](../../../../tars/docs/messaging/outbox.md) para o outbox; a fiação
específica do Pandora está em
[`OutboxRegistration.cs`](../../../backend/src/Host/Pottmayer.Pandora.Host/OutboxRegistration.cs).

Esse é o mecanismo inteiro. Um produtor publica um fato, a linha cai na tabela de outbox do banco
dele, o relay desse banco pega a linha, e os handlers registrados para aquele nome rodam. Nada
atravessa fronteira de rede.

---

## 2. A decisão: in-process, um processo só

O Pandora é um monolito modular e continua sendo. Os módulos conversam pelo barramento in-process e
por portas síncronas; nada atravessa fronteira de rede para alcançar outro módulo.

O motivo é o tamanho do problema. Isso é um sistema pessoal, com um usuário e um deploy. Um broker
compra durabilidade entre reinícios de processo e back-pressure entre máquinas — nenhum dos dois é
problema aqui — e cobra infraestrutura para rodar, um segundo caminho de entrega para manter
funcionando, e uma classe de falha (mensagem parada numa fila que ninguém olha) mais difícil de
depurar do que a exceção que ela substituiu. Os módulos que consumiriam das filas estão no mesmo
processo que os que publicam nelas.

O que isso **não** abre mão é da costura. Produtores e consumidores só conhecem o
`IIntegrationEventBus` e um nome lógico de evento. Se o formato do problema mudar, outro transporte é
uma mudança de composition root e nenhum handler se move. É isso que torna essa decisão barata de
revisitar, em vez de uma parede — ver §6.

---

## 3. Assincronia sem broker

Algum trabalho genuinamente não pode acontecer dentro da requisição que o disparou: um handler HTTP
que precisa responder rápido, ou uma tarefa que leva dezenas de segundos. A resposta é um **job no
módulo dono do trabalho**, sobre a tabela desse módulo — não uma fila na frente de outro processo.

O formato já está no código três vezes:

| Módulo | Tabela | Job |
|---|---|---|
| Channels | `chn006_notification` | `NotificationDispatcherBackgroundService` drena o que venceu |
| Finances | `fin011_pending_transaction` | varredura de recorrência gera o que venceu |
| Agenda *(planejado)* | `agd00x_alert` | `AlertSweepBackgroundService` dispara o que venceu |

O padrão em todos: quem chama grava uma linha durável na mesma transação da sua mudança de estado e
retorna; um `PeriodicTimer` num `BackgroundService` pega a linha num escopo novo, faz o trabalho e
registra o resultado. Retry com backoff, contador de tentativas e estado morto vivem na tabela — que
é exatamente a durabilidade que a fila ofereceria, com a diferença de que o estado é uma linha que
dá para consultar, corrigir e reprocessar na mão.

Duas regras impedem isso de virar um broker particular por módulo:

- **A tabela é do módulo que faz o trabalho**, não do que pediu. O Channels é dono da fila de
  notificações porque quem envia é ele; a Agenda é dona da varredura de alertas porque quem decide
  *quando* é ela.
- **Job não é scheduler do domínio alheio.** Ele drena o que venceu nas tabelas dele e publica fatos;
  não aceita "roda isso pra mim depois" de outro módulo.

---

## 4. Idempotência

O relay do outbox entrega **at-least-once**: um crash entre o despacho e marcar a linha como
processada reproduz o evento na próxima varredura. Handlers precisam ser idempotentes por `EventId`,
igual seriam atrás de um broker — ver
[a garantia at-least-once do relay](../../../../tars/docs/messaging/outbox.md#the-relay) para o
mecanismo. A mesma disciplina de at-least-once é necessária onde um **job reprocessa**, que é em todo
o §3.

Onde existe idempotência natural, ela resolve e nenhuma tabela extra é necessária:

- `chn006_notification` deduplica por `(correlation_id, channel)`.
- `chn004_inbound_update` *(planejado)* tem `provider_update_id` como PK — reprocessar um update do
  Telegram é inofensivo por construção.
- `chn003_interaction` *(planejado)* tem `consumed_at` — um botão age uma vez só.

Todas são chave natural sobre o próprio trabalho, que é a forma a preferir. Uma tabela genérica de
eventos processados é o plano B para quando não existe chave assim, e até aqui nenhum módulo precisou
de uma.

---

## 5. O que não passa pelo barramento

**Agendamento.** "Me lembre às 14:00" não é evento a ser atrasado; é uma linha com hora de
vencimento. O agendamento fica no job sobre tabela do módulo dono (o `AlertSweepBackgroundService` da
Agenda), que publica **na hora em que dispara**. Lembrete é exatamente a coisa que muda de horário, e
uma linha pode ser reagendada, cancelada ou corrigida quando o usuário muda de fuso. Isso é o
princípio D3 da Agenda e o C1 do Channels, e os dois estão certos.

**Request/response.** Pedir um token válido ao Integrations, ou os bytes de um áudio ao Channels, é
uma pergunta com resposta imediata, não um fato que aconteceu. Isso é chamada de porta síncrona.

**Leitura.** Nenhum módulo replica dado de outro por evento para poder ler. Quem precisa ler chama a
porta.

---

## 6. O que foi descartado, e por quê — e o que voltou depois

Três coisas estavam planejadas aqui e foram descartadas; uma delas voltou depois, em outra forma.

**Um broker RabbitMQ** — dois topic exchanges, uma fila por consumidor lógico, uma DLX comum. Segue
descartado: os problemas que ele vinha resolver são resolvidos pelo §3 a uma fração do custo. Ack
rápido do webhook vira linha mais job, trabalho lento do Assistant vira linha mais job, e evento
perdido vira linha com contador de tentativas. O que o broker acrescenta além disso é infraestrutura
para rodar e vigiar, que esse sistema não tem volume para justificar.

**O padrão outbox — voltou, sem broker.** O raciocínio original ("sem broker não há segundo commit a
perder") estava errado: o próprio despacho não é livre de um segundo commit. Sem outbox, um produtor
que commita sua mudança de estado e depois chama `IIntegrationEventBus.PublishAsync` in-process ainda
pode falhar entre os dois passos — a mudança de estado sobrevive, o fato nunca é publicado, e nada
registra que deveria ter sido. O outbox fecha exatamente essa brecha, com ou sem broker: o
`Pottmayer.Tars.Messaging.EntityFrameworkCore` grava o evento como uma linha na *mesma transação* da
mudança de estado, e um relay (`BackgroundService`) por banco produtor drena para os handlers locais.
O Pandora adotou isso no tars 0.0.8 — ver
[`OutboxRegistration.cs`](../../../backend/src/Host/Pottmayer.Pandora.Host/OutboxRegistration.cs),
ligado a Identity, Channels, Agenda e Integrations. Mecanismo completo:
[tars/docs/messaging/outbox.md](../../../../tars/docs/messaging/outbox.md). Isso **não** reintroduz um
broker nem um segundo processo — o relay roda no mesmo deployable e entrega para os mesmos handlers
in-process de antes; só o último passo do despacho mudou.

**Extração como serviço** — Assistant primeiro, Channels depois. Segue descartado como objetivo. Os
módulos mantêm as propriedades que a tornariam possível, porque essas propriedades valem por si: cada
um é dono do próprio `DbContext`, não toca schema alheio, publica contratos POCO e conversa por
portas. Isso é bom design modular, não preparação para uma separação. Vale notar que o outbox acima é
também o que os docs do próprio tars apontam como o que fica parado quando um módulo *é* extraído
depois — ver a observação em `OutboxRegistration.cs`: a troca de transporte
(`AddTarsMassTransitRabbitMq`) substitui só esse método de registro, e os contratos, produtores e
consumidores são reaproveitados como estão.

Nada disso é porta que trancou. Produtores e consumidores conhecem uma interface de barramento e um
nome lógico de evento, então no dia em que aparecer um motivo real — carga sustentada, cadência de
deploy genuinamente separada, trabalho que precisa de uma máquina que essa não é — o transporte muda
no composition root. O motivo tem que aparecer primeiro.

---

## 7. Questões em aberto

1. **Versionamento de contrato.** O `IntegrationEventName` já carrega sufixo de versão
   (`identity.account-activation.v1`). Falta a regra de quando bumpar. Menos urgente in-process, onde
   produtor e consumidores sobem juntos, mas o sufixo está nos contratos e deveria significar alguma
   coisa. Decidir quando o primeiro contrato mudar de verdade.
2. **Visibilidade de falha.** Uma exceção num handler in-process aparece no fluxo de quem chamou, o
   que é honesto mas nem sempre é onde alguém está olhando. As tabelas de job carregam `last_error` e
   estado morto; falta um lugar só que responda "o que falhou recentemente" entre os módulos.
