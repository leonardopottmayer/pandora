# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Visão geral](overview.md)

---

## 1. Estrutura dos projetos

O módulo segue a mesma divisão em camadas dos outros, em `backend/src/Modules/Assistant/`:

```
Pottmayer.Pandora.Modules.Assistant.
  Abstractions      → o contrato público que os outros módulos implementam: IAssistantTool, IAssistantTargetedTool,
                      AssistantCommandDescriptor, ConfirmationPolicy, ListedRefs, ToolArguments,
                      IAssistantFileQueue, SharedFile; AssistantModule, AssistantOptions
  Application       → Commands (Interpret, Confirm/CancelInvocation, SaveProfile, TestProvider), Queries
                      (GetProfile, GetProviders, GetInvocations), Interpret/ (system prompt, contexto,
                      parser de sim/não), Subscribers/ (inbound do Telegram + toques em botão), DI
  Contracts         → vazio — o módulo publica o SendAssistantReply do Channels, não eventos próprios
  Domain            → Aggregates (AssistantProfile, Conversation, Message, CommandInvocation),
                      ValueObjects (ConfirmationLevel, InvocationStatus, MessageAuthor), Errors, Ports
  Infrastructure    → só DI: faz o bind de AssistantOptions e registra o cliente de chat Gemini do Tars.Ai
  Persistence       → AssistantDbContext (+ mapeamento do outbox do Tars), EntityConfigs, Repositories, DI
  Presentation      → AssistantController, DI
```

Wiring no Host (`Program.cs`): `AddAssistantPersistence()`, `AddAssistantInfrastructure()`,
`AddAssistantApplication()`, `AddAssistantPresentationPart()`; o `OutboxRegistration.cs` adiciona
`AddTarsOutboxRelay(AssistantModule.DatabaseKey)`. Ver [Wiring do Tars](../../../architecture/pt-BR/tars-wiring.md).

**Provedor.** Gemini via `Pottmayer.Tars.Ai.Chat.Gemini`, resolvido por chamada com
`IAiChatCompletionClientFactory.GetClient(profile.ChatProvider)`. Nenhuma chave é configurada no Host:
a chave é lida por chamada do Integrations (`IExternalCredentialProvider.GetApiKeyAsync(userId,
accountId)`) e passada em `ChatRequest.ApiKey`. O modelo também é por chamada (`ChatRequest.Model`),
padrão `gemini-3.6-flash` (`AssistantDefaults`). Temperatura 0 em tudo.

## 2. O pipeline de interpretação

O `InterpretCommandHandler` é o núcleo; os endpoints web e o subscriber do Telegram mandam o mesmo
`InterpretCommand`.

1. **Pré-checagem.** Entrada vazia → `Assistant.EmptyText`. Sem perfil / desligado →
   `Assistant.NotEnabled`. Sem conta de chat, ou chave ilegível → `Assistant.NoApiKey`. São as únicas
   falhas devolvidas como erro; tudo depois disso registra uma invocação.
2. **Conversa.** O `conversationId` informado, senão a mais recente do usuário; uma nova quando não
   existe, é de outro usuário, ou está parada há mais de 30 minutos (`Conversation.IdleTimeout`). Sem
   id de conversa → a mesma conversa serve web e Telegram.
3. **Contexto.** Idioma (`locale_override`, padrão `pt-BR`), fuso efetivo (`IEffectiveTimeZoneResolver`,
   que cai no padrão da conta) e início da semana (`IUserPreferencesReader`) → um `AssistantToolContext`.
4. **Voz.** Um áudio (máx. 5 MB) é transcrito antes, numa chamada própria ao mesmo modelo — o áudio vai
   como `ChatAttachment` no turno do usuário, sem tools. A transcrição vira a frase; uma vazia registra
   uma clarificação sob `[voice note]`. O áudio não é guardado.
5. **Sim/não digitado.** Numa conversa existente, uma resposta feita só de palavras de sim/não
   (`ConfirmationReply`: "sim", "pode", "não, deixa", até 4 palavras) resolve todas as chamadas retidas
   ali pelos mesmos comandos Confirmar/Cancelar — o modelo não é consultado.
6. **Prompt.** `AssistantSystemPrompt` (versionado, `Version = 6`): papel, agora local + dia da semana,
   fuso IANA, início da semana, idioma, a regra de ISO-8601 absoluto, "faça uma pergunta em vez de
   inventar", como passar `ref`, e cada descriptor com seus exemplos. As tools vão como `ToolDefinition`s.
7. **Histórico.** Até `Pandora:Assistant:HistoryMessageLimit` (padrão 10) mensagens recentes de `ast003`
   da conversa ativa, entre o system prompt e a frase. Se o modelo responde imitando uma linha de recap
   (`[tool: …]`), a chamada é refeita uma vez sem o histórico.
8. **Resultado por tool call.** Sem tool call → `clarification` com a prosa do modelo (ou "Não entendi").
   Senão, cada chamada, em ordem, passa por `RunAsync`:
   - tool desconhecida → `rejected`; um número `ref` fora da última lista → `rejected`;
   - `ListedRefs.Pin` grava o item listado nos argumentos;
   - se exige confirmação (§4): uma `IAssistantTargetedTool` acha o item antes (nenhum / vários →
     `failed` na hora; um → fixado com `PinTarget`), e a chamada é retida como `pending-confirmation`
     com o `Describe` da tool como pergunta, expirando em 10 minutos;
   - senão `ExecuteAsync` → `executed` ou `failed` com a mensagem da tool;
   - `ArgumentException` / `FormatException` / `InvalidOperationException` → `rejected`.
   Uma falha do provedor (`AiException`) em qualquer chamada → `provider-error`.
9. **Registro.** Uma unit of work: atualiza/insere a conversa (e o `last_listing` quando um passo
   devolveu lista), a mensagem do usuário, a do assistant (o **recap**, §5), uma linha `ast004` por passo.
   Latência e tokens (transcrição inclusa) vão só na primeira linha. Vários passos → a resposta é numerada.

O resultado (`InterpretResultDto`) traz o id da conversa, a resposta combinada, uma entrada por invocação
(id, status, nome da tool, argumentos, mensagem) e a transcrição.

## 3. O catálogo de tools

Um módulo contribui tools registrando implementações de `IAssistantTool` no seu `ApplicationDI`
(`services.AddScoped<IAssistantTool, …>()`); o Assistant recebe `IEnumerable<IAssistantTool>`. Uma tool
é fina: lê os argumentos com `ToolArguments`, acha o item localmente (`MatchByTitle` / `PickByTitle` /
`PickTarget`: sem diferenciar acento e caixa, por prefixo de palavra, ignorando palavras com menos de
três letras) e manda o comando ou query existente do módulo pelo mediator. Suas `Permissions` listam as
permissões que esses casos de uso exigem: o `Interpret` oferece ao modelo só as tools cujas permissões
o usuário tem (e cujas features estão ligadas), e os casos de uso ainda as verificam quando a tool roda.

| Módulo | Tool | Política | Notas |
|---|---|---|---|
| Agenda | `create_reminder`, `create_task`, `create_event` | WhenAmbiguous | |
| Agenda | `complete_task`, `reopen_task`, `update_task` | WhenAmbiguous | targeted |
| Agenda | `update_event` | WhenAmbiguous | targeted; uma ocorrência, salvo "e as seguintes" / "todas" |
| Agenda | `reschedule_reminder`, `rename_reminder` | WhenAmbiguous | targeted |
| Agenda | `delete_task`, `delete_event`, `cancel_reminder` | **Required** | targeted |
| Agenda | `list_agenda`, `list_tasks`, `list_reminders` | Never | lista numerada |
| Notes | `create_note` | WhenAmbiguous | nova página de topo |
| Notes | `append_to_note` | WhenAmbiguous | targeted; texto no fim |
| Notes | `search_notes` | Never | lista numerada |
| Notes | `read_note` | Never | a nota inteira, ao usuário |
| Finances | `record_expense` | WhenAmbiguous | uma transação pendente `manual` no inbox, nunca postada |
| Finances | `list_transactions`, `summarize_transactions`, `account_balances`, `list_cards`, `list_inbox` | Never | só leitura, em tópicos (não numerada) |

Fonte: `backend/src/Modules/{Agenda,Notes,Finances}/…Application/Assistant/`. O Finances não muda nada
pelo chat além do `record_expense`.

**Filas de arquivos.** `IAssistantFileQueue` (nome, palavras-chave, `EnqueueAsync(SharedFile)`) é o outro
ponto de contribuição. O Finances registra a `FinancesFileQueue` (palavras como `comprovante`, `boleto`,
`financeiro`, `fatura`); um arquivo que bate cai sem dono em `fin017_attachment`, com a legenda como nota.

## 4. Confirmação

A `ConfirmationPolicy` (`Never`, `WhenAmbiguous`, `Always`, `Required`) é deslocada pelo
`ConfirmationLevel` do perfil, e a chamada é retida quando a política deslocada é `Always`, ou a declarada
é `Required`:

| Nível | Never → | WhenAmbiguous → | Always → | Retido na prática |
|---|---|---|---|---|
| `strict` | WhenAmbiguous | Always | Always | toda escrita; leituras rodam |
| `balanced` (padrão) | Never | WhenAmbiguous | Always | tools `Always` e `Required` |
| `trusting` | Never | Never | WhenAmbiguous | só tools `Required` |

`WhenAmbiguous` não tem detector de ambiguidade: quando não é deslocada para `Always`, roda. A ambiguidade
é tratada pelo modelo fazendo uma pergunta (uma `clarification`) e pelas tools targeted recusando um nome
que bate com zero ou vários itens. Nenhuma tool declara `Always` hoje, então com `balanced` só as
exclusões são retidas.

Uma chamada retida é resolvida por `POST …/invocations/{id}/confirm|cancel`, por um botão no Telegram ou
por um sim/não digitado. Confirmar roda de novo os argumentos **guardados** (item fixado incluso) e grava o
resultado na mesma linha; depois de `expires_at` a linha vira `expired` e a confirmação é recusada. Não há
varredura: uma linha sem resposta fica `pending-confirmation` até alguém tentar confirmá-la (e o sim/não
digitado só olha linhas ainda dentro da janela).

## 5. Privacidade: o que o modelo vê

- A **frase** (e a nota de voz) vão para o Gemini.
- A resposta de uma tool de leitura vai só ao usuário. `AssistantCommandOutcome.Recap` é o que o `ast003`
  guarda para o próximo turno; uma tool sem recap fica como `[command: status; reply shown to the user,
  content withheld from you]`. Só a prosa do próprio modelo (uma clarificação) fica como foi dita.
- Uma lista numerada acrescenta `[numbered for reference: 1=event, 2=task; …]` — tipos, nunca títulos.
- Nomes são casados localmente, então um título que o usuário não digitou nunca sai de casa.

## 6. Apontar por número

Uma tool de lista devolve `AssistantCommandOutcome.Listed` (`ListedItem`: tipo, id, rótulo, início da
ocorrência opcional). O pipeline guarda a última em `ast002.last_listing`. O modelo passa `"ref": 2`; o
`ListedRefs.Pin` descarta qualquer campo `ref_*` escrito pelo modelo e fixa `ref_id`, `ref_kind`,
`ref_label`, `ref_at`. As tools leem com `ToolArguments.OptionalRef` / `PickTarget`, que recusam um número
fora da lista ou de outro tipo. Como o item é fixado antes de a chamada ser retida, a confirmação age sobre
o que o usuário viu, não sobre o que for o número 2 depois.

## 7. Caminho do Telegram

O módulo nunca fala com a Bot API. Sequência completa: [Fluxo do Telegram](../../../architecture/pt-BR/telegram-assistant-flow.md).

- **`InboundMessageReceivedHandler`** (subscriber de integration event no `InboundMessageReceived` do
  Channels) só trata mensagens do bot `assistant`. Texto → `InterpretCommand`. Áudio → bytes por
  `IInboundMediaReader.OpenAsync(channel, bot, mediaRef)`, depois `InterpretCommand` com o anexo; a
  resposta começa com `🎤 "…"`. Qualquer outro arquivo → uma fila de arquivos pela legenda (só
  imagens/PDFs, ≤ 20 MB, fotos nomeadas `telegram-<timestamp>.jpg`); legenda sem correspondência → a
  resposta pergunta para onde vai. Falhas de pré-checagem (desligado, sem chave) só são logadas.
- Chamadas retidas ganham **botões Confirmar / Cancelar** (`NotificationButton` com dono `assistant`, ação
  `confirm`/`cancel`, payload = id da invocação), numerados quando a resposta tem várias linhas.
- **`AssistantInteractionReceivedHandler`** trata `InboundInteractionReceived` do dono `assistant` e manda
  os mesmos comandos Confirmar/Cancelar, respondendo com o resultado ou "expirou / já respondido".
- As respostas são publicadas como **`SendAssistantReply`** do Channels dentro de uma unit of work do
  Assistant, então vão pelo outbox transacional (`assistant.tars_outbox_message`) e o relay as entrega
  depois do commit. O Channels quebra respostas acima de 4096 caracteres. Ver [Mensageria](../../../architecture/pt-BR/messaging.md).

O console web roda o pipeline inline na requisição HTTP; o Telegram roda no subscriber.

## 8. Acoplamento com o Integrations

- O perfil guarda `chat_account_id`, um id de `int001_external_account`; o `SaveProfile` confere via
  `IExternalAccountReader` que é uma conta `api-key` do usuário no mesmo provedor.
- O `GetProviders` conta as API keys do usuário por provedor (sem ler segredo, sem chamada externa).
- O `AssistantAccountUsageSource` (`IExternalAccountUsageSource`) diz ao Integrations que a conta é usada
  por `assistant`/`chat`, para a tela de conexões e antes de desconectar.

Ver [Integrations — OAuth e credenciais](../../integrations/pt-BR/oauth-and-credentials.md) e
[Configuração do Google OAuth e do Gemini](../../../deployment/google-and-gemini-setup.md).

## 9. Frontend

`client-web/src/modules/assistant`:

- `pages/AssistantPage.tsx` (`/assistant`) — aba **Chat**: campo de texto, botão de voz, respostas com
  Confirmar/Cancelar e os detalhes da tool call (modelo, latência, tokens), "nova conversa". Aba
  **Histórico**: o log de invocações, cada linha podendo reabrir a sua conversa.
- `pages/AssistantSettingsPage.tsx` (`/settings/assistant`) — ligar, escolher a chave Gemini do
  Integrations, modelo, idioma, nível de confirmação; "testar" roda a sonda de alcance.
- `hooks/useVoiceRecorder.ts` — `MediaRecorder` até 150 s, recodificado no navegador para WAV mono
  16 kHz e enviado a `/interpret/audio` (exige contexto seguro).
- `services/assistant.service.ts`, `hooks/useAssistant.ts`, `hooks/queryKeys.ts`, `models.ts`.

## 10. Testes

- `backend/tests/Pottmayer.Pandora.Modules.Assistant.Tests` — pipeline, confirmação, perfil, teste do
  provedor, níveis de confirmação, os dois subscribers (fakes para o cliente de IA, repositórios, bus).
- Os testes das tools ficam nos módulos donos (`AgendaAssistantToolsTests`, `AgendaAssistantEditToolsTests`,
  `NoteAssistantToolsTests`); o Channels cobre o `SendAssistantReplyHandler`.
- `backend/tests/Pottmayer.Pandora.IntegrationTests/Live/AssistantLiveTests` — execução opt-in contra o
  Gemini real (`PANDORA_LIVE_GEMINI=1`, gasta tokens; relatório via `PANDORA_LIVE_REPORT`).
- `client-web/src/modules/assistant/pages/AssistantPage.test.tsx`.
