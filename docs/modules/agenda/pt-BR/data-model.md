# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Alertas e Sweep](alerts-and-sweep.md)

Schema PostgreSQL **`agenda`**. Convenções: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ` em todo
lugar (tempo armazenado absoluto, D4), colunas de auditoria `created_by/created_at/updated_by/updated_at`,
constraints nomeadas, enums como `VARCHAR` + `CHECK` armazenados **PascalCase** (para casar com
`agd006_reminder.status`). Cada item carrega seu próprio `time_zone` IANA, porque a recorrência expande
no fuso do próprio item. (Quando uma requisição de criação o omite, a Agenda o define a partir do `UserPreferences` do Identity — um fuso padrão a nível de usuário — caindo em UTC só quando o usuário não tem preferência.)

As migrations ficam em `migrations/migrations/agenda/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| agd001 | `calendar` | Container nomeado de eventos |
| agd002 | `event` | Evento de calendário (+ RRULE, expandido na leitura) |
| agd003 | `event_occurrence_override` | Desvio por ocorrência de uma série de eventos |
| agd004 | `task_list` | Container nomeado de tarefas |
| agd005 | `task` | Uma tarefa (status/prioridade/vencimento/subtarefas/recorrência) |
| agd006 | `reminder` | Um ping num instante (único-disparo ou recorrente) |
| agd006x | `reminder_dispatch` | Ledger de dispatch por ocorrência para lembretes recorrentes |
| agd007 | `alert` | Primitivo polimórfico "me avise sobre *sujeito*" |
| agd008 | `alert_dispatch` | Ledger de idempotência do disparo de alerta |
| agd009–agd012 | *(reservadas)* | Calendar binding / sync link / cursor / conflict — **não implementadas** (fases 5–6) |

---

## agd001_calendar

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `name` | varchar(200) NOT NULL | |
| `color` | varchar(50) NULL | |
| `is_default` / `is_visible` | bool | no máximo um default por usuário (`uq_agd001_user_default`, parcial `WHERE is_default`) |
| `time_zone` | varchar(100) NOT NULL DEFAULT 'UTC' | recorrência expande aqui |
| `origin` | varchar(20) NOT NULL DEFAULT 'Local' | `Local \| External` (`chk_agd001_origin`); só `Local` importa até o sync Google |
| `archived_at` | timestamptz NULL | arquivar oculta; apagar um calendário com eventos vivos é recusado pela app |

## agd002_event

Um evento é **calculado, nunca armazenado**: uma linha mais uma rrule, expandida em ocorrências na
leitura.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `calendar_id` | uuid | `fk_agd002_calendar … ON DELETE RESTRICT` (um calendário mantém seus eventos) |
| `title` / `description` / `location` / `url` | | `url` = link da reunião |
| `starts_at` / `ends_at` | timestamptz NOT NULL | dia inteiro ⇒ meia-noite no `time_zone`, fim exclusivo |
| `is_all_day` | bool | |
| `time_zone` | varchar(100) | IANA, por evento |
| `rrule` | text NULL | subconjunto RFC 5545, literal; NULL ⇒ ocorrência única |
| `recurrence_ends_at` | timestamptz NULL | limite denormalizado da última ocorrência (de UNTIL/COUNT) para uma query de intervalo podar séries terminadas por índice |
| `status` | varchar(20) DEFAULT 'Confirmed' | `Confirmed \| Tentative \| Cancelled` |
| `deleted_at` | timestamptz NULL | soft delete (um futuro sync de entrada pode ressuscitar) |

Índices: `ix_agd002_user_id`, `ix_agd002_calendar_id` (query de intervalo + guarda de delete).

## agd003_event_occurrence_override

Chave natural `(event_id, original_starts_at)` — qual ocorrência, pelo seu início na grade.

| Coluna | Tipo | Notas |
|---|---|---|
| `event_id` | uuid → agd002 | `ON DELETE CASCADE` |
| `original_starts_at` | timestamptz NOT NULL | identifica a ocorrência |
| `is_cancelled` | bool | o caso EXDATE (a ocorrência some) |
| `starts_at` / `ends_at` / `title` / `description` / `location` | NULL | colunas não-nulas sobrescrevem a série naquela ocorrência; NULL cai de volta |

Constraint: `uq_agd003_event_occurrence (event_id, original_starts_at)`. Editar "esta e futuras" em vez
disso **divide** a série (uma nova linha `agd002`) e não escreve override.

## agd004_task_list

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `name` | varchar(200) | |
| `is_default` | bool | `uq_agd004_user_default` (parcial `WHERE is_default`) |
| `position` | int | ordenação |
| `archived_at` | timestamptz NULL | |

## agd005_task

Uma tarefa recorrente é materializada **uma instância por vez**: concluir fecha a linha atual e a app
insere a próxima da RRULE, carregando campos e alertas (duas linhas, para o histórico sobreviver).

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `list_id` | uuid | `fk_agd005_list … ON DELETE RESTRICT` |
| `parent_task_id` | uuid NULL → agd005 | subtarefas; um nível, garantido no agregado; `ON DELETE CASCADE` |
| `title` / `notes` | | |
| `due_at` / `due_has_time` | timestamptz / bool | uma tarefa "para amanhã" não vence às 00:00 — `due_has_time` dirige a renderização + offset de alerta padrão |
| `priority` | varchar(10) DEFAULT 'None' | `None \| Low \| Medium \| High` |
| `status` | varchar(20) DEFAULT 'Todo' | `Todo \| InProgress \| Done \| Cancelled` |
| `completed_at` | timestamptz NULL | |
| `time_zone` | varchar(100) | recorrência expande aqui |
| `rrule` | text NULL | só tarefas top-level; NULL ⇒ não recorrente |
| `position` | int | |
| `deleted_at` | timestamptz NULL | soft delete |

Índices: `ix_agd005_user_id`, `ix_agd005_list_status (list_id, status)` (tela de lista),
`ix_agd005_parent_task_id` (parcial).

## agd006_reminder

Um ping num instante. Único-disparo (`rrule` NULL) é guardado por `status`; recorrente é guardado pelo
ledger `agd006x`, e seu `status` fica `Scheduled` pela vida da série.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `title` / `notes` | | |
| `remind_at` | timestamptz NOT NULL | |
| `time_zone` | varchar(100) DEFAULT 'UTC' | |
| `rrule` | text NULL | subconjunto RFC 5545, literal; NULL ⇒ único-disparo |
| `recurrence_ends_at` | timestamptz NULL | fim de série denormalizado para o sweep podar séries terminadas por índice |
| `status` | varchar(20) | `Scheduled \| Notified \| Acknowledged \| Snoozed \| Cancelled` |
| `snoozed_until` / `acknowledged_at` | timestamptz NULL | ação do único-disparo |

Índices: `ix_agd006_user_id`, `ix_agd006_status_remind_at` (caminho quente do sweep único-disparo),
`ix_agd006_recurrence_ends_at` (parcial `WHERE rrule IS NOT NULL`, sweep recorrente).

## agd006x_reminder_dispatch

Ledger de dispatch por ocorrência para lembretes recorrentes — o que torna o sweep idempotente quando a
coluna `status` não pode (um lembrete recorrente dispara muitas vezes).

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `reminder_id` | uuid → agd006 | `ON DELETE CASCADE` |
| `user_id` | uuid | |
| `occurrence_starts_at` | timestamptz NOT NULL | |
| `dispatched_at` / `correlation_id` | | |
| `is_late` | bool | disparado da janela de grace (uma máquina suspensa se recuperou); informativo |
| `acknowledged_at` / `snoozed_until` | timestamptz NULL | ação por ocorrência (ack/soneca agem na ocorrência, nunca na série) |

Constraint: `uq_agd006x_reminder_occurrence (reminder_id, occurrence_starts_at)` — um dispatch por
(lembrete, ocorrência). Índice `ix_agd006x_snoozed_until` (parcial) para o caminho de re-disparo da
soneca.

> **Nota de nomenclatura.** Este ledger é `agd006x_` (uma extensão do agregado lembrete), não o
> polimórfico `agd008` — a forma honesta até o Alert cobrir lembretes; migra para `agd008` depois.

## agd007_alert

O primitivo de agendamento polimórfico: uma linha por ping desejado, com chave para um sujeito por
`(subject_type, subject_id)` **sem FK** (validado na app, removido com o sujeito).

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `subject_type` | varchar(20) | `Task \| Event \| Reminder` (`chk_agd007_subject_type`) — **`Task` e `Event` estão ligados**; `Reminder` mantém o ledger `agd006x` em vez disso |
| `subject_id` | uuid NOT NULL | |
| `offset_minutes` | int NOT NULL | com sinal, relativo à âncora do sujeito (`0` = no instante, `-15` = 15 min antes) |
| `channels` | text[] NULL | NULL ⇒ resolve da preferência do usuário no Channels; senão explícito (`email`, `telegram`) |
| `is_enabled` | bool DEFAULT true | |

Índices: `ix_agd007_user_id`, `ix_agd007_subject (subject_type, subject_id)`,
`ix_agd007_enabled_subject_type` (parcial `WHERE is_enabled`, a raiz do scan do sweep).

## agd008_alert_dispatch

O ledger de dispatch de alerta. Uma linha na primeira vez que um alerta dispara para uma âncora de
sujeito.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `alert_id` | uuid → agd007 | `ON DELETE CASCADE` |
| `user_id` | uuid | |
| `occurrence_starts_at` | timestamptz NOT NULL | |
| `dispatched_at` / `correlation_id` | | |
| `is_late` | bool | disparado da janela de grace; informativo |

Constraint: `uq_agd008_alert_occurrence (alert_id, occurrence_starts_at)`. Sem ack/soneca — o botão de
um alerta de tarefa conclui a própria tarefa.

## agd009–agd012 *(reservadas — não implementadas)*

Sync Google (fases 5–6): `calendar_binding`, `sync_link` (`remote_id`, `etag`, hashes), `sync_cursor`,
`sync_conflict`. Ver [product-plan.md](product-plan.md).
