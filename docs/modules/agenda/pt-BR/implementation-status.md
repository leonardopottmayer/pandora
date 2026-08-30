# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído versus o que está desenhado mas ainda não implementado. O roadmap
adiante fica em [product-plan.md](product-plan.md).

---

## Implementado (fases 1–4 + frontend)

| Área | Notas |
|---|---|
| **Scaffold do módulo** | Sete projetos por camada; schema `agenda`; DI + registro do módulo. |
| **Lembretes** | `agd006` único-disparo + recorrente; ledger por ocorrência `agd006x`; reconhecer/soneca (série e por ocorrência); `ReminderSweepBackgroundService`. |
| **Motor de recorrência** | `RecurrenceRule` parse + `EventExpander` expand, subconjunto RFC 5545, ciente de DST, ordinais no estilo `-1FR`; expande no `time_zone` do item. |
| **Tarefas** | `agd004` listas + `agd005` tarefas; subtarefas de um nível; prioridade; vencimento com/sem hora; concluir/reabrir; recorrente materializada uma instância por vez carregando alertas. |
| **Alertas** | `agd007` polimórfico + ledger de dispatch `agd008`; sujeito `Task` ligado; `TaskAlertSweepBackgroundService`. |
| **Calendário e eventos** | `agd001` calendários, `agd002` eventos (ocorrências calculadas), `agd003` overrides; escopos de edição esta / esta-e-futuras / todas; `EventAlertSweepBackgroundService`. |
| **Hoje** | Leitura unificada `GET /agenda/today` (eventos + tarefas + lembretes). |
| **Botões de entrada** | `InboundInteractionReceivedHandler` + `TaskInteractionHandler` para `task_done` / `snooze_*` do Channels. |
| **API** | Controllers de reminders, task-lists, tasks, calendars, events, alerts, today. |
| **Frontend** | `client-web/src/modules/agenda` — Hoje, Lembretes, Tarefas, Calendário. |

### Desvios notáveis do plano original

- **Três sweeps, não um.** `ReminderSweep`, `TaskAlertSweep`, `EventAlertSweep` substituem o único
  `AlertSweepBackgroundService` — cada tipo de sujeito expande diferente.
- **O ledger de dispatch de lembrete é `agd006x`** (escopo do lembrete), não o polimórfico `agd008`.
  Migra para `agd008` quando o Alert cobrir lembretes.
- **`Alert.subject_type` admite `Task`/`Event`/`Reminder` mas só `Task` está ligado**; eventos usam o
  sweep de alerta-evento diretamente, lembretes mantêm `agd006x`.
- **`time_zone` é carregado por linha** (lembrete/tarefa/evento/calendário) para a recorrência expandir
  no fuso do próprio item. Quando o caller não o informa, a Agenda agora usa como padrão o
  `UserPreferences` do Identity (via porta `IUserPreferencesReader`), caindo em UTC só quando o usuário
  não tem preferência. As forms do web também enviam a preferência salva, e o editor de alertas usa
  `DefaultAlertOffsetMinutes` como offset padrão.
- **As visões de semana e dia são uma grade de tempo feita à mão** (`WeekDayGrid`), não uma
  biblioteca de calendário: grade de horas com empacotamento guloso em faixas para eventos que se
  sobrepõem, tira de dia-inteiro, indicador de "agora" e clique-para-criar. A visão de mês mantém o
  `<Calendar>` do antd. As três honram `WeekStartsOn` (a matemática da semana via um `startOfWeek`
  manual; o grid de mês via o `weekStart` do locale do dayjs).
- **Tela de Configurações da Agenda** (`AgendaSettingsPage`, `/agenda/settings`): expõe os padrões de
  agendamento (fuso, início da semana, offset de alerta padrão) via o contexto de preferências
  compartilhado — uma fonte de verdade com o Identity, não uma cópia — mais um seletor de
  **calendário padrão**. Promover um calendário a padrão demove o anterior (o `UpdateCalendar` o
  limpa antes, para o índice único parcial nunca ver dois).

## Ainda não implementado (desenhado / planejado)

| Área | Status | Fase |
|---|---|---|
| **Sync Google Calendar** | `agd009`–`agd012` (binding/link/cursor/conflict), `ICalendarSyncProvider` + impl Google, push/supressão de eco, log de conflito — nada construído. Depende de [Integrations](../../integrations/pt-BR/overview.md) I1 (feito). | 5 |
| **Sync Google Tasks** | `ITaskSyncProvider` reusando a maquinaria de sync. | 6 |
| **Catálogo de comandos do Assistant** | Comandos são comandáveis (D6), mas o registro de descriptors (`create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `whats_my_day`) para o Assistant não está ligado. | 7 |
| **Além** | Links Nota↔evento, quick-add NL, tempo de deslocamento, alertas por local, ICS/CalDAV, provedores Microsoft/Apple, vencimentos do Finances na visão do dia. | — |

## Pontos em aberto conhecidos

1. **Biblioteca de UI de calendário vs. grade feita à mão** — **decidido: feita à mão**
   (`WeekDayGrid`), sem nova dependência. Revisitar só se quiser arrastar-para-mover/redimensionar ou
   barras de eventos multi-dia.
2. **Profundidade de subtarefa** limitada a um nível (casa com a fidelidade do Google Tasks).
3. **Se o Finances migra para o motor RRULE** — não é pré-requisito; reavaliar se surgir um terceiro
   consumidor de recorrência.
