# Mensageria — Barramento de Eventos de Integração

> **Status:** Plano. O barramento in-process existe hoje; o broker não.
> 🇺🇸 [English version](../en/messaging.md)
>
> Documento transversal: descreve uma decisão que nenhum módulo é dono sozinho.
> Módulos afetados: [Channels](../../modules/channels/pt-BR/product-plan.md) ·
> [Assistant](../../modules/assistant/pt-BR/product-plan.md) ·
> [Agenda](../../modules/agenda/pt-BR/product-plan.md)

---

## 1. Onde estamos

O Tars já abstrai o transporte. `IIntegrationEventBus` tem hoje uma implementação —
`InProcessIntegrationEventBus`, que despacha síncrono num escopo de DI novo — e o roteamento já é
feito pelo `IntegrationEventName` lógico, não pelo tipo .NET. Essa é exatamente a costura que uma
implementação sobre broker precisa.

Trocar o transporte é, por construção, uma mudança de composition root: produtores e consumidores não
mudam.

---

## 2. Por que um broker

O barramento in-process é suficiente enquanto todo trabalho entre módulos é curto e o processo é um
só. Três coisas quebram isso:

1. **O webhook precisa devolver 200 rápido.** Um `callback_query` do Telegram não pode esperar a
   Agenda concluir uma tarefa e publicar os fatos dela. O trabalho de domínio tem que ir para depois
   do ack.
2. **O Assistant é lento por natureza.** Transcrever um áudio e rodar tool-calling num modelo local
   leva de segundos a dezenas de segundos. Isso não pode acontecer dentro de um request HTTP nem
   segurando o loop de long polling.
3. **Perder um evento fica caro.** Hoje uma exceção num subscriber in-process derruba o fluxo
   inteiro ou é engolida. Com fila durável e dead-letter, uma falha é visível e reprocessável.

O que o broker **não** resolve, e nem deve: latência de leitura, consistência, e — principalmente —
agendamento (§6).

---

## 3. Topologia

Dois exchanges do tipo **topic**, uma fila por consumidor lógico, uma DLX para todas.

```
pandora.events    (topic)   fatos de domínio       identity.*  agenda.*  finances.*  notify.*
pandora.inbound   (topic)   o que entra pela borda inbound.interaction.*  inbound.message.*
pandora.dlx       (topic)   dead letters de todas as filas
```

A chave de roteamento é o `IntegrationEventName`. Nada além disso precisa ser configurado por
mensagem.

| Fila | Binding | Consumidor |
|---|---|---|
| `channels.dispatch` | `notify.user.requested` | Channels — entrega |
| `assistant.inbound` | `inbound.message.#` | Assistant (`prefetch=1`) |
| `agenda.interactions` | `inbound.interaction.agenda.#` | Agenda |
| `assistant.interactions` | `inbound.interaction.assistant.#` | Assistant |
| `channels.identity` | `identity.#` | Channels — subscribers de segurança |
| `<módulo>.events` | `agenda.# · finances.# · notes.#` | conforme o interesse de cada um |

Três coisas que essa tabela codifica:

- **A entrada é roteada, não transmitida.** `inbound.interaction.<módulo>.<ação>` entrega ao dono, e
  o dono é uma coluna que ele mesmo escreveu quando pediu o botão (ver
  [Channels §7.3](../../modules/channels/pt-BR/product-plan.md#73-tokens-de-interação)). Nenhum
  módulo filtra eventos que não são dele.
- **`prefetch=1` no Assistant.** Trabalho longo e um Ollama local que não deve ser afogado. As demais
  filas usam o prefetch padrão.
- **Uma DLX só.** Filas separadas dariam granularidade que ninguém vai olhar; um lugar só para "o que
  falhou" é o que se consulta na prática.

---

## 4. Outbox no produtor

Publicar depois do commit é um segundo commit que pode falhar sozinho — e o evento some sem ninguém
perceber. O padrão é conhecido e a implementação vai para o Tars:

`Pottmayer.Tars.Messaging.Outbox` — tabela de outbox em EF Core, gravada **na mesma transação** que a
mudança de estado, mais um relay em background que publica e marca enviado.

O produtor continua chamando `IIntegrationEventBus.PublishAsync`; a diferença é que a implementação
registrada grava na outbox em vez de ir direto ao broker. Nenhum handler muda.

---

## 5. Consumidor idempotente

Entrega no RabbitMQ é **at-least-once**. "Criar lembrete" não é operação que se queira duas vezes.

Todo consumidor deduplica por `EventId` — que já é a identidade estável da ocorrência no contrato do
Tars — contra uma tabela de eventos processados por módulo, na mesma transação do trabalho. Um evento
já visto é ack e descarte.

Onde já existe idempotência natural, ela vale e dispensa a tabela:

- `chn004_inbound_update` tem `provider_update_id` como PK — reprocessar um update do Telegram é
  inofensivo por construção.
- `chn006_notification` deduplica por `(correlation_id, channel)`.
- `chn003_interaction` tem `consumed_at` — um botão só age uma vez.

---

## 6. O que não vai no broker

**Agendamento.** Nada de `x-delayed-message`, nada de TTL + dead-letter para "me lembre às 14:00".
Uma mensagem atrasada não pode ser cancelada, reagendada nem corrigida quando o usuário muda de fuso
— e lembrete é exatamente a coisa que muda de horário. O agendamento fica no scheduler sobre tabela
do módulo dono (o `AlertSweepBackgroundService` da Agenda), publicando **na hora**. Isso é o
princípio D3 da Agenda e o C1 do Channels, e os dois estão certos.

**Request/response.** Pedir um token válido ao Integrations, ou os bytes de um áudio ao Channels, é
uma pergunta com resposta imediata, não um fato que aconteceu. Isso é chamada de porta síncrona,
in-process, e continua assim mesmo depois de o broker entrar. Se um dia esses módulos virarem
serviços, essas portas viram HTTP — não mensagem.

**Leitura.** Nenhum módulo replica dado de outro por evento para poder ler. Quem precisa ler chama a
porta.

---

## 7. Building blocks no Tars

| Projeto | Conteúdo |
|---|---|
| `Pottmayer.Tars.Messaging.RabbitMq` | `IIntegrationEventBus` sobre topic exchange, roteando pelo `IntegrationEventName`; host de consumidor com prefetch configurável, ack manual, DLX e política de retry; re-despacho da mensagem desserializada para os `IIntegrationEventHandler<T>` locais (o "último quilômetro" que a doc do Tars já descreve). |
| `Pottmayer.Tars.Messaging.Outbox` | Tabela de outbox em EF Core, `IIntegrationEventBus` que grava nela, e o relay em background. |

Nenhum dos dois conhece Pandora. A documentação vai no repositório do Tars, em `docs/messaging/`.

---

## 8. Quando isso entra

Depois da entrada funcionar in-process, não antes. A ordem é:

1. [Channels C4](../../modules/channels/pt-BR/product-plan.md#fase-c4--entrada) — entrada, triagem e
   roteamento por chave, ainda no barramento in-process. O roteamento por chave já é escrito como se
   houvesse broker, porque o `IntegrationEventName` é o mesmo nos dois transportes.
2. **Troca de transporte** — `Messaging.RabbitMq` + `Messaging.Outbox`, `docker-compose` ganha o
   serviço, composition root registra o bus novo. Nenhum handler muda.
   **Pronto quando:** derrubar o broker por um minuto não perde nenhum evento.
3. [Assistant A3](../../modules/assistant/pt-BR/product-plan.md) — quando o trabalho lento entra, a
   fila já existe.

Fazer na ordem inversa significaria depurar roteamento e infraestrutura ao mesmo tempo.

---

## 9. Extração como serviço

O broker não é um passo em direção a microserviços; é o que torna a decisão adiável sem custo. Se e
quando um módulo sair:

- **O primeiro candidato é o Assistant.** Trabalho longo, cadência de deploy própria, possível
  afinidade de GPU, e ele já conversa só por evento e por uma porta (`IInboundMediaReader`).
- **O segundo é o Channels**, se o ingress público justificar isolá-lo.
- Os dois já são donos do próprio `DbContext` e não tocam schema alheio.

O que a extração acrescenta, e que não vale pagar antes: um consumer group, uma decisão sobre
replicar ou consultar dado por API, e transformar as portas síncronas em HTTP.

---

## 10. Questões em aberto

1. **Um `docker-compose` só ou um perfil.** O RabbitMQ no compose de desenvolvimento é obrigatório ou
   opcional? Se o bus in-process continuar sendo uma opção de configuração, dá para desenvolver sem
   subir o broker — ao custo de dois caminhos que precisam funcionar. Inclinação: manter os dois,
   porque o in-process é o que os testes de integração usam.
2. **Versionamento de contrato.** `IntegrationEventName` já carrega sufixo de versão
   (`identity.account-activation.v1`). Falta a regra de quando bumpar e como conviver com duas
   versões numa fila. Decidir quando o primeiro contrato mudar de verdade.
3. **Observabilidade.** Correlação ponta a ponta entre o `correlation_id` do domínio e o message id
   do broker. Provavelmente um header e um enricher de log; ainda não desenhado.
