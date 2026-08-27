# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Saída e Templates](outbound-and-templates.md)

Schema PostgreSQL **`channels`** (renomeado de `notifications`). Convenções: PK
`uuid DEFAULT uuid_generate_v7()`, timestamps `TIMESTAMPTZ`, colunas de auditoria
`created_by/created_at/updated_by/updated_at`, constraints nomeadas, enums como `VARCHAR` + `CHECK`.

As migrations ficam em `migrations/migrations/channels/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| chn001 | `user_channel` | Onde um usuário pode ser alcançado, por canal |
| chn002 | `channel_link_token` | O handshake de vínculo do Telegram (uso único) |
| chn003 | `interaction` | Um botão inline registrado + sua rota de volta |
| chn004 | `inbound_update` | Guarda de idempotência de entrada + trilha |
| chn005 | `notification_preference` | Política de entrega por categoria |
| chn006 | `notification` | A fila de saída durável |

---

## chn001_user_channel

Onde um usuário pode ser alcançado. Um endereço é utilizável só quando **verificado** e **habilitado**.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `channel` | varchar(20) NOT NULL | `email \| telegram` |
| `address` | varchar(255) NOT NULL | endereço de e-mail, ou chat id do Telegram |
| `locale` | varchar(10) NOT NULL | locale preferido deste endereço |
| `is_verified` / `verified_at` | bool / timestamptz | e-mail herda a ativação do Identity; Telegram é verificado pelo handshake |
| `is_enabled` / `disabled_reason` | bool / text | o interruptor do usuário, e o desligamento automático após falha permanente |
| `metadata` | jsonb | username/primeiro nome do Telegram, exibidos em configurações |

Constraints: `chk_chn001_channel`, `uq_chn001_user_channel (user_id, channel)` (um endereço por canal
por usuário), `uq_chn001_channel_address (channel, address)` (um endereço pertence a uma conta).

## chn002_channel_link_token

O handshake que liga um chat a uma conta — uso único, curta duração. A única coisa que autoriza um
chat id.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `channel` | | |
| `token` | varchar(64) NOT NULL | token aleatório curto, **único** (`uq_chn002_token`) |
| `locale` | varchar(10) | |
| `expires_at` / `consumed_at` | timestamptz | uso único, TTL ~15 minutos |

## chn003_interaction

Um botão inline registrado e sua rota de volta. O `callback_data` renderizado **é o id desta linha**, e é
assim que um callback de 64 bytes do Telegram carrega um `(usuário, módulo, ação, payload)` completo sem
guardar nada disso.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | renderizado no `callback_data` |
| `user_id` | uuid NOT NULL | checado contra o remetente do callback |
| `owner_module` | varchar(50) NOT NULL | `agenda`, `assistant`, … — lido para montar a chave de rota |
| `action` | varchar(100) NOT NULL | `task_done`, `snooze_1h`, `confirm`, … — opaco aqui |
| `payload` | text NULL | payload opaco do dono, devolvido intacto (text, não jsonb: pode ser um id puro) |
| `notification_id` | uuid NULL → chn006 | a notificação enfileirada que declarou o botão; null para mensagens de sistema (`fk_chn003_notification … ON DELETE SET NULL`) |
| `expires_at` / `consumed_at` | timestamptz | uso único — um segundo toque é "expirado", não um segundo comando |

## chn004_inbound_update

Todo update que o bot recebeu, registrado **antes** do processamento. O `update_id` do provedor torna
reprocessar inofensivo: o offset do long-polling é confirmado ao escrever esta linha.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | surrogate mantido (o plano pedia PK composta; usa-se um índice único) |
| `provider` | varchar(20) NOT NULL | `telegram` |
| `provider_update_id` | bigint NOT NULL | para o Telegram, o `update_id` |
| `raw` | jsonb NULL | update bruto para debug; **anulado** pelo job de retenção quando envelhece |
| `user_id` | uuid NULL | resolvido de `chn001`; null quando o chat é desconhecido |
| `classification` | varchar(20) NOT NULL | `Interaction \| Command \| Message \| Discarded` |
| `received_at` / `processed_at` | timestamptz | |

Constraints/índices: `uq_chn004_provider_update (provider, provider_update_id)` (guarda de idempotência),
`ix_chn004_provider_update_id_desc` (offset do long-polling no startup), `ix_chn004_received_at_unpurged`
(parcial `WHERE raw IS NOT NULL` — apoia o scan de retenção).

## chn005_notification_preference

Política de entrega por categoria: em quais canais um tipo de notificação sai, como um array ordenado.
Um array vazio significa que o usuário silenciou a categoria. `identity.*` nunca consulta isto —
notificações de segurança são obrigatórias.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `category` | uuid, varchar(100) | ex. `agenda.reminder`, `finances.statement` |
| `channels` | text[] NOT NULL DEFAULT '{}' | ordenado; vazio ⇒ silenciada |

Constraint: `uq_chn005_user_category (user_id, category)`.

> **Quiet hours estão ausentes de propósito.** Precisam do fuso IANA do usuário — agora disponível nas
> preferências do Identity — então estão desbloqueadas mas ainda não construídas; entram nesta tabela
> quando implementadas. Ver [product-plan.md](product-plan.md).

## chn006_notification

A fila durável (antes `not001_notification`).

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `channel` | varchar(20) NOT NULL | |
| `recipient` | varchar(255) NOT NULL | endereço resolvido |
| `user_id` | uuid NULL | para quem é (null para `SendNotificationRequested` ad-hoc) — dirige o histórico |
| `category` | varchar(100) NULL | categoria de entrega — dirige o histórico |
| `template_key` | varchar(100) NOT NULL | |
| `locale` | varchar(10) NOT NULL | |
| `payload` | jsonb NOT NULL | o payload plano de render |
| `subject` / `body` / `is_html` | varchar/text/bool | conteúdo de e-mail (mantido do design original) |
| `rendered_payload` | jsonb NULL | conteúdo estruturado para canais que as colunas de e-mail não expressam (teclado inline do Telegram) |
| `status` | varchar(20) NOT NULL | `Pending \| Sending \| Sent \| Failed \| Dead` |
| `attempt_count` / `max_attempts` / `next_attempt_at` / `last_error` | | estado de retry |
| `provider` / `provider_message_id` | varchar | provedor + id da mensagem após envio (habilita respostas em thread) |
| `correlation_id` | uuid NOT NULL | chave de dedup |
| `group_id` | uuid NULL | compartilhado pelas N linhas do fan-out de uma requisição |

Constraints/índices: `uq_chn006_correlation_channel (correlation_id, channel)` (dedup é **por canal** —
um fan-out compartilha um correlation id, então a unicidade inclui o canal), `chk_chn006_status`,
`ix_chn006_status_next_attempt_at` (scan do dispatcher),
`ix_chn006_user_created_at (user_id, created_at DESC)` (leitura do histórico de entrega).
