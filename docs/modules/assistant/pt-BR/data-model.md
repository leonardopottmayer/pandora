# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Referência da API](api-reference.md)

Schema PostgreSQL **`assistant`**. Convenções: PK `uuid DEFAULT uuid_generate_v7()` (os aggregates
também geram ids v7 no código), timestamps `TIMESTAMPTZ`, constraints nomeadas (`pk_astXXX`,
`fk_astXXX_*`, `chk_astXXX_*`, `uq_`/`ix_astXXX_*`), enums como `VARCHAR` + `CHECK` com valores
hifenizados. Toda linha de usuário tem `user_id`; não há FK para outros schemas.

As migrations ficam em `migrations/migrations/assistant/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| ast001 | `assistant_profile` | Uma configuração do assistant por usuário |
| ast002 | `conversation` | Conversas curtas (expiram após 30 min parada), com a última lista numerada |
| ast003 | `message` | Turnos de uma conversa: a frase e o recap da resposta |
| ast004 | `command_invocation` | A trilha de auditoria: uma linha por tool call (ou pela falta dela) |
| — | `tars_outbox_message` | Outbox transacional do Tars para as respostas enviadas ao Channels |

---

## ast001_assistant_profile

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | único (`uq_ast001_user`) |
| `chat_provider` | varchar(40) NOT NULL | chave do cliente de IA e provedor no Integrations (`gemini`) |
| `chat_account_id` | uuid NULL | a `integrations.int001_external_account` (API key) que o chat gasta; sem FK. Nulo até ser escolhida |
| `chat_model` | varchar(100) NOT NULL | ex.: `gemini-3.6-flash` |
| `is_enabled` | boolean NOT NULL DEFAULT false | |
| `locale_override` | varchar(20) NULL | o idioma do assistant; nulo → `pt-BR` |
| `confirmation_level` | varchar(20) NOT NULL | `strict\|balanced\|trusting` (`chk_ast001_confirmation_level`) |
| `created_by` / `created_at` / `updated_by` / `updated_at` | | colunas de auditoria (interceptor) |

Nenhum segredo fica aqui: a chave é lida do Integrations a cada chamada. Antes do primeiro save a API
devolve os padrões (`gemini`, `gemini-3.6-flash`, desligado, `balanced`) sem gravar linha.

## ast002_conversation

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `started_at` | timestamptz NOT NULL | |
| `last_activity_at` | timestamptz NOT NULL | atualizado a cada turno; parada > 30 min → a próxima frase abre outra (no código) |
| `last_listing` | jsonb NULL | a última lista numerada: `[{ kind, id, label, at? }]`, substituída a cada nova lista |

Índice `ix_ast002_user_activity (user_id, last_activity_at DESC)` — a conversa mais recente do usuário é
buscada a cada frase. Não há coluna `source`: a mesma conversa serve web e Telegram.

## ast003_message

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `conversation_id` | uuid NOT NULL | FK → `ast002` `ON DELETE CASCADE` |
| `author` | varchar(20) NOT NULL | `user\|assistant` (`chk_ast003_author`) |
| `content` | text NOT NULL | user: a frase (ou transcrição); assistant: o **recap**, não a resposta mostrada |
| `created_at` | timestamptz NOT NULL | |

Índice `ix_ast003_conversation_created (conversation_id, created_at)`. Essas linhas são o histórico
reenviado ao modelo, então nunca carregam os dados de uma tool de leitura.

## ast004_command_invocation

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | o id enviado para confirmar/cancelar e levado pelos botões do Telegram |
| `user_id` | uuid NOT NULL | |
| `conversation_id` | uuid NOT NULL | FK → `ast002` `ON DELETE CASCADE` |
| `utterance` | text NOT NULL | o que o usuário disse; `[voice note]` quando o áudio não deu texto |
| `command_name` | varchar(100) NULL | a tool; nulo para clarificação ou erro de provedor |
| `arguments` | jsonb NULL | os argumentos da tool call, com `ref_id`/`ref_kind`/`ref_label`/`ref_at` quando um item foi fixado |
| `status` | varchar(20) NOT NULL | ver abaixo (`chk_ast004_status`) |
| `result` | text NULL | a mensagem ao usuário no sucesso, a pergunta de uma chamada retida, ou a clarificação |
| `error` | text NULL | por que falhou ou foi rejeitada |
| `provider` / `model` | varchar NOT NULL | do perfil no momento |
| `latency_ms` | bigint NOT NULL | |
| `prompt_tokens` / `completion_tokens` | int NOT NULL DEFAULT 0 | transcrição inclusa; só na primeira linha de um turno com várias chamadas |
| `expires_at` | timestamptz NULL | preenchido só enquanto `pending-confirmation` (criação + 10 min) |
| `created_at` | timestamptz NOT NULL | |

Índice `ix_ast004_user_created (user_id, created_at DESC)` — a aba Histórico.

**Status** (`InvocationStatus`):

| Valor | Significado |
|---|---|
| `executed` | A tool rodou e o caso de uso deu certo. |
| `failed` | A tool rodou (ou procurou o item) e o caso de uso recusou. |
| `clarification` | O modelo respondeu em prosa; nada rodou. |
| `rejected` | Tool desconhecida, um `ref` fora da última lista, ou argumentos ilegíveis. |
| `provider-error` | O provedor falhou ou recusou a chamada. |
| `pending-confirmation` | Retida; espera confirmar/cancelar até `expires_at`. |
| `cancelled` | O usuário recusou uma chamada retida. |
| `expired` | Uma confirmação chegou depois de `expires_at`. |

Só `pending-confirmation` muda: para `executed`/`failed` (confirmar), `cancelled` ou `expired`.

## tars_outbox_message

A tabela padrão de outbox do Tars (`event_id` único, índice parcial nas pendentes), mapeada por
`modelBuilder.AddTarsOutbox(schema: "assistant")` no `AssistantDbContext`. Guarda os eventos
`SendAssistantReply` publicados pelos subscribers do Telegram, então uma resposta só sai se a unit of work
dela fez commit. Ver [Mensageria](../../../architecture/pt-BR/messaging.md).
