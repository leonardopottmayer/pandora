# Módulo Channels — Plano de Produto

> **Status:** Plano. Descreve a evolução — e a renomeação — de um módulo **já existente**
> (`Notifications`).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Agenda](../../agenda/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) ·
> [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## 1. Onde o módulo está hoje

`Pottmayer.Pandora.Modules.Notifications` já entrega uma fila durável de saída:

- Agregado `Notification` (`not001_notification`) com `Pending → Sending → Sent`, backoff
  exponencial, `MaxAttempts` e estado `Dead` de dead-letter.
- `NotificationEnqueuer` — renderiza um template e persiste uma linha, deduplicada por
  `correlation_id`.
- `NotificationDispatcherBackgroundService` — busca as linhas vencidas e envia.
- Subscribers dos eventos do Identity (ativação, reset/troca de senha, habilitar/desabilitar MFA).
- `SendNotificationRequested` — um POCO simples e serializável, escape hatch para envios ad-hoc.

E tem quatro limites duros:

1. **`Channel` tem exatamente um valor, `email`.** O smart enum foi escrito prevendo mais
   (`// Sms / Telegram / WhatsApp can be added later`), mas nada a jusante é ciente de canal.
2. **`Recipient` é um value object `Email`.** Um chat id do Telegram não passa por esse tipo.
3. **`NotificationContent` é `{ Subject, Body, IsHtml }`** — um record com formato de email. Telegram
   não tem assunto, tem seu próprio dialeto de markup e tem teclados inline que email não expressa.
4. **O módulo não tem noção de *para quem* é a notificação.** Ele conhece um endereço, não um usuário
   — então não existe lugar para pendurar "o Leonardo prefere Telegram para lembretes e email para
   faturas".

---

## 2. Para onde precisa ir

Tudo que a [Agenda](../../agenda/pt-BR/product-plan.md) e o
[Assistant](../../assistant/pt-BR/product-plan.md) fazem depende deste módulo aprender cinco coisas:

1. Enviar por **Telegram** como canal de primeira classe, não como um caso especial parafusado no
   email.
2. Endereçar um **usuário**, e resolver os canais desse usuário por conta própria.
3. Renderizar **por canal** — um corpo de email e uma mensagem de Telegram são artefatos diferentes a
   partir de uma chave de template.
4. Receber updates **de entrada**: vinculação de conta, callbacks de botão inline, mensagens de texto
   e áudios.
5. **Rotear** essa entrada para o módulo dono, em vez de transmiti-la para todos.

### 2.1 Por que o nome muda

O módulo se chama `Notifications`, e a partir do item 4 metade do que ele faz não é notificação. Ele
hospeda webhook, trata `/start`, guarda chat id e recebe áudio. O nome já estava apertado quando
"notificar" significava "mandar um email"; deixa de descrever o conteúdo quando significa "falar com
o usuário".

**`Channels`** nomeia o que o módulo passa a ser: dono da conversa com o humano, nos dois sentidos,
em todos os canais. A renomeação acontece na **fase C1**, enquanto o módulo ainda é pequeno — é a
coisa mais barata do roadmap agora e a mais cara depois que o Telegram entrar.

### 2.2 Por que um módulo, e não dois

A separação "transporte" versus "política de entrega" foi avaliada e **descartada**. A fronteira, se
traçada ali, corta no lugar errado três vezes:

- **O fan-out é um join.** Decidir se um lembrete vira email, Telegram ou os dois exige cruzar a
  preferência do usuário para aquela categoria (`chn005`) com os canais que ele tem habilitados e
  verificados (`chn001`). Duas tabelas que nunca são lidas separadas. Em módulos diferentes isso vira
  chamada cross-module no caminho quente de *toda* notificação, e a decisão de canal deixa de caber
  numa transação.
- **A porta teria um chamador só.** `IChannelTransport` é implementado duas vezes (email, Telegram) e
  chamado de exatamente um lugar: o dispatcher. É uma interface interna útil, e é isso que ela deve
  continuar sendo. Promovê-la a fronteira de módulo custa sete projetos e um schema por nada — e
  contraria a regra 2 do `CLAUDE.md` ("no abstractions for single-use code").
- **Os tokens de interação nascem de notificações.** Um botão *Feito* só existe porque alguma
  notificação o declarou. Num módulo só, `chn003_interaction` tem FK para a linha da fila que o gerou
  e "esse clique veio de qual mensagem" é uma coluna. Separado, é correlação por id opaco entre dois
  schemas.

O que de fato **não** pertence à conversa com o usuário é o *processamento* do que ele mandou —
transcrever, interpretar, executar comando. Isso está no [Assistant](../../assistant/pt-BR/product-plan.md)
e continua lá.

### 2.3 A costura fica dentro, e é real

Um módulo não quer dizer uma pilha indistinta. A divisão interna carrega a mesma fronteira, em
namespace em vez de em `.csproj`:

```
Pottmayer.Pandora.Modules.Channels.Application
├── Delivery/        preferências · fan-out · renderização · dispatcher · retry
├── Ingress/         drivers · triagem · vinculação · resolução de interação
└── Addressing/      chn001_user_channel — lido pelos dois

Pottmayer.Pandora.Modules.Channels.Infrastructure
├── Transports/      IChannelTransport: Email (MailKit) · Telegram (Tars) — interno
├── Ingress/         driver de long polling · controller de webhook — mesmo handler
└── Templates/       arquivos por chave/canal/locale, validados na inicialização
```

A regra que mantém isso honesto: nada em `Ingress` escreve na fila direto (publica evento ou chama
`Delivery` pela mesma superfície que um módulo externo usaria), e nada em `Delivery` conhece a Bot
API.

---

## 3. Princípios

1. **O Channels envia agora; ele não agenda.** Sem `ScheduledFor`, sem API de cancelamento. Quem
   quer entrega às 14:00 chama às 14:00. Isso mantém a fila simples e o módulo sem estado em relação
   ao tempo de negócio. *(C1)*
2. **O chamador nomeia um usuário e uma intenção, não um endereço.** Resolução de endereço, seleção
   de canal, horário de silêncio e opt-outs são política de entrega, e política de entrega mora aqui.
   *(C2)*
3. **Uma requisição vira N notificações.** "Email e Telegram" são duas linhas com um group id em
   comum — retry independente, falha independente, status honesto. *(C3)*
4. **Canais são portas.** Adicionar WhatsApp é uma implementação de `IChannelTransport` e uma
   variante de template. Nenhum `switch` no dispatcher. *(C4)*
5. **A entrada é classificada por estrutura, nunca por semântica.** O módulo resolve um id numa
   tabela e lê a coluna que o módulo dono escreveu; ele nunca interpreta o significado de uma ação.
   *(C5)*
6. **A renderização acontece no enqueue, não no envio.** O que saiu fica gravado, o retry reenvia
   byte a byte o mesmo conteúdo, e mudar um template amanhã não reescreve o histórico. *(C6)*

---

## 4. Nomenclatura e coordenadas

| Item | Valor |
|---|---|
| Projetos backend | `Pottmayer.Pandora.Modules.Channels.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `channels` (renomeado de `notifications`) |
| Prefixo de tabela | `chnXXX_`, PK `uuid_generate_v7()` |
| Base da API | `/api/v{version}/channels` |
| Frontend | seção *Notificações* nas configurações (ver §9) |
| Migrations | `migrations/migrations/channels/` |
| Building block no Tars | `Pottmayer.Tars.Communication.Telegram.*` (ver §10) |

---

## 5. Mudanças no modelo

### 5.1 `Channel`

```csharp
public static readonly Channel Email    = new("email");
public static readonly Channel Telegram = new("telegram");
```

Mais `FromValue` e uma coleção `All` para iteração. A forma de smart enum já está certa — é uma
mudança de duas linhas mais o braço do parse.

### 5.2 `Recipient` → `NotificationAddress`

O `Email` (VO compartilhado) é substituído no agregado por um value object ciente de canal:

```csharp
public sealed record NotificationAddress(Channel Channel, string Value)
```

A validação delega por canal: um endereço de email é validado pelo VO `Email` existente; um endereço
de Telegram é um chat id numérico. A coluna continua um único `text` — só o invariante muda.

### 5.3 Conteúdo passa a ser por canal

`NotificationContent { Subject, Body, IsHtml }` não cobre Telegram. As colunas `Subject`/`Body`/`IsHtml`
são **mantidas** para email e acompanhadas de uma nova `rendered_payload jsonb` para canais
estruturados, em vez de uma reescrita destrutiva:

| Canal | Payload renderizado |
|---|---|
| `email` | `{ subject, body, isHtml }` — exatamente o conteúdo de hoje, inalterado na saída. |
| `telegram` | `{ text, parseMode, disableNotification, buttons: [{ interactionId, label }] }` |

Repare que o botão renderizado carrega o **id da interação**, não a ação: o mapeamento ação → id
acontece no enqueue, contra `chn003` (ver §7.3).

### 5.4 Catálogo de schema

**Endereçamento**

**`chn001_user_channel`** — onde um usuário pode ser alcançado.

| Coluna | Observações |
|---|---|
| `user_id`, `channel` | Únicos em conjunto. Um endereço por canal por usuário. |
| `address` | Endereço de email, ou chat id do Telegram. |
| `is_verified`, `verified_at` | Email herda a ativação do Identity; Telegram é verificado pelo handshake de vinculação. |
| `is_enabled`, `disabled_reason` | O botão de desligar do usuário — e o desligamento automático depois de uma falha permanente. |
| `metadata` | jsonb — username/primeiro nome do Telegram para exibir nas configurações. |

**`chn002_channel_link_token`** — o handshake do Telegram.

| Coluna | Observações |
|---|---|
| `user_id`, `channel`, `token` | Token aleatório curto, único. |
| `expires_at`, `consumed_at` | Uso único, TTL de 15 minutos. |

**Entrada**

**`chn003_interaction`** — um botão registrado, e a rota de volta dele.

| Coluna | Observações |
|---|---|
| `user_id` | Dono. Conferido contra o remetente do callback. |
| `owner_module` | `agenda`, `assistant`, … Escrito por quem pediu o botão; lido para montar a chave de roteamento. |
| `action` | `task_done`, `snooze_1h`, `confirm`, … Opaco para este módulo. |
| `payload` | jsonb, opaco. Devolvido intacto ao dono. |
| `notification_id` | FK para a linha da fila que gerou o botão. Nulo para botões de mensagens de sistema. |
| `expires_at`, `consumed_at` | Uso único. Um segundo clique é "expirado", não um segundo comando. |

**`chn004_inbound_update`** — idempotência e trilha da entrada.

| Coluna | Observações |
|---|---|
| `provider`, `provider_update_id` | PK composta. Para o Telegram, o `update_id`. Reprocessar é inofensivo por construção. |
| `raw` | jsonb do update cru, para depuração. Retenção curta (ver questão aberta 3). |
| `user_id` | Resolvido de `chn001`; nulo quando o chat é desconhecido. |
| `classification` | `interaction` \| `command` \| `message` \| `discarded`. |
| `received_at`, `processed_at` | |

**Entrega**

**`chn005_notification_preference`** — política de entrega por categoria.

| Coluna | Observações |
|---|---|
| `user_id`, `category` | ex.: `agenda.reminder`, `agenda.task`, `identity.security`, `finances.statement`. |
| `channels` | Array ordenado. Vazio ⇒ mudo. |
| `quiet_hours_start`, `quiet_hours_end` | No fuso do usuário (vindo das preferências do Identity). |
| `quiet_hours_behaviour` | `suppress` \| `deliver_anyway`. Ver §5.5. |

Notificações de segurança (`identity.*`) **não são configuráveis** — um email de reset de senha não é
preferência. O registro de categorias marca essas como obrigatórias.

**`chn006_notification`** — a fila durável. É a `not001_notification` de hoje, renomeada, mais
`rendered_payload`, `group_id` e `provider_message_id`.

| Coluna nova | Observações |
|---|---|
| `group_id` | Comum às N linhas geradas por uma requisição. |
| `rendered_payload` | jsonb, para canais estruturados (§5.3). |
| `provider_message_id` | O id da mensagem no provedor, depois do envio. Permite correlacionar uma resposta encadeada (ver §7.5). |

O índice de dedup migra de `correlation_id` para **`(correlation_id, channel)`** — senão o segundo
canal é engolido como duplicata. Essa é a única migração sobre a tabela existente que exige cuidado.

### 5.5 Horário de silêncio

`defer_to_end` foi **descartado**. Adiar uma entrega até de manhã é agendamento, e o princípio C1 diz
que agendamento não mora aqui. Restam `suppress` e `deliver_anyway`; quem quiser mesmo o adiamento
reagenda do lado de quem chamou, que é onde o scheduler já existe.

---

## 6. Templates

### 6.1 Quem preenche o quê

A corrente já existe no código e está certa; o que muda é a dimensão de canal e onde os arquivos
moram.

| Participante | Sabe | Não sabe |
|---|---|---|
| **Produtor** (Identity, Agenda, …) | O fato: `PasswordResetRequested(userId, email, token, locale)`. | Que existe email, template ou canal. |
| **Subscriber** (aqui) | O mapeamento fato → `TemplateKey` + payload plano + categoria. Monta valores derivados (a URL de reset, a partir das options). | Como o texto é escrito. |
| **Renderer** (aqui) | O arquivo, por `(chave, canal, locale)`. Substitui placeholders e mais nada. | De onde veio o payload. |
| **Fila** (aqui) | O conteúdo já renderizado. | Tudo o resto. |

Existem **dois caminhos** para chegar aqui, e a regra de qual usar é simples: **quem é dono dos
botões é dono do `NotifyUserRequested`**.

- **Sem botões** (`identity.*`): o produtor publica um fato e um subscriber *deste* módulo mapeia para
  template. É como o Identity já funciona, e continua assim.
- **Com botões** (`agenda.*`, `assistant.*`): o chamador publica `NotifyUserRequested` direto, porque
  a ação de cada botão e seu payload são domínio dele. Se um subscriber daqui tivesse que inventar os
  botões, este módulo precisaria saber o que é uma tarefa — que é exatamente o acoplamento que o
  princípio C5 existe para evitar.

O `NotificationEnqueuer` de hoje já faz exatamente isso — `renderer.Render(...)` antes de
`Notification.Queue(...)`. A assinatura ganha o canal:

```csharp
RenderedContent Render(TemplateKey key, Channel channel, string locale,
                       IReadOnlyDictionary<string, string> payload);
```

### 6.2 Onde eles moram

**Em arquivos no repositório**, embutidos como recurso, não em banco. Eles são conteúdo que passa por
code review e precisa acompanhar a versão do código que os preenche; um catálogo em banco dá hot
reload que não é necessário e tira o texto do diff.

O que muda em relação ao `switch` do `InMemoryNotificationTemplateRenderer` é a escala: chave × canal
× locale não cabe num `switch`, mas cabe numa árvore:

```
Templates/
├── password-reset/
│   ├── email.pt-BR.txt          (linha 1 = assunto, resto = corpo)
│   └── email.en.txt
└── agenda.reminder.due/
    ├── email.pt-BR.txt
    ├── email.en.txt
    ├── telegram.pt-BR.txt
    └── telegram.en.txt
```

Um registro enumera as chaves conhecidas × canais permitidos por categoria × locales e **falha o
startup** se faltar variante. O renderer em memória de hoje já enumera seu catálogo, então isso é uma
passada de validação sobre ele.

Toda lógica sai do renderer: o que hoje é `options.PasswordResetUrlTemplate.Replace("{token}", ...)`
passa a ser feito pelo subscriber, que entrega `resetUrl` já pronta no payload. O renderer vira
substituição de `{{resetUrl}}` e escolha de arquivo — e o teste dele fica trivial.

### 6.3 Botões

Rótulo é conteúdo; ação é domínio.

- O **chamador** declara quais ações oferecer e o payload de cada uma (`NotificationButton(action,
  payload)`).
- O **template** do canal traz os rótulos por ação, porque rótulo tem locale:
  `buttons.task_done = ✓ Feito`.
- O **merge** acontece no render. Canais que não suportam botão descartam a lista sem erro.

---

## 7. Entrada

### 7.1 Dois drivers, um handler

O webhook precisa de HTTPS público, que não existe no começo. Long polling cobre esse período — e
continua útil para sempre, porque funciona atrás de NAT sem túnel nenhum, que é o cenário de
desenvolvimento.

`getUpdates` aceita `timeout=30`: a requisição fica **pendurada** até chegar update ou estourar o
tempo. Não é polling curto em loop; é uma conexão aberta que devolve na hora. Latência praticamente
igual à do webhook.

```
Job de long polling ─┐
                     ├─► IInboundUpdateHandler ─► chn004 ─► triagem (§7.2)
Controller de webhook┘
```

Nenhum código além do driver sabe qual está ativo. `Channels:Telegram:Ingress = LongPolling | Webhook`
é a única diferença.

Três restrições que o long polling impõe e que precisam estar escritas:

- `getUpdates` e `setWebhook` são **mutuamente exclusivos**, e dois consumidores no mesmo bot token
  recebem `409 Conflict`. O job é singleton; uma segunda réplica exigiria eleição de líder.
- O **offset é o ack**. Ele avança na mesma transação que grava a linha em `chn004`; o processamento
  vem depois. Crash no meio reprocessa, e `provider_update_id` como PK torna isso inofensivo.
- O Telegram guarda updates não confirmados por **24 h**, então uma queda de algumas horas não perde
  nada.

O controller de webhook, quando entrar, é `POST /api/v{version}/channels/telegram/webhook` — anônimo,
protegido pelo header `X-Telegram-Bot-Api-Secret-Token`, verificado em tempo constante.

### 7.2 Triagem

Passo zero, antes de qualquer classificação: **resolver `chat_id` → `user_id`** por `chn001`. Chat
desconhecido recebe uma mensagem genérica e é descartado — sem enumeração.

Depois, três saídas, decididas pela **estrutura do update**, nunca pelo conteúdo:

| Update | Ação | Destino |
|---|---|---|
| `callback_query` | Resolve o `callback_data` em `chn003`; confere dono, validade e uso; responde `answerCallbackQuery`. | Publica `inbound.interaction.<owner_module>.<action>` |
| `/start <token>`, `/unlink`, `/status`, `/help` | Tratado localmente. Vinculação, desvinculação, diagnóstico. | Nunca vira evento. |
| Texto livre, áudio, foto | Normaliza. | Publica `inbound.message.<channel>` |

Qualquer outro `/comando` recebe "não conheço esse comando" e para ali.

### 7.3 Tokens de interação

Este é o mecanismo que substitui o broadcast, e ele existe por uma razão concreta antes de qualquer
razão arquitetural: `callback_data` do Telegram tem **64 bytes**. Não cabe userId, itemId, nome da
ação e módulo de origem. A tabela de indireção é obrigatória — e, uma vez que ela existe, é o lugar
certo para guardar quem é o dono do botão.

**Ida.** No enqueue, para cada botão declarado, grava uma linha `chn003` com
`(user_id, owner_module, action, payload, notification_id, expires_at)`. O `callback_data`
renderizado é o id da linha.

**Volta.** O callback resolve o id, e a chave de roteamento sai da coluna `owner_module`:

```
inbound.interaction.agenda.task_done
```

Só a fila da Agenda está ligada a `inbound.interaction.agenda.#`. Nenhum outro módulo acorda, e
nenhum módulo filtra.

O que a tabela dá de brinde: expiração (um botão de ontem não age hoje), uso único (clicar "Feito"
duas vezes é um caso tratado em vez de dois comandos), e a FK para a notificação de origem.

**A frase que resume o desenho:** a resposta não volta para a notificação, volta para o **botão**.
Uma notificação não tem canal de retorno; o botão tem, porque foi *registrado* na ida.

### 7.4 Vinculação

1. Usuário abre Configurações → Notificações → *Conectar Telegram*.
2. O backend emite um `chn002_channel_link_token` e devolve `https://t.me/<bot>?start=<token>`.
3. Usuário toca; o Telegram manda `/start <token>`.
4. A triagem consome o token, grava `chn001` com o chat id e `is_verified`, e responde com uma
   mensagem de confirmação.

O token é a única coisa que amarra um chat a uma conta, é de uso único e vida curta, e o chat id
nunca é aceito vindo do cliente.

### 7.5 Respostas encadeadas *(opcional, fase C4)*

Com `provider_message_id` gravado na linha da fila, uma resposta encadeada no Telegram
(`reply_to_message_id`) permite enriquecer o `inbound.message` com a correlação da notificação de
origem. O Assistant ganha contexto — "era sobre *aquele* lembrete" — sem que ninguém precise
interpretar texto para descobrir isso.

---

## 8. Contratos

Publicados de `Channels.Contracts`. Todos são POCOs simples e serializáveis, sem value object de domínio.

### 8.1 Entrada de trabalho

```csharp
public sealed record NotifyUserRequested(
    Guid   EventId,
    DateTimeOffset OccurredAt,
    Guid   UserId,
    string Category,                   // → consulta de preferências
    string TemplateKey,
    string? Locale,                    // null ⇒ preferência do usuário
    IReadOnlyList<string>? Channels,   // null ⇒ preferência do usuário para a categoria
    IReadOnlyDictionary<string,string> Payload,
    IReadOnlyList<NotificationButton>? Buttons,
    Guid   CorrelationId) : IIntegrationEvent;

public sealed record NotificationButton(string OwnerModule, string Action, string? Payload);
```

O `SendNotificationRequested` atual permanece para envios admin/ad-hoc com endereço explícito.

### 8.2 Saída de entrada

```csharp
[IntegrationEventName("inbound.interaction")]   // chave completa: inbound.interaction.{module}.{action}
public sealed record InboundInteractionReceived(
    Guid   EventId, DateTimeOffset OccurredAt,
    Guid   UserId, string Channel,
    string OwnerModule, string Action, string? Payload,
    Guid?  SourceCorrelationId) : IIntegrationEvent;

[IntegrationEventName("inbound.message")]       // chave completa: inbound.message.{channel}
public sealed record InboundMessageReceived(
    Guid   EventId, DateTimeOffset OccurredAt,
    Guid   UserId, string Channel,
    string? Text, string? MediaRef, string? MediaMimeType,
    Guid?  InReplyToCorrelationId) : IIntegrationEvent;
```

`MediaRef` é opaco — para o Telegram, o `file_id`. Os bytes são buscados por porta:

```csharp
public interface IInboundMediaReader
{
    Task<Stream> OpenAsync(string channel, string mediaRef, CancellationToken ct = default);
}
```

É a única coisa que o Assistant chama neste módulo, e é o que permite a ele nunca saber que Telegram
existe.

### 8.3 Falha de entrega

```csharp
public sealed record UserChannelDisabled(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid UserId, string Channel, string Reason) : IIntegrationEvent;
```

Um erro permanente do provedor (Telegram *chat not found*, *bot blocked*) marca a linha como `Dead`
na hora, desabilita aquele canal do usuário com motivo e publica o fato — não adianta tentar cinco
vezes contra um bot bloqueado, e o usuário precisa saber que parou.

---

## 9. Frontend

Sem tela própria. O módulo contribui uma seção **Notificações** nas configurações:

- Conectar/desconectar Telegram, com status do vínculo.
- Canais por categoria, com o aviso de canal desabilitado e o motivo.
- Horário de silêncio.
- Histórico de entregas, filtrável, com status e último erro.
- Envio de teste por canal.

---

## 10. Building block no Tars: `Communication.Telegram`

Espelha a divisão existente `Communication.Email` / `Communication.Email.MailKit`, que é o padrão
estabelecido no Tars.

| Projeto | Conteúdo |
|---|---|
| `Pottmayer.Tars.Communication.Telegram.Abstractions` | `ITelegramClient`, `TelegramMessage`, `InlineKeyboard`/`InlineButton`, `TelegramUpdate` e afins, `TelegramSendResult`, `TelegramException` com distinção permanente/transiente. |
| `Pottmayer.Tars.Communication.Telegram` | Implementação da Bot API sobre `HttpClient`: `sendMessage`, `answerCallbackQuery`, `getUpdates`, `getFile`/download, `setWebhook`, escape de MarkdownV2, validação do secret token, binding de options, extensão de DI. |

Fica deliberadamente fino: transporte mais modelos. Templates, retries, endereçamento, triagem e
persistência são assunto do Pandora e já existem aqui. A documentação vai no repositório do Tars
(`docs/communication/telegram.md`), junto do building block de email.

Nenhum outro building block novo é assumido. Os eventos de integração deste plano trafegam pelo
barramento in-process que o Tars já oferece; ver o
[doc de mensageria](../../../architecture/pt-BR/messaging.md).

---

## 11. Roadmap

### Fase C1 — Renomear *(barata agora, cara depois)*
- `Notifications` → `Channels`: projetos, schema, prefixo de tabela, rotas, migrations.
  `not001_notification` → `chn006_notification`.
- `Channel.Telegram` no smart enum; `Recipient` → `NotificationAddress`.
- Atualizar as referências nos docs de Finances (`architecture.md`, `overview.md`,
  `implementation-status.md`, `jobs-and-integration.md`), que hoje nomeiam o módulo antigo.
- **Pronto quando:** os emails de Identity continuam chegando e nada além do nome mudou.

### Fase C2 — Saída por Telegram
- `Tars.Communication.Telegram`; `IChannelTransport` interno com os dois transports.
- Renderização por canal; templates saem do `switch` para arquivos; validação de catálogo no startup.
- `chn001`, `chn002`; vinculação por deep link.
- Tratamento de erro permanente que desabilita o canal e publica `UserChannelDisabled`.
- **Pronto quando:** um envio de teste chega num chat vinculado, com um botão que ainda não faz nada.

### Fase C3 — Preferências e fan-out
- `chn005`; contrato `NotifyUserRequested`; fan-out no enqueuer; dedup por
  `(correlation_id, channel)`.
- Subscribers do Identity migrados para o caminho por usuário.
- **Pronto quando:** uma requisição vira duas linhas com retry independente.

### Fase C4 — Entrada
- `chn003`, `chn004`; `IInboundUpdateHandler`; driver de long polling; triagem; roteamento por chave.
- `IInboundMediaReader`; `provider_message_id` e respostas encadeadas.
- Primeiro consumidor: Agenda (`task_done`, `snooze_1h`).
- **Pronto quando:** apertar *Feito* fecha a tarefa, e o segundo clique diz que expirou.

### Fase C5 — Operação
Polimento e observabilidade. O módulo é totalmente usável sem esta fase, então ela chega aos poucos.

- **Purga de retenção do raw — feita.** Um job diário (`InboundUpdateRetentionBackgroundService`)
  limpa o payload cru das linhas de `chn004` mais velhas que a janela de retenção, colocando `raw`
  em null — a linha em si fica, por ser o guard de idempotência e o offset do long polling. Duas
  configs em `Channels:RawRetention`: `Enabled` (ligado por padrão) e `RetentionDays` (padrão 7).
  Fecha a questão em aberto 3.
- **Histórico de entregas — feito.** `GET /channels/notifications` (filtrável por status, canal,
  categoria e data, paginado) mais uma tabela de histórico no settings, pra "meu lembrete saiu
  mesmo?" ter resposta. Exigiu atribuir a linha da fila a um usuário: `chn006` ganhou `user_id` e
  `category`, carimbados no enqueue (o fan-out do `NotifyUserRequested`, os e-mails de
  segurança/conta do Identity, e os envios de teste).
- **Métricas — planejado, depois.** Profundidade da fila, latência de despacho, taxa de falha por
  canal, updates descartados. Depende de plugar OpenTelemetry no Host, que é tarefa transversal e não
  só do Channels.
- **Envio de teste por canal — já feito** na C2 (`POST /channels/{channel}/test`).

#### Talvez no futuro *(não planejado)*
- **Driver de webhook.** O long polling cobre o ingress em qualquer lugar, inclusive atrás de NAT,
  então o webhook só se paga quando o homelab for exposto em HTTPS público. O client do Tars já
  suporta (`SetWebhookAsync`/`DeleteWebhookAsync`), então fica uma adição pequena e adiável — o
  controller só entregaria os updates recebidos à mesma triagem que o long polling usa.
- **Retry manual de linha morta.** Re-enfileirar uma notificação morta pela UI. Não compensa a
  superfície enquanto dead-letters são raras e já inspecionáveis no log; revisitar se isso mudar.

### Extração como serviço — *descartada*
Esta fase planejava o módulo saindo do monolito. Não é mais objetivo: o Pandora continua um
deployable só, in-process. Ver o
[doc de mensageria §6](../../../architecture/pt-BR/messaging.md#6-o-que-foi-descartado-e-por-quê).

O que a fase descrevia como preparação fica de qualquer forma, por mérito próprio: contratos POCO,
`ChannelsDbContext` próprio, nenhum acesso a schema alheio. É isso que mantém o módulo honesto dentro
do monolito — e, de quebra, o que tornaria a decisão reversível se um dia precisasse ser.

---

## 12. Questões em aberto

1. **Um endereço por canal.** Dois chats de Telegram (pessoal + um grupo) está fora de escopo; a
   constraint única faz disso uma mudança futura deliberada.
2. **Se o Finances entra.** Os eventos de fatura/importação dele estão documentados como planejados
   mas não publicados. Assim que a C3 entrar, eles ganham categorias de graça — vale uma fase pequena
   de follow-up lá.
3. ~~**Retenção do `raw` em `chn004`.**~~ **Decidido (C5):** um job diário coloca `raw` em null
   quando mais velho que `Channels:RawRetention:RetentionDays` (padrão 7), mantendo a linha;
   ligado/desligado por `Channels:RawRetention:Enabled`.
4. **Categorias como registro tipado ou string.** Hoje `Category` é string no contrato. Um registro
   central daria validação no startup ("a Agenda declarou `agenda.reminder`") ao custo de um lugar a
   mais para tocar quando um módulo nasce. Inclinação: string até doer.
