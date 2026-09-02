# Tarefas

[← Voltar ao índice](README.md) · Relacionados: [Alertas e Sweep](alerts-and-sweep.md), [Modelo de dados](data-model.md)

---

Uma **tarefa** é algo a fazer, com um fluxo: um status, uma prioridade, um vencimento opcional,
subtarefas opcionais de um nível, e recorrência opcional. Tarefas vivem em **listas de tarefas**
(`agd004`), a *lista* do Apple Reminders / *projeto* do Todoist.

## 1. Listas

Um usuário tem ao menos uma lista; no máximo uma é a `default` (índice único parcial). Uma lista é
**arquivada** para ocultá-la, não apagada — apagar uma lista que ainda tem tarefas é recusado
(`ON DELETE RESTRICT`); a app arquiva. Listas carregam uma `position` para ordenação.
(`CreateTaskList`, `UpdateTaskList`, `DeleteTaskList`.)

## 2. A tarefa

| Campo | Comportamento |
|---|---|
| `status` | `Todo → InProgress → Done`, ou `Cancelled`. `completed_at` carimbado em Done. |
| `priority` | `None \| Low \| Medium \| High`. |
| `due_at` + `due_has_time` | Uma tarefa "para amanhã" não vence às 00:00 — `due_has_time` dirige a renderização e o offset de alerta padrão. |
| `parent_task_id` | Subtarefas são tarefas, **um nível de profundidade** (garantido no agregado). Apagar um pai cascateia suas subtarefas. |
| `position` | Ordenação dentro da lista. |
| `rrule` | Recorrência, só tarefas top-level. |

## 3. Concluir / reabrir, e recorrência

- **Concluir** (`CompleteTask`) define `status = Done` + `completed_at`.
- **Reabrir** (`ReopenTask`) a devolve para `Todo`.
- **Tarefa recorrente.** Uma tarefa recorrente é materializada **uma instância por vez**, não como
  série armazenada. Concluir a linha atual a fecha (`Done`, `completed_at`) e a aplicação insere a
  **próxima instância** da RRULE, carregando seus campos e seus alertas. Duas linhas, não uma linha
  mutável, para o histórico sobreviver. É por isso que "uma tarefa semanal recorrente concluída hoje
  reaparece na semana que vem com seus alertas."

## 4. Alertas e atraso

Alertas se ligam a uma tarefa pelo `Alert` polimórfico (`subject_type = Task`) — um dos dois tipos de
sujeito ligados (`Event` é o outro; `Reminder` mantém seu próprio ledger `agd006x`). `offset_minutes`
tem sinal relativo a `due_at` (`0` no instante, `-15` quinze minutos antes); `channels` NULL resolve da
preferência do usuário no Channels. `TaskAlertSweepBackgroundService` os despacha, e o botão *Concluído*
do Telegram completa a tarefa. Ver [Alertas e Sweep](alerts-and-sweep.md).

## 5. Comandos e endpoints

Listas: `CreateTaskList`, `UpdateTaskList`, `DeleteTaskList`. Tarefas: `CreateTask`, `UpdateTask`,
`CompleteTask`, `ReopenTask`, `DeleteTask`. Alertas: `CreateAlert`, `DeleteAlert`. HTTP: ver
[Referência de API](api-reference.md) — `/agenda/task-lists`, `/agenda/tasks` (com `{id}/complete`,
`{id}/reopen`), e `/agenda/{subjectType}/{id}/alerts`.
