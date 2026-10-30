# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Referência da API](api-reference.md)

Schema PostgreSQL **`communications`**. Convenções: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ` para
timestamps, constraints nomeadas (`pk_comXXX`, `uq_comXXX_*`, `chk_comXXX_*`), enums como `VARCHAR` + `CHECK` com
valores hifenizados.

As migrations ficam em `migrations/migrations/communications/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| com001 | `mailbox` | Uma caixa conectada e o estado da sincronização |
| com002 | `mail_label` | Uma etiqueta do Gmail ou uma pasta IMAP |
| com003 | `mail_message` | O cabeçalho espelhado de uma mensagem |
| com004 | `mail_message_label` | Quais etiquetas a mensagem tem |
| com005 | `mail_content` | O corpo em cache de uma mensagem aberta |
| — | `tars_outbox_message` | O outbox do módulo (`MailMessageReceived`, o aviso de senha recusada) |

A `com006_mail_rule` chega com a M4.

---

## com001_mailbox

A credencial fica no Integrations: `external_account_id` a referencia só logicamente, porque nenhuma chave
estrangeira sai do schema.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | dono |
| `provider` | varchar(20) NOT NULL | `gmail` \| `titan` \| `imap` |
| `external_account_id` | uuid NOT NULL | a conta do Integrations |
| `address` | varchar(320) NOT NULL | |
| `display_name` | varchar(100) NULL | |
| `imap_host`, `imap_port`, `smtp_host`, `smtp_port` | NULL | só para `titan` (já preenchido) e `imap`; TLS ou STARTTLS decorre da porta |
| `status` | varchar(20) NOT NULL | `active` \| `syncing` \| `needs-reconnect` \| `error` |
| `sync_cursor` | text NULL | opaco; nulo até a carga inicial começar |
| `backfill_cursor` | text NULL | opaco; nulo quando não há mais o que ler |
| `backfill_completed_at` | timestamptz NULL | |
| `message_total_estimate` | int NULL | a contagem do provedor no início da carga, para a barra de progresso |
| `last_synced_at` | timestamptz NULL | |
| `last_error` | text NULL | |
| `consecutive_failures` | int NOT NULL DEFAULT 0 | `error` em 5 |
| `created_by`, `created_at`, `updated_by`, `updated_at` | | auditoria |

**Constraints:** `uq_com001_user_external_account (user_id, external_account_id)`: uma caixa por credencial.
**Índices:** `user_id`; `external_account_id` (credencial revogada → as caixas dela).

## com002_mail_label

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `mailbox_id` | uuid FK → com001, cascade | |
| `provider_label_id` | text NOT NULL | o id da etiqueta no Gmail, ou o caminho completo da pasta |
| `name` | text NOT NULL | |
| `kind` | varchar(20) NOT NULL | `system` \| `user` |
| `role` | varchar(20) NULL | `inbox` \| `sent` \| `drafts` \| `trash` \| `spam` \| `archive` |
| `is_exclusive` | boolean NOT NULL | falso no Gmail, verdadeiro no IMAP |

**Constraints:** `uq_com002_mailbox_provider_label (mailbox_id, provider_label_id)`.

## com003_mail_message

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `mailbox_id` | uuid FK → com001, cascade | |
| `provider_message_id` | text NOT NULL | o id do Gmail; no IMAP `{uidvalidity}:{uid}:{caminho da pasta}` |
| `thread_key` | text NOT NULL | `threadId`, ou o primeiro Message-ID de `References` |
| `rfc822_message_id` | text NULL | sem os sinais de maior e menor |
| `from_name`, `from_address` | NULL | |
| `to_recipients`, `cc_recipients` | jsonb NOT NULL DEFAULT `[]` | `[{ "Name", "Address" }]` |
| `subject` | text NOT NULL | |
| `snippet` | text NOT NULL | no IMAP, vazio até a mensagem ser aberta |
| `sent_at` | timestamptz NOT NULL | sempre em UTC |
| `is_unread`, `is_starred`, `has_attachments` | boolean NOT NULL | no Gmail, `has_attachments` significa `multipart/mixed` no nível de cima |
| `size_bytes` | bigint NOT NULL DEFAULT 0 | |

**Constraints:** `uq_com003_mailbox_provider_message (mailbox_id, provider_message_id)`.

**Índices:** `(mailbox_id, sent_at DESC, id DESC)` para a lista; `(mailbox_id, rfc822_message_id)` para "já temos
essa?"; `(mailbox_id, thread_key)` para uma conversa.

## com004_mail_message_label

`message_id` (FK → com003, cascade) + `label_id` (FK → com002, cascade), ambos a PK. Índice
`(label_id, message_id)`. No IMAP a mensagem tem exatamente uma linha aqui: a pasta dela.

## com005_mail_content

| Coluna | Tipo | Notas |
|---|---|---|
| `message_id` | uuid PK, FK → com003, cascade | |
| `text_body`, `html_body` | text NULL | HTML como recebido; o cliente limpa para mostrar |
| `attachments` | jsonb NOT NULL DEFAULT `[]` | `[{ "Id", "FileName", "ContentType", "SizeBytes", "ContentId" }]`; a lista, não os bytes |
| `fetched_at`, `last_accessed_at` | timestamptz NOT NULL | |

**Índices:** `last_accessed_at`, para a retenção.

### Cache de conteúdo

Linhas não abertas por `ContentCacheRetentionDays` (padrão 30) são apagadas pelo job e buscadas de novo na próxima
abertura. Não há teto de tamanho. Medido em 2026-10-30, com uma caixa Gmail e uma Titan:

| | Valor |
|---|---|
| Mensagens espelhadas | 65.656 (Gmail 64.018, Titan 1.638) |
| Essas mensagens nos provedores | cerca de 4,9 GB |
| `com003` + `com004` | 90 MB, cerca de 1,4 kB por mensagem |
| Um corpo em cache | 15–20 kB em média (só 3 amostras) |

Nesse tamanho, abrir 1.000 mensagens em 30 dias custa uns 20 MB, então um teto seria código sem uso. A amostra é
pequena: vale criar um teto se a `com005` passar do que o próprio espelho ocupa.
