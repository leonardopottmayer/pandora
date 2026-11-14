# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído e do que está desenhado mas ainda não implementado. O que vem pela
frente está em [product-plan.md](product-plan.md).

---

## Implementado (fases 1–6, frontend, ferramentas do Assistant)

| Área | Notas |
|---|---|
| **Estrutura do módulo** | Sete projetos em camadas; schema `agenda`; DI + registro do módulo. |
| **Lembretes** | `agd006` único-disparo + recorrente; ledger por ocorrência `agd006x`; confirmar/adiar (série e por ocorrência); renomear; `ReminderSweepBackgroundService`. |
| **Motor de recorrência** | `RecurrenceRule` (parse) + `EventExpander` (expansão), subconjunto do RFC 5545, ciente de horário de verão, ordinais no estilo `-1FR`; expande no `time_zone` do item. |
| **Tarefas** | Listas `agd004` + tarefas `agd005`; subtarefas de um nível; prioridade; vencimento com ou sem hora; concluir/reabrir; recorrentes materializadas uma instância por vez, levando os alertas. |
| **Alertas** | `agd007` polimórfico + ledger de disparo `agd008`; sujeitos `Task` e `Event` ligados; `TaskAlertSweepBackgroundService` + `EventAlertSweepBackgroundService`. |
| **Calendário e eventos** | Calendários `agd001`, eventos `agd002` (ocorrências calculadas), exceções `agd003`; escopos de edição esta / esta e futuras / todas. |
| **Preferências** | `agd013_preferences`: o tempo padrão do alerta. Fuso e início da semana vêm do Identity. |
| **Google Agenda (5)** | Vínculos `agd009` (`read` / `read-write` pelo acesso da conta) + links `agd010` + conflitos `agd011`; `GoogleCalendarSync` (listagem completa de um ano para trás, depois as mudanças pelo `syncToken`, 410 ⇒ completa de novo, completa mensal); `GoogleEventMapper` (com hora, dia inteiro, RRULE + EXDATE, exceções como overrides, lembretes como alertas); séries que o Pandora não expande vêm como instâncias; `GoogleCalendarPush` (links pendentes inseridos, com patch e `If-Match` ou excluídos, ocorrências como patch da instância, alertas como lembretes popup, supressão de eco pelo etag, vence a edição mais recente e a perdedora vai para o log); trava de somente leitura nos vínculos `read`; `GoogleCalendarSyncBackgroundService` a cada 2 minutos; revogada ⇒ `needs-reconnect`, desconectada ⇒ desvinculado; `AgendaAccountUsageSource`; a seção nas configurações, com direção e conflitos. Ver [Calendário e Eventos §6](calendar-and-events.md#6-google-agenda). |
| **Google Tasks (6)** | Vínculos `agd012` + links `agd014`, conflitos na `agd011` (`subject_type`); `GoogleTasksSync` (listagem completa, depois as mudanças por `updatedMin` com exclusões, completa mensal); `GoogleTaskMapper` (prazo só com data, a hora do Pandora mantida na mesma data, completed/needsAction, subtarefas de um nível; prioridade, alertas e recorrência ficam no Pandora); `GoogleTasksPush` (links pendentes inseridos, atualizados depois de ler a tarefa, excluídos; vence a edição mais recente e a perdedora vai para o log); tarefas recorrentes geram a próxima instância ao serem concluídas de qualquer lado; o mesmo job de 2 minutos; revogada ⇒ `needs-reconnect`, desconectada ⇒ desvinculado; a seção nas configurações. Ver [Tarefas §6](tasks.md#6-google-tasks). |
| **Hoje** | `GET /agenda/today`, leitura unificada (eventos + tarefas + lembretes). |
| **Botões de entrada** | `InboundInteractionReceivedHandler` + `TaskInteractionHandler` para `task_done` / `snooze_*` vindos do Channels. |
| **Ferramentas do Assistant** | 15 `IAssistantTool` em `Application/Assistant`: `create_reminder`, `list_reminders`, `rename_reminder`, `reschedule_reminder`, `cancel_reminder`, `create_task`, `list_tasks`, `update_task`, `complete_task`, `reopen_task`, `delete_task`, `create_event`, `update_event`, `delete_event`, `list_agenda`. O Assistant resolve datas relativas; o Agenda não interpreta nada. |
| **API** | Controllers de lembretes, listas de tarefas, tarefas, calendários, eventos, alertas, preferências, hoje e Google. Ver [Referência da API](api-reference.md). |
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
| **Além** | Vínculo nota↔evento, criação rápida em linguagem natural, tempo de deslocamento, alertas por localização, ICS/CalDAV, provedores Microsoft/Apple, vencimentos do Finances na visão do dia. | — |

## Pontos em aberto conhecidos

1. **Profundidade das subtarefas** limitada a um nível (bate com a fidelidade do Google Tasks).
2. **Se o Finances migra para o motor RRULE** — não é pré-requisito; rever se aparecer um terceiro
   consumidor de recorrência.
