# Módulo Assistant — Plano de Produto

> **Status:** Plano. O backend do módulo Assistant ainda não existe; o building block `Tars.Ai`
> (Gemini) e o armazenamento da chave no Integrations **já existem** — ver o plano de execução.
> 📋 Plano de execução (etapas e passos): [execution-plan.md](execution-plan.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Agenda](../../agenda/pt-BR/product-plan.md) ·
> [Channels](../../channels/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## 1. O que o módulo faz

**Assistant** transforma linguagem natural — digitada ou falada — em comandos executados contra os
próprios módulos do Pandora.

> "lembra de ligar pro dentista amanhã às 9"
> → `create_reminder(title: "Ligar pro dentista", remindAt: 2026-08-16T09:00-03:00)`
> → uma linha real em `agd006_reminder`, alerta agendado, confirmação de volta no mesmo chat.

Duas superfícies:

- **Telegram** — mensagens de texto e **áudios**. A principal: é onde as notificações já chegam, então o
  ciclo se fecha num app só.
- **Web** — uma barra de comando no client, mais gravação de áudio, usando o mesmo pipeline.

Provedores são intercambiáveis por porta. O alvo atual é **Gemini** (hospedado); **OpenAI** entra como
"mais um provedor" quando fizer sentido. A ideia de um modelo local (**Ollama**) foi abandonada — não
compensa em custo/qualidade. O provedor é escolhido pelo usuário, com a chave guardada no Integrations.

### O que ele não é

Não é um chatbot com opiniões sobre a vida do usuário, e não é lugar onde regra de negócio mora. Toda
ação que ele toma é um comando que já existe e que a UI web também consegue invocar. Se o assistente
consegue fazer algo que a API não consegue, isso é um bug.

---

## 2. Nomenclatura e coordenadas

| Item | Valor |
|---|---|
| Projetos backend | `Pottmayer.Pandora.Modules.Assistant.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `assistant` |
| Prefixo de tabela | `astXXX_`, PK `uuid_generate_v7()` |
| Base da API | `/api/v{version}/assistant` |
| Frontend | `client-web/src/modules/assistant` |
| Migrations | `migrations/migrations/assistant/` |
| Building block no Tars | `Pottmayer.Tars.Ai.*` (ver §6) |

---

## 3. Princípios

1. **O LLM escolhe; o Pandora decide.** A única saída do modelo é uma *tool call*: um nome e argumentos
   tipados. Validação, autorização e execução são código de aplicação comum. Um comando alucinado falha
   na validação como qualquer request ruim. *(A1)*
2. **Cada módulo é dono dos seus comandos.** Cada módulo publica um catálogo de comandos a partir do seu
   `Abstractions`. O Assistant descobre na inicialização e nunca chumba o que é um lembrete. Colocar o
   Finances no assistente é um registro, não uma reescrita. *(A2)*
3. **Ações de escrita são confirmadas; o limiar é por comando.** Criar um lembrete a partir de uma frase
   inequívoca executa. Apagar qualquer coisa, ou agir sobre um match de baixa confiança, pergunta antes —
   com botões inline no Telegram. *(A3)*
4. **Provedores são portas.** Gemini e OpenAI diferem no transporte, não no que o módulo pede deles.
   Trocar de provedor é mudança de configuração, sem reiniciar nada. No Tars isso já é keyed DI:
   `IAiChatCompletionClientFactory.GetClient(provider)`. *(A4)*
5. **Tempo é dado, nunca adivinhado.** O modelo recebe a hora local atual do usuário, o fuso e o início da
   semana no system prompt, e devolve timestamps absolutos ISO. "Amanhã às 9" é resolvido antes de chegar
   num comando. *(A5)*
6. **Tudo é logado.** Toda invocação guarda o enunciado, a tool call resolvida e o desfecho. Isso é ao
   mesmo tempo a trilha de auditoria e a única forma realista de depurar um componente probabilístico.
   *(A6)*

---

## 4. Arquitetura

```
 Telegram áudio/texto ──► entrada do Channels ──► inbound.message.telegram
                          (long polling ou webhook)             │
                                                                │
 Barra de comando / áudio web ──► POST /assistant/interpret ─────┤
                                                                ▼
                                                       ┌──────────────────┐
                                                       │  Assistant       │
                                                       │  1. transcreve   │  ITranscriptionClient (se áudio)
                                                       │  2. monta prompt │  hora, fuso, locale, catálogo
                                                       │  3. chat + tools │  IChatCompletionClient
                                                       │  4. valida       │  JSON schema + validador do módulo
                                                       │  5. confirma?    │  política por comando
                                                       │  6. executa      │  IAssistantCommandHandler
                                                       │  7. responde     │  NotifyUserRequested
                                                       └──────────────────┘
                                                                │
                                       handlers de comando de Agenda / Notes / Finances
```

O módulo **nunca sabe que Telegram existe**. Ele consome `InboundMessageReceived(userId, channel,
text?, mediaRef?, mediaMimeType?)` — já normalizado pelo
[Channels](../../channels/pt-BR/inbound-and-linking.md) — e busca os bytes de mídia pela
porta `IInboundMediaReader`. Trocar por WhatsApp, ou entrar pela web, não toca em nada aqui.

**A entrada não roda o pipeline inline.** Transcrever e chamar o provedor leva de segundos a dezenas de
segundos, tempo demais para segurar quem chamou por HTTP; e uma execução que morre no meio não pode
virar mensagem perdida. Então o subscriber faz só a parte barata: grava uma linha para a invocação e
retorna. Um job em background deste módulo pega a linha e roda os sete passos, **um de cada vez**, que
é onde a garantia do `prefetch=1` do antigo plano de broker passa a morar.

Esse é o padrão de job no módulo dono que o resto do Pandora já usa — mesmo formato do dispatcher do
Channels e da varredura da Agenda. E ganha a durabilidade de brinde: uma execução que morre no meio é
uma linha com estado, não uma mensagem perdida. Ver o
[doc de mensageria §3](../../../architecture/pt-BR/messaging.md#3-assincronia-sem-broker).

### 4.1 Catálogo de comandos

Um módulo contribui descritores a partir do seu projeto `Abstractions`:

```csharp
public sealed record AssistantCommandDescriptor(
    string Name,                     // "create_reminder"
    string Description,              // mostrado ao modelo
    string ParametersJsonSchema,     // o schema da tool
    ConfirmationPolicy Confirmation, // Never | WhenAmbiguous | Always
    IReadOnlyList<string> Examples);

public interface IAssistantTool
{
    AssistantCommandDescriptor Descriptor { get; }
    Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement args, CancellationToken ct);
    string Describe(AssistantToolContext context, JsonElement args); // a pergunta de confirmação
}

// Para quem a tool roda e como falar com ele: toda frase no Locale, todo instante no TimeZone.
public sealed record AssistantToolContext(Guid UserId, string Locale, TimeZoneInfo TimeZone);
```

O Assistant coleta todo descritor registrado na inicialização e renderiza como as definições de tool do
provedor. Os handlers são finos: mapeiam argumentos no comando de aplicação já existente do módulo e
mandam pelo mediator. Nenhuma regra de negócio é duplicada.

**Catálogo inicial da Agenda** (fase A4, casando com a fase 7 da Agenda):
`create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `list_agenda`
(hoje/amanhã/esta semana), `search_items`.

Depois: `create_note` e `search_notes` (Notes), `record_transaction` e `balance_summary` (Finances).

### 4.2 Catálogo de schema

**`ast001_assistant_profile`** — configuração por usuário.

| Coluna | Observações |
|---|---|
| `user_id` | Único. |
| `chat_provider`, `chat_model` | ex.: `gemini` + um modelo Gemini rápido. |
| `transcription_provider`, `transcription_model` | Reservado (A4). Pode diferir do chat. |
| `credential_ref` | Aponta para a linha `int001_external_account` (`auth_kind = api-key`) com a chave. Obrigatório para provedor hospedado. |
| `endpoint` | Base URL para provedores self-hosted. Reservado/nulo enquanto só há Gemini. |
| `is_enabled`, `locale_override` | |
| `confirmation_level` | `strict` \| `balanced` \| `trusting` — desloca a política de todo comando um degrau. |

**`ast002_conversation`** — `user_id`, `source` (`telegram` \| `web`), `started_at`, `last_message_at`,
`is_active`. Uma conversa expira após 30 minutos de silêncio, para que "cancela isso" não alcance ontem.

**`ast003_message`** — `conversation_id`, `role` (`user` \| `assistant` \| `tool`), `content`,
`audio_ref` (anulável), `token_count`, `created_at`.

**`ast004_command_invocation`** — a trilha de auditoria.

| Coluna | Observações |
|---|---|
| `conversation_id`, `message_id` | |
| `command_name`, `arguments` (jsonb) | Exatamente o que o modelo pediu. |
| `status` | `pending-confirmation` \| `executed` \| `rejected` \| `failed` \| `expired` |
| `result` (jsonb), `error` | |
| `provider`, `model`, `latency_ms`, `tokens_in`, `tokens_out` | Acompanhamento de custo e qualidade. |

### 4.3 Confirmação

`ConfirmationPolicy` no descritor, ajustada pelo `confirmation_level` do perfil:

| Política | Comportamento |
|---|---|
| `Never` | Executa e reporta. Comandos de leitura e criação de itens trivialmente reversíveis. |
| `WhenAmbiguous` | Executa a menos que a confiança do modelo seja baixa ou um argumento obrigatório tenha sido inferido em vez de dito. Senão, ecoa a intenção interpretada com botões **Confirmar / Cancelar**. |
| `Always` | Nunca executa sem um toque de botão. Exclusões, operações em lote, qualquer coisa financeira. |

Uma confirmação pendente é uma linha `ast004` em `pending-confirmation`, expirando em 10 minutos. A
resposta é o `Describe` da própria tool ("Criar o lembrete \"Pagar o aluguel\" para 05/09/2026 às 10:00?"),
nunca JSON cru. No Telegram os botões vão na própria resposta — `SendAssistantReply.Buttons`, com
`owner_module: "assistant"` e o id da invocação como payload — e o Channels os registra no
`chn003_interaction` exatamente como os de uma notificação. O toque volta como `InboundInteractionReceived`
para o `AssistantInteractionReceivedHandler`, que roda os mesmos comandos de confirmar/cancelar da web e
responde com o resultado — o mesmo mecanismo que a Agenda usa, sem nenhum código compartilhado entre os dois.

**Várias chamadas por frase.** O modelo pode devolver mais de uma tool call ("lembra X e cria Y"). Cada
uma roda (ou fica pendente) sozinha e ganha sua linha `ast004`; a resposta as numera, e os botões são
numerados para casar. O custo do provedor é uma chamada, registrado na primeira linha.

**Idioma.** As respostas — mensagens das tools, as do próprio pipeline ("Não entendi…"), a prosa do
modelo, os rótulos dos botões — seguem o locale do perfil (`locale_override`, padrão `pt-BR`), via
`AssistantToolContext`.

A expiração e o uso único do botão são garantidos pelo `chn003_interaction`; a expiração da
*invocação* (`ast004`) continua sendo deste módulo, porque é ela que decide se ainda faz sentido
executar.

### 4.4 Voz

**Construído (Telegram).** Áudios do Telegram são OGG/Opus, mas o módulo não precisa saber disso. O fluxo:

1. O Channels publica `InboundMessageReceived` com `mediaRef` e `mediaMimeType`.
2. O subscriber, vendo que não há texto e o MIME é `audio/*`, lê os bytes por
   `IInboundMediaReader.OpenAsync(channel, bot, mediaRef)` — a única porta que ele chama no Channels. O
   `bot` importa: um `file_id` do Telegram só pode ser baixado pelo bot que o recebeu. Áudios acima de
   5 MB são recusados com uma resposta.
3. O pipeline de interpretação faz uma **chamada de transcrição** ao mesmo provedor de chat: o áudio vai
   como `ChatAttachment` no turno do usuário (Tars `Ai.Chat`, mapeado para `inlineData` do Gemini), sem
   tools, temperatura 0. Não foi preciso um `ITranscriptionClient` separado.
4. A transcrição vira o enunciado e segue o pipeline de texto normal. Tokens e latência das duas
   chamadas somam na invocação; transcrição vazia registra um pedido de esclarecimento com o enunciado
   `[voice note]`.
5. A resposta ecoa o que foi ouvido (`🎤 "…"`) acima do resultado.

Os bytes de áudio são descartados depois da transcrição. Retenção continua como opt-in futuro por
perfil, porque voz é a coisa mais sensível que este módulo toca.

A superfície web (ainda não construída) grava com `MediaRecorder`, faz upload para
`POST /assistant/interpret` como multipart, e segue o mesmo caminho — o `InterpretInput.Audio` já aceita.

### 4.5 Prompting

Um único system prompt, versionado no código, carregando: a hora local atual do usuário e o fuso IANA, o
locale, o início da semana, os nomes dos calendários e listas de tarefas (para que "calendário do
trabalho" resolva), e a regra de que todo timestamp deve voltar absoluto e ISO-8601. Os exemplos few-shot
vêm do campo `Examples` dos descritores, no idioma do usuário — o módulo precisa funcionar em português
primeiro, porque é assim que vão falar com ele.

O histórico da conversa é limitado às últimas N mensagens da conversa ativa, para não pagar tokens
raciocinando sobre uma transcrição infinita.

---

## 5. Comportamento em falha

Um componente probabilístico falha diferente do resto do Pandora, então os modos de falha são parte do
desenho, não um detalhe posterior:

| Situação | Resposta |
|---|---|
| Nenhuma tool casou | Responde o que entendeu e lista o que sabe fazer. Nunca inventa uma ação. |
| Argumento obrigatório faltando | Faz uma pergunta específica, mantendo a chamada parcial na conversa. |
| Validação rejeitou os argumentos | Reporta o erro de domínio em linguagem clara; loga a chamada crua. |
| Provedor inacessível / timeout | Diz isso com todas as letras e preserva o enunciado para retry. Nunca engole em silêncio o lembrete do usuário. |
| Modelo devolveu JSON malformado | Um reprompt, e então desiste com uma mensagem honesta. |

A única coisa que ele nunca pode fazer é alegar um sucesso que não houve — o status da invocação é escrito
a partir do resultado do comando, não da narração do modelo.

---

## 6. Building block no Tars: `Ai`

Namespacing **por capacidade**, não por provedor — assim transcrição e embeddings entram como
`Ai.Transcription.*` / `Ai.Embedding.*` sem reorganizar o que já existe.

| Projeto | Conteúdo | Estado |
|---|---|---|
| `Pottmayer.Tars.Ai.Abstractions` | `AiException` com `IsPermanent` (permanente vs. transiente), compartilhado por todas as capacidades. | **Feito** (uncommitted) |
| `Pottmayer.Tars.Ai.Chat.Abstractions` | `IAiChatCompletionClient`, `IAiChatCompletionClientFactory`, e os models `ChatRequest`/`ChatCompletion`/`ChatMessage`/`ToolDefinition`/`ToolCall`/`TokenUsage`. Modelo e chave (`ApiKey`) vêm **por chamada**. | **Feito** (uncommitted) |
| `Pottmayer.Tars.Ai.Chat` | `KeyedAiChatCompletionClientFactory` + `AddTarsAiClientFactory`. | **Feito** (uncommitted) |
| `Pottmayer.Tars.Ai.Chat.Gemini` | `GeminiAiChatCompletionClient` sobre `v1beta/models/{model}:generateContent`, chave no header `x-goog-api-key`; options `Tars:Ai:Chat:Gemini`. | **Feito** (uncommitted, 17 testes verdes; falta 1ª chamada real) |
| `Pottmayer.Tars.Ai.Chat.OpenAi` | Chat Completions com tools. | Futuro |
| `Pottmayer.Tars.Ai.Transcription.*` | Entrada de áudio (Gemini) / Whisper. | Futuro (A4) |

A seleção de provedor é por chamada, não por aplicação: os clients são resolvidos por keyed DI
(`GetClient(provider)`) usando o perfil do usuário, porque dois usuários da mesma instância podem
escolher diferente.

O Tars fica com o transporte e a forma; o Pandora fica com prompts, catálogos, políticas e persistência. A
documentação vai no repositório do Tars, em `docs/ai/`.

---

## 7. Superfície de API

```
GET    /assistant/profile                POST /assistant/profile          → configuração de provedor/modelo
GET    /assistant/providers              → provedores disponíveis, modelos, teste de alcance
POST   /assistant/interpret              → { text } ou áudio multipart → intenção interpretada + resultado
POST   /assistant/invocations/{id}/confirm
POST   /assistant/invocations/{id}/cancel
GET    /assistant/conversations          GET /assistant/conversations/{id}/messages
GET    /assistant/invocations            → a trilha de auditoria, filtrável por status
GET    /assistant/commands               → o catálogo vivo (debug e painel de ajuda na web)
```

---

## 8. Roadmap

### Fase A1 — `Ai` no Tars + perfil
- `Ai.Chat` com chat + tools + provedor Gemini: **já implementado** (uncommitted); falta a 1ª chamada
  real e o commit. Ver [execution-plan.md](execution-plan.md), Etapa 0.
- Casca do módulo Assistant; `ast001`; registro do Gemini no Host; UI de configurações + teste de
  alcance (usando a chave do usuário no Integrations).
- **Pronto quando:** uma tela de configurações consegue fazer um prompt ida-e-volta no Gemini e mostrar
  a resposta.

### Fase A2 — Pipeline de comandos (web, texto)
- Registro e descoberta de descritores; system prompt; validação da tool call; execução pelo mediator;
  `ast002`–`ast004`.
- A Agenda registra `create_reminder` e `create_task`; a barra de comando web chama `/interpret`.
- Fluxo de confirmação na UI web.
- **Pronto quando:** digitar "lembrete de pagar o aluguel dia 5 às 10h" no navegador cria o lembrete
  certo, e o log de invocação mostra a tool call exata.

### Fase A3 — Entrada por chat, texto *(depende de [Channels C4 — entrada](../../channels/pt-BR/inbound-and-linking.md), já implementado)*
- Subscriber ligado a `inbound.message.#`, gravando uma linha de invocação drenada uma de cada vez; responder por
  `NotifyUserRequested`.
- Confirmação com botões `owner_module: "assistant"`; subscriber ligado a
  `inbound.interaction.assistant.#`.
- **Pronto quando:** a mesma frase digitada no Telegram faz a mesma coisa, e *Confirmar* funciona.

### Fase A4 — Voz *(Telegram feito em 2026-09-22)*
- ✅ Áudio no Telegram: mídia lida por `IInboundMediaReader`, transcrição pela entrada de áudio do
  Gemini (`ChatAttachment`, Tars 0.0.16), transcrição ecoada na resposta. Ver §4.4.
- Pendente: upload por `MediaRecorder` na web; opt-in de retenção de áudio.
- **Pronto quando:** um áudio no Telegram cria um lembrete, em português.

### Fase A5 — Qualidade e segundo provedor
- ✅ Escritas da Agenda (2026-09-27): `create_task`, `create_event`, `complete_task`, `snooze_reminder`.
  As duas últimas recebem as palavras do usuário e acham o item localmente pelo título (sem acento/caixa,
  prefixo de palavra), então nenhum título sai de casa; vários resultados → a resposta lista e pergunta
  qual. Lembrete recorrente só se adia pelo botão da notificação.
- Pendente: `list_agenda` — e, junto com as leituras, a decisão sobre dado pessoal saindo de casa (ver §9.2).
- OpenAI como segundo provedor (`Ai.Chat.OpenAi` + registro), se houver motivo real além do Gemini.
- Um conjunto de avaliação com enunciados reais, para que trocar de modelo seja uma decisão medida e
  não um chute.
- **Pronto quando:** o conjunto de avaliação passa no modelo escolhido, com os números registrados.

### Fase A6 — Além da Agenda *(futuro)*
- ✅ `record_expense` (2026-09-28): "gastei 45 no mercado no Nubank" → transação pendente `manual` na
  caixa de entrada do Finances (nunca lançada direto). Cartão/conta achado localmente pelas palavras do
  usuário; nada citado e uma só conta/cartão → ela; senão a resposta pergunta qual.

Notes (`create_note`, `search_notes`), Finances (`balance_summary`), resumos
proativos ("este é o seu dia" toda manhã às 07:00, gerado em vez de templatizado), e recuperação sobre as
Notes para responder perguntas.

---

## 9. Questões em aberto

1. ~~**Onde ficam as chaves de API hospedadas.**~~ **Decidido:** no Integrations, em
   `int001_external_account` com `auth_kind = api-key`. Um cofre cifrado só. O `credential_ref` da
   `ast001` aponta para lá, e a chave é obtida por `IExternalCredentialProvider` — a mesma porta
   síncrona que a Agenda usa para o token do Google. Ver
   [Integrations — OAuth e Credenciais](../../integrations/pt-BR/oauth-and-credentials.md).
2. **Dado pessoal sai de casa.** Como o provedor é hospedado (Gemini), **todo enunciado sai de casa**, e
   leituras como `list_agenda` mandariam títulos de eventos para o Google. Sem Ollama, isso não é mais
   "resolver antes da A5" — vale **agora**. Mínimo para o 1º release: aviso por perfil, e começar com
   catálogo só de *escrita* (`create_reminder`); leituras entram junto com uma decisão explícita. Ver o
   [execution-plan](execution-plan.md#questão-que-subiu-de-prioridade-dado-pessoal-sai-de-casa).
3. **Streaming.** Desnecessário para executar comandos; útil se um modo conversacional for adicionado. A
   abstração não expõe hoje; adiado.
