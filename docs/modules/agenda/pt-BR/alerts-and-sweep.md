# Alertas e Sweep

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O alerta — um primitivo de agendamento (D1)

Eventos, tarefas e lembretes não crescem cada um sua lógica de notificação. Todo "me avise sobre isto
no tempo T" é uma linha **`Alert`** (`agd007`), polimórfica sobre seu sujeito por
`(subject_type, subject_id)` sem foreign key — validada na aplicação e removida com o sujeito.

| Campo | Significado |
|---|---|
| `subject_type` | `Task` \| `Event` \| `Reminder` — **só `Task` ligado hoje**; eventos usam o caminho do sweep de alerta-evento, lembretes mantêm o ledger `agd006x`. |
| `offset_minutes` | Com sinal, relativo à âncora do sujeito (o `due_at` de uma tarefa, o início da ocorrência de um evento). `0` = no instante, `-15` = quinze minutos antes. |
| `channels` | NULL ⇒ resolve da preferência do usuário no Channels para a categoria; senão explícito (`email`, `telegram`). |
| `is_enabled` | O sweep só varre alertas habilitados. |

## 2. Os três sweeps

Em vez de um serviço unificado, rodam três serviços hospedados especializados, cada um drenando um
comando de mediator em **uma unidade de trabalho por sujeito**:

| Serviço | Comando | Sujeito | Ledger de idempotência |
|---|---|---|---|
| `ReminderSweepBackgroundService` | `DispatchDueReminders` | lembretes | `status` (único-disparo) / `agd006x` (recorrente) |
| `TaskAlertSweepBackgroundService` | `DispatchDueTaskAlerts` | alertas de tarefa | `agd008 (alert_id, occurrence)` |
| `EventAlertSweepBackgroundService` | `DispatchDueEventAlerts` | alertas de evento | `agd008`; expande a série do evento em âncoras primeiro |

## 3. O loop do sweep

A cada tick, por sujeito:

```
janela = [agora - grace, agora + lookahead]   # grace cobre downtime; lookahead 0 por padrão
âncoras = expand(sujeito, janela)             # 1 para não-recorrente, N para recorrente (no fuso do item)
para âncora em âncoras:
    fire_at = âncora + offset
    if fire_at não in janela: continua
    if existe linha de dispatch para (sujeito/alerta, ocorrência): continua   # idempotência
    escreve linha de dispatch                 # a chave de idempotência
    publica NotifyUserRequested               # ao Channels
```

- **Idempotência.** O `UNIQUE (…, occurrence_starts_at)` da linha de dispatch significa que re-rodar o
  sweep sobre o mesmo tick — ou reiniciar no meio do tick — nunca dispara em dobro e nunca pula. Tudo
  acontece numa unidade de trabalho por alerta, então uma queda no meio do tick reproduz de forma limpa
  no próximo.
- **Grace** (padrão ~15 min) significa que um laptop que estava dormindo ainda entrega o lembrete que
  perdeu; tal disparo é marcado `is_late = true` (informativo).
- **Look-ahead** é 0 por padrão — alertas disparam no seu tick, não antes.

## 4. Entrega e botões

O sweep publica **`NotifyUserRequested`** (um contrato do Channels) com o conteúdo renderizado e os
botões inline declarados — porque *quem é dono dos botões é dono do `NotifyUserRequested`* (princípio do
Channels). O Telegram carrega os botões (*Concluído*, *Soneca 1h*); o e-mail não.

O toque volta como o **`InboundInteractionReceived`** do Channels com `owner_module = agenda`, tratado
por `InboundInteractionReceivedHandler` → `TaskInteractionHandler`:

- `task_done` conclui a tarefa (um alerta de tarefa não tem soneca por ocorrência).
- `snooze_*` move a ocorrência do lembrete.

O Channels já consumiu a interação (uso único), então um segundo toque é "expirado", não um segundo
comando.

## 5. Por que o agendamento vive aqui (D3)

Um horário de vencimento é uma **coluna numa linha**. Reagendar ou concluir um item *antes* de ele
disparar é um update local sem nada a cancelar downstream — exatamente o que um lembrete precisa, sendo
a coisa cujo horário muda. Channels só sabe *enviar agora*; Agenda decide *quando*.
