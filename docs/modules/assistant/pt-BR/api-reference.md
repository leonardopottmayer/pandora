# Referência da API

[← Voltar ao índice](README.md)

Caminho base: **`/api/v{version}/assistant`** (o cliente web usa `v1.0`). Todo endpoint é autenticado e
restrito ao usuário do token; uma invocação de outro usuário devolve **404**
(`Assistant.InvocationNotFound`). Um controller: `Presentation/Controllers/AssistantController.cs`.

---

## Perfil e provedores

| Método | Rota | Para quê |
|---|---|---|
| GET | `/profile` | O perfil do usuário, ou os padrões quando nenhum foi salvo |
| POST | `/profile` | Cria ou substitui o perfil |
| GET | `/providers` | Provedores suportados e quantas API keys o usuário tem em cada um |
| POST | `/providers/{provider}/test` | Sonda de alcance: um prompt mínimo com a chave escolhida (chamada real, cobrada) |

Corpo do `POST /profile`: `{ provider, chatAccountId?, model, isEnabled, localeOverride?, confirmationLevel }`.
Erros: `Assistant.ModelRequired`, `Assistant.UnknownConfirmationLevel`, `Assistant.ChatAccountNotFound`,
`Assistant.ChatAccountMismatch` (não é uma conta `api-key` daquele provedor). Provedor vazio significa
`gemini`.

`GET /providers` → `[{ provider, displayName, keyCount }]` — só `gemini` hoje. Sem ler segredo, sem
chamada externa.

Corpo do `POST /providers/{provider}/test`: `{ accountId?, model? }` (modelo padrão quando omitido) →
`{ ok, latencyMs, reply?, error?, errorKind? }`. Sempre devolve 200; `errorKind` é `no_key`, `rejected`
(`AiException` permanente: chave ou modelo inválido) ou `unreachable` (transitório).

## Interpretar

| Método | Rota | Para quê |
|---|---|---|
| POST | `/interpret` | Interpreta uma frase digitada e executa inline |
| POST | `/interpret/audio` | O mesmo, para uma nota de voz (`multipart/form-data`) |

Corpo do `POST /interpret`: `{ text, conversationId? }`. Formulário do `POST /interpret/audio`: `audio`
(arquivo) + `conversationId` opcional; o limite da requisição é 6 MB, o pipeline recusa áudio acima de
5 MB com uma clarificação legível. Os dois fazem chamadas reais e cobradas ao provedor.

Resposta (`InterpretResultDto`):

```json
{
  "conversationId": "…",
  "message": "a resposta inteira (numerada quando há várias chamadas)",
  "invocations": [
    { "invocationId": "…", "status": "pending-confirmation", "commandName": "delete_task",
      "arguments": "{…}", "message": "Excluir a tarefa \"…\"?" }
  ],
  "transcript": "o que foi ouvido, para áudio; null para texto"
}
```

Erros (antes de registrar qualquer coisa): `Assistant.EmptyText`, `Assistant.NotEnabled`,
`Assistant.NoApiKey`. Qualquer outro resultado é um 200 com um status de invocação — ver
[Modelo de dados](data-model.md#ast004_command_invocation).

## Invocações

| Método | Rota | Para quê |
|---|---|---|
| POST | `/invocations/{id}/confirm` | Executa uma chamada retida com os argumentos guardados |
| POST | `/invocations/{id}/cancel` | Recusa uma chamada retida |
| GET | `/invocations?limit=` | A trilha de auditoria, mais recentes primeiro (padrão 50, máx. 100) |

Confirmar / cancelar devolvem `{ invocationId, status, commandName, arguments, message }`. Erros:
`Assistant.InvocationNotFound`, `Assistant.NotPendingConfirmation`, `Assistant.ConfirmationExpired` (a
linha é marcada `expired`). Uma chamada confirmada cuja tool falha ainda devolve 200 com status `failed`.

`GET /invocations` → `[{ id, conversationId, utterance, commandName, arguments, status, result, error,
provider, model, latencyMs, promptTokens, completionTokens, expiresAt, createdAt }]`.

## Fora do HTTP

O Telegram chega ao módulo por integration events, não por este controller: `InboundMessageReceived`
(mensagens no bot `assistant`) e `InboundInteractionReceived` (toques em botão com dono `assistant`),
ambos do Channels; as respostas saem como `SendAssistantReply` do Channels. Ver
[Arquitetura §7](architecture.md#7-caminho-do-telegram).
