# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Alertas e Sweep](alerts-and-sweep.md)

---

## 1. Organização dos projetos

Projetos por camada sob `backend/src/Modules/Agenda/`:

```
Pottmayer.Pandora.Modules.Agenda.
  Abstractions      → registro AgendaModule, AgendaOptions, AgendaCategories
  Application       → Commands, Queries, Subscribers, Sweep (comandos de dispatch), Reminders, Tasks,
                      Mapping, Dtos, Errors, DI
  Contracts         → (vazio hoje — Agenda publica NotifyUserRequested dos contratos do Channels)
  Domain            → Aggregates, ValueObjects, Recurrence (o motor RRULE), Ports (repositórios)
  Infrastructure    → Jobs: serviços de background ReminderSweep / TaskAlertSweep / EventAlertSweep, DI
  Persistence       → EntityConfigs, Repositories, AgendaDbContext, DI
  Presentation      → Controllers (Reminders, Tasks, TaskLists, Calendars, Events, Alerts, Today), DI
```

Estilo de design: **agregados DDD** com construtores privados + factories estáticas, `TimeProvider`
injetado para toda leitura de tempo, uma camada de aplicação **command/query** (uma pasta por caso de
uso), e toda ação do usuário expressa como comando para o
[Assistant](../../assistant/pt-BR/product-plan.md) invocá-la diretamente (D6).

## 2. Blocos de domínio

### Agregados (`Domain/Aggregates`)

| Raiz de agregado | Responsabilidade / invariantes-chave |
|---|---|
| **Calendar** (`agd001`) | Container nomeado de eventos. No máximo um `default` por usuário; arquivar oculta, apagar um calendário não vazio é recusado. |
| **Event** (`agd002`) | Ocupa tempo; RRULE opcional. Ocorrências calculadas na leitura; `EventOccurrence` é a materialização em memória; desvios armazenados como overrides. Soft delete. |
| **EventOccurrenceOverride** (`agd003`) | Um desvio por ocorrência com chave `(event_id, original_starts_at)`: cancelada (EXDATE) ou campos editados. |
| **TaskList** (`agd004`) | Container nomeado de tarefas. No máximo um `default` por usuário; arquivar oculta. |
| **TaskItem** (`agd005`) | Status/prioridade/vencimento; um nível de subtarefas (garantido no agregado); recorrência materializada uma instância por vez — concluir fecha a linha e insere a próxima da RRULE, carregando campos e alertas. |
| **Reminder** (`agd006`) | Um ping. Único-disparo (rrule NULL) guardado por status; recorrente guardado pelo ledger de dispatch. |
| **ReminderDispatch** (`agd006x`) | Ledger de dispatch por ocorrência para lembretes recorrentes; carrega ack/soneca por ocorrência. |
| **Alert** (`agd007`) | Primitivo de agendamento polimórfico sobre `Task`/`Event`/`Reminder`, com chave `(subject_type, subject_id)`; `offset_minutes` com sinal; canais explícitos opcionais. |
| **AlertDispatch** (`agd008`) | Ledger de idempotência do disparo de alerta, com chave `(alert_id, occurrence_starts_at)`; sem ack/soneca. |

### Objetos de valor (`Domain/ValueObjects`)

`ReminderStatus`, `TaskItemStatus`, `TaskPriority`, `EventStatus`, `CalendarOrigin` (`Local` \|
`External`), `AlertSubjectType` (`Task` \| `Event` \| `Reminder`).

### Motor de recorrência (`Domain/Recurrence`)

`RecurrenceRule` (parseia/guarda o subconjunto do RFC 5545), `RecurrenceFrequency`, `WeekdayOrdinal`
(ordinais no estilo `-1FR`), `EventExpander` (expande uma série em ocorrências dentro de uma janela),
`EventAlertExpansion` (expande os alertas de um sujeito para instantes-âncora). A recorrência expande no
`time_zone` IANA do próprio item, então "toda segunda às 09:00" sobrevive a uma troca de DST.

### Portas (`Domain/Ports`)

Repositórios, um por agregado: `ICalendarRepository`, `IEventRepository`,
`IEventOccurrenceOverrideRepository`, `ITaskListRepository`, `ITaskRepository`, `IReminderRepository`,
`IReminderDispatchRepository`, `IAlertRepository`, `IAlertDispatchRepository`.

## 3. Os sweeps

Em vez do único `AlertSweepBackgroundService` do plano original, a implementação roda **três serviços
hospedados especializados**, cada um drenando um comando de mediator na sua própria unidade de
trabalho:

| Serviço | Comando | Guarda a idempotência com |
|---|---|---|
| `ReminderSweepBackgroundService` | `DispatchDueReminders` | `status` do lembrete (único-disparo) + ledger `agd006x` (recorrente) |
| `TaskAlertSweepBackgroundService` | `DispatchDueTaskAlerts` | ledger `agd008 (alert_id, occurrence)` |
| `EventAlertSweepBackgroundService` | `DispatchDueEventAlerts` | ledger `agd008`; expande a série do evento em âncoras primeiro |

Cada tick varre uma janela `[agora − grace, agora + lookahead]` (grace padrão ~15 min cobre downtime;
lookahead 0 por padrão), expande sujeitos recorrentes em âncoras, e para cada âncora devida escreve uma
linha de dispatch (a chave de idempotência) e publica `NotifyUserRequested` ao Channels. Uma queda no
meio do tick reproduz de forma limpa no próximo. Ver [Alertas e Sweep](alerts-and-sweep.md).

## 4. Entrada (botões do Telegram)

`InboundInteractionReceivedHandler` + `TaskInteractionHandler` assinam o `InboundInteractionReceived`
do Channels para `owner_module = agenda`, agindo em `task_done` / `snooze_*`. O Channels já consumiu a
interação, então um segundo toque é "expirado".

## 5. Decisões de design e desvios

| # | Decisão | Nota |
|---|---|---|
| **D1** | Um primitivo de alerta, varrido em background. | Implementado como **três** sweeps (lembrete, alerta-tarefa, alerta-evento) em vez de um, porque cada tipo de sujeito expande diferente. |
| **D2** | Ocorrências calculadas, não armazenadas — exceto uma **tarefa recorrente**, materializada uma instância por vez. | Um evento é `linha + rrule` expandido na leitura; uma tarefa são duas linhas (fechada + próxima) para o histórico sobreviver e a fidelidade do Google Tasks valer. |
| **D3** | Agendamento vive aqui; Channels só envia agora. | Um horário de vencimento é uma coluna; concluir/reagendar antes de disparar é um update local, nada a cancelar downstream. |
| **D4** | Tempo absoluto + fuso IANA por item. | `time_zone` é carregado **na linha** (lembrete/tarefa/evento/calendário) porque a recorrência precisa expandir no fuso do *próprio item*. O `UserPreferences` do Identity agora carrega um padrão a nível de usuário; a Agenda ainda não o consome como padrão de novos itens. |
| **—** | O ledger de dispatch de lembrete é `agd006x` (escopo do lembrete), não o polimórfico `agd008`. | Forma honesta até o Alert cobrir lembretes; migra para `agd008` depois. |

## 6. Regras transversais

- **Multi-tenant por usuário.** Toda tabela tem `user_id NOT NULL` e um índice nele; endpoints são
  escopo do usuário do token.
- **`TimeProvider` em todo lugar** — sweeps, TTLs e âncoras de recorrência são calculados contra o
  tempo injetado.
- **Guardas de delete** — apagar uma lista/calendário não vazio é recusado (arquive); apagar uma tarefa
  pai cascateia suas subtarefas.
