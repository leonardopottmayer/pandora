# Lembretes

[← Voltar ao índice](README.md) · Relacionados: [Alertas e Sweep](alerts-and-sweep.md), [Modelo de dados](data-model.md)

---

Um **lembrete** é um ping num momento no tempo, sem fluxo — semântica do Apple Reminders. Dispara, e o
usuário reconhece, soneca ou cancela. Lembretes são autônomos (`agd006`), não presos a um calendário
ou lista.

## 1. Único-disparo vs. recorrente

A coluna `rrule` decide a forma, e cada forma tem uma **guarda de idempotência diferente**:

| | Único-disparo (`rrule` NULL) | Recorrente (`rrule` set) |
|---|---|---|
| Dispara | uma vez, em `remind_at` | uma vez por ocorrência |
| Guarda de idempotência | a coluna `status` | o ledger `agd006x_reminder_dispatch` |
| Após disparar | `status = Notified`; a linha não é mais selecionada, então um restart nunca re-dispara | `status` fica `Scheduled` pela vida da série; uma linha de ledger por ocorrência |
| Fim de série | — | `recurrence_ends_at` (denormalizado de UNTIL/COUNT) deixa o sweep podar séries terminadas por índice |

A recorrência expande no `time_zone` do próprio lembrete, então "todo dia útil às 08:00" dispara
exatamente uma vez por dia útil através de uma troca de DST.

## 2. Reconhecer e soneca

- **Único-disparo.** Reconhecer define `status = Acknowledged` + `acknowledged_at`; soneca define
  `status = Snoozed` + `snoozed_until`, que o sweep então trata como o `remind_at` efetivo.
  (`AcknowledgeReminder`, `SnoozeReminder`, `CancelReminder`.)
- **Recorrente.** Ack e soneca agem na **ocorrência, nunca na série** — são escritos na linha de ledger
  `agd006x` (`acknowledged_at` / `snoozed_until`). Uma ocorrência sonecada re-dispara uma vez quando
  `snoozed_until` passa (o sweep a limpa no re-disparo); uma reconhecida nunca re-dispara. A série
  segue rodando. (`AcknowledgeOccurrence`, `SnoozeOccurrence`.)

## 3. Entrega

Um lembrete devido é despachado por `ReminderSweepBackgroundService` (`DispatchDueReminders`), que
publica `NotifyUserRequested` ao [Channels](../../channels/pt-BR/overview.md) com o título do lembrete e
botões inline. O botão *Soneca 1h* do Telegram volta por `InboundInteractionReceived` e move o
lembrete. Ver [Alertas e Sweep](alerts-and-sweep.md) para a janela do sweep, grace e disparo tardio.

## 4. Comandos e endpoints

`CreateReminder`, `AcknowledgeReminder`, `SnoozeReminder`, `CancelReminder`, `AcknowledgeOccurrence`,
`SnoozeOccurrence`. HTTP: ver [Referência de API](api-reference.md) — `POST /agenda/reminders`,
`GET /agenda/reminders`, `POST /agenda/reminders/{id}/acknowledge`,
`POST /agenda/reminders/{id}/snooze`, `DELETE /agenda/reminders/{id}`.
