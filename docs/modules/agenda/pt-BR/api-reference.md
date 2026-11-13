# Referência de API

[← Voltar ao índice](README.md) · Relacionados: [Lembretes](reminders.md), [Tarefas](tasks.md), [Calendário e Eventos](calendar-and-events.md)

Caminho base: **`/api/v{version}/agenda`**. Todos os endpoints são autenticados e escopo do usuário do
token; o recurso de outro usuário devolve 404. Erros vêm de falhas tipadas `Result`.

---

## Lembretes — `/agenda/reminders`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/reminders` | Lista os lembretes do usuário. |
| POST | `/agenda/reminders` | Cria um lembrete (único-disparo ou recorrente). |
| PATCH | `/agenda/reminders/{id}` | Renomeia; horário, recorrência e status não mudam. |
| POST | `/agenda/reminders/{id}/acknowledge` | Reconhece (único-disparo). |
| POST | `/agenda/reminders/{id}/snooze` | Soneca (único-disparo). |
| DELETE | `/agenda/reminders/{id}` | Cancela. |

Reconhecer/soneca por ocorrência para lembretes recorrentes são os comandos `AcknowledgeOccurrence` /
`SnoozeOccurrence` (dirigidos pelo caminho do botão do Telegram).

## Listas de tarefas — `/agenda/task-lists`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/task-lists` | Lista. |
| POST | `/agenda/task-lists` | Cria. |
| PATCH | `/agenda/task-lists/{id}` | Renomeia / reordena / define default. |
| DELETE | `/agenda/task-lists/{id}` | Apaga (recusado se não vazia — arquive). |

## Tarefas — `/agenda/tasks`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/tasks` | Lista (por lista/status). |
| POST | `/agenda/tasks` | Cria (com pai, vencimento, recorrência opcionais). |
| PATCH | `/agenda/tasks/{id}` | Atualiza. |
| POST | `/agenda/tasks/{id}/complete` | Conclui (recorrente ⇒ próxima instância materializada). |
| POST | `/agenda/tasks/{id}/reopen` | Reabre. |
| DELETE | `/agenda/tasks/{id}` | Apaga (cascateia subtarefas). |

## Calendários — `/agenda/calendars`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/calendars` | Lista. |
| POST | `/agenda/calendars` | Cria. |
| PATCH | `/agenda/calendars/{id}` | Atualiza (nome/cor/default/visibilidade). |
| DELETE | `/agenda/calendars/{id}` | Apaga (recusado se tiver eventos vivos). |

## Eventos — `/agenda/events`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/events` | Query de intervalo — ocorrências expandidas em memória para a janela. |
| GET | `/agenda/events/{id}` | Um evento. |
| POST | `/agenda/events` | Cria (com recorrência opcional). |
| PATCH | `/agenda/events/{id}` | Atualiza com um escopo de edição (esta / esta-e-futuras / todas). |
| DELETE | `/agenda/events/{id}` | Soft delete. |

## Alertas — `/agenda/{subjectType}/{id}/alerts`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/{subjectType}/{id}/alerts` | Lista os alertas de um sujeito. |
| POST | `/agenda/{subjectType}/{id}/alerts` | Adiciona um alerta (offset + canais). |
| DELETE | `/agenda/alerts/{id}` | Remove um alerta. |

`subjectType` é `task` ou `event` (sem diferenciar maiúsculas); `reminder` é rejeitado — lembretes mantêm seu próprio ledger `agd006x` em vez de `Alert`.

## Preferências — `/agenda/preferences`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/preferences` | As preferências da Agenda do usuário; os padrões quando nunca salvas. |
| PUT | `/agenda/preferences` | Salva: `{ "defaultAlertOffsetMinutes": -15 }`. |

## Hoje — `/agenda/today`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/today` | A leitura unificada do dia: eventos (expandidos) + tarefas devidas + lembretes. |

## Google Agenda — `/agenda/google`

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/agenda/google/calendars?accountId=` | Os calendários que uma conta Google conectada vê, cada um com o `bindingId` que já o traz, se houver. |
| GET | `/agenda/google/bindings` | Os vínculos do usuário com o Google: calendário, conta, `direction` (`read`, `read-write`), status (`ok`, `needs-reconnect`, `error`), última sincronização, último erro. |
| GET | `/agenda/google/conflicts` | Os 50 últimos eventos mudados dos dois lados: título, `losingSide` (`pandora`, `google`), `losingVersion` (JSON), `detectedAt`. |
| POST | `/agenda/google/bindings` | `{ accountId, calendarIds }`: um calendário do Pandora por calendário do Google. Um calendário já vinculado é pulado. |
| POST | `/agenda/google/bindings/{id}/sync` | Sincroniza agora, retomando um vínculo parado; devolve o vínculo depois. |
| DELETE | `/agenda/google/bindings/{id}` | Desvincula: o calendário e os eventos ficam, como calendário local. |

O `isReadOnly` de um calendário (em `/agenda/calendars`) é verdadeiro num vínculo `read`: as escritas de
evento dele respondem `Agenda.CalendarReadOnly` (409). Mover um evento para dentro ou para fora de um
calendário vinculado responde `Agenda.EventMoveAcrossGoogle` (409), e excluir um calendário vinculado
`Agenda.CalendarBound` (409).