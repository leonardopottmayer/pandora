# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído e do que está desenhado mas ainda não implementado. O que vem pela
frente está em [product-plan.md](product-plan.md).

---

## Implementado (fases 1–4, frontend, ferramentas do Assistant)

| Área | Notas |
|---|---|
| **Estrutura do módulo** | Sete projetos em camadas; schema `agenda`; DI + registro do módulo. |
| **Lembretes** | `agd006` único-disparo + recorrente; ledger por ocorrência `agd006x`; confirmar/adiar (série e por ocorrência); renomear; `ReminderSweepBackgroundService`. |
| **Motor de recorrência** | `RecurrenceRule` (parse) + `EventExpander` (expansão), subconjunto do RFC 5545, ciente de horário de verão, ordinais no estilo `-1FR`; expande no `time_zone` do item. |
| **Tarefas** | Listas `agd004` + tarefas `agd005`; subtarefas de um nível; prioridade; vencimento com ou sem hora; concluir/reabrir; recorrentes materializadas uma instância por vez, levando os alertas. |
| **Alertas** | `agd007` polimórfico + ledger de disparo `agd008`; sujeitos `Task` e `Event` ligados; `TaskAlertSweepBackgroundService` + `EventAlertSweepBackgroundService`. |
| **Calendário e eventos** | Calendários `agd001`, eventos `agd002` (ocorrências calculadas), exceções `agd003`; escopos de edição esta / esta e futuras / todas. |
| **Preferências** | `agd013_preferences`: o tempo padrão do alerta. Fuso e início da semana vêm do Identity. |
| **Hoje** | `GET /agenda/today`, leitura unificada (eventos + tarefas + lembretes). |
| **Botões de entrada** | `InboundInteractionReceivedHandler` + `TaskInteractionHandler` para `task_done` / `snooze_*` vindos do Channels. |
| **Ferramentas do Assistant** | 15 `IAssistantTool` em `Application/Assistant`: `create_reminder`, `list_reminders`, `rename_reminder`, `reschedule_reminder`, `cancel_reminder`, `create_task`, `list_tasks`, `update_task`, `complete_task`, `reopen_task`, `delete_task`, `create_event`, `update_event`, `delete_event`, `list_agenda`. O Assistant resolve datas relativas; o Agenda não interpreta nada. |
| **API** | Controllers de lembretes, listas de tarefas, tarefas, calendários, eventos, alertas, preferências e hoje. Ver [Referência da API](api-reference.md). |
| **Frontend** | `client-web/src/modules/agenda`: Hoje, Lembretes, Tarefas, Calendário (mês, semana e dia) e Configurações (`/agenda/settings`). Os formulários são modais de 640 px que empilham no celular; as telas distinguem falha de requisição de resultado vazio. |

O Notes mostra dados do Agenda no bloco Agenda ao vivo, que lê os endpoints do próprio Agenda; ver os
docs do Notes.

### Decisões diferentes do primeiro desenho

- **Três sweeps, não um.** `ReminderSweep`, `TaskAlertSweep`, `EventAlertSweep` substituem o
  `AlertSweepBackgroundService` único — cada tipo de sujeito expande de um jeito.
- **O ledger de disparo dos lembretes é o `agd006x`** (do lembrete), não o `agd008` polimórfico.
  `Alert.subject_type` aceita `Task`/`Event`/`Reminder`, mas só `Task` e `Event` estão ligados.
- **`time_zone` vai em cada linha** (lembrete/tarefa/evento/calendário) para a recorrência expandir no
  fuso do próprio item. Quando quem chama omite, o Agenda usa o padrão do `UserPreferences` do Identity
  (pela porta `IUserPreferencesReader`), e cai para UTC só quando o usuário não tem preferência.
- **As visões de semana e dia são uma grade feita à mão** (`WeekDayGrid`), não uma biblioteca de
  calendário: grade de horas com empacotamento guloso de eventos sobrepostos, faixa de dia inteiro,
  indicador de agora e clique para criar. A visão de mês mantém o `<Calendar>` do antd. As três
  respeitam o `WeekStartsOn` (a conta da semana por um `startOfWeek` manual, a grade do mês pelo
  `weekStart` do locale do dayjs). Rever só se quiserem arrastar para mover/redimensionar ou barras de
  vários dias.
- **A tela de configurações do Agenda** edita o início da semana (preferência do Identity, mostrada
  aqui porque só o calendário usa) e o tempo padrão do alerta, mostra o fuso herdado do Identity e
  escolhe o **calendário padrão**. Promover um calendário a padrão rebaixa o anterior (`UpdateCalendar`
  limpa primeiro, então o índice único parcial nunca vê dois).

## Ainda não implementado (desenhado / planejado)

| Área | Status | Fase |
|---|---|---|
| **Sincronização com o Google Calendar** | `agd009`–`agd012` (vínculo/link/cursor/conflito), `ICalendarSyncProvider` + implementação Google, push/supressão de eco, log de conflito — nada construído. Depende da I1 do [Integrations](../../integrations/pt-BR/README.md) (pronta). | 5 |
| **Sincronização com o Google Tasks** | `ITaskSyncProvider` reaproveitando o mecanismo de sincronização. | 6 |
| **Além** | Vínculo nota↔evento, criação rápida em linguagem natural, tempo de deslocamento, alertas por localização, ICS/CalDAV, provedores Microsoft/Apple, vencimentos do Finances na visão do dia. | — |

## Pontos em aberto conhecidos

1. **Profundidade das subtarefas** limitada a um nível (bate com a fidelidade do Google Tasks).
2. **Se o Finances migra para o motor RRULE** — não é pré-requisito; rever se aparecer um terceiro
   consumidor de recorrência.
