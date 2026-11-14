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

## 6. Google Tasks

O usuário escolhe uma conta Google e algumas listas de tarefas dela nas configurações do Agenda; cada uma
vira uma lista própria no Pandora, com o nome do Google. Um **vínculo** (`agd012`) liga as duas e guarda o
cursor; um **link** (`agd014`) liga cada tarefa do Pandora à tarefa do Google. É sempre nos dois
sentidos: uma lista do Google Tasks é da própria conta. Uma conta conectada antes da fase 6 não tem o
escopo `auth/tasks` e precisa ser reconectada.

**Ciclo.** O job dos calendários (`GoogleCalendarSyncBackgroundService`, a cada 2 minutos) também roda um
ciclo do `GoogleTasksSync` por vínculo em `ok`: primeiro as edições do Pandora, depois as do Google. O
Google Tasks não tem sync token. O primeiro ciclo, um a cada 30 dias e o que vem depois de um conflito
vencido pelo Google listam todas as tarefas vivas e removem o que o Google não tem mais; os outros pedem o
que mudou desde o começo da última listagem (`updatedMin`, um minuto antes), exclusões incluídas.

**O que cada lado guarda** (`GoogleTaskMapper`, uma função pura):

| Pandora | Google |
|---|---|
| título, notas | `title` (`(sem título)` quando vazio), `notes` |
| prazo | `due`, só a data. O Pandora manda a data no fuso da tarefa. Uma data vinda do Google mantém a hora do Pandora quando é a mesma data; uma data nova vira dia inteiro |
| `Done`, `Cancelled` / `Todo`, `InProgress` | `completed` / `needsAction` |
| subtarefa | `parent` (um nível, como no Pandora) |
| prioridade, alertas, recorrência | nada: ficam no Pandora, e a leitura nunca os apaga |

Uma tarefa recorrente vai para o Google como tarefa simples. Concluí-la, aqui ou no Google, gera a
próxima instância no Pandora, que vai para o Google no ciclo seguinte.

**Escrita** (`GoogleTasksPush`). Criar, editar, concluir, reabrir ou excluir uma tarefa numa lista
vinculada marca o link dela como pendente (`pending_push_at`) na mesma transação. Quem escolhe o id é o
Google, então o link de uma tarefa nova fica sem id até o insert, e uma subtarefa espera a tarefa-mãe. O
Google Tasks não aceita `If-Match`: a atualização lê a tarefa antes, e um etag diferente do link quer
dizer que ela mudou lá também. Vence a edição mais recente (o `updated` do Google contra a hora da edição
no Pandora), e a outra vai para a `agd011` e a lista de conflitos, como nos eventos. Uma tarefa excluída
no Google enquanto o Pandora a editava continua excluída. Um insert cuja resposta se perde é repetido e
pode deixar uma duplicata no Google.

**O que não muda.** Uma lista vinculada não pode ser excluída (`Agenda.TaskListBound`); desvincule antes.
Desvincular remove o vínculo e os links; a lista e as tarefas ficam, e nada muda no Google. Uma conta
revogada deixa os vínculos em `needs-reconnect`; uma conta desconectada os desvincula.
