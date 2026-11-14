# Jobs de background e integração

[← Voltar ao índice](README.md) · `Infrastructure/Jobs` · `Contracts`

---

## Jobs de background

Três `BackgroundService`s hospedados dirigem o comportamento automático do módulo. Todos são
**idempotentes** e **auditados** (ação do sistema). Cada um usa um `PeriodicTimer` dirigido pelo
`TimeProvider` injetado, e um guarda `JobConcurrency` impede execuções sobrepostas.

| Job | Intervalo | Responsabilidade |
|---|---|---|
| `RecurrenceGenerationBackgroundService` | a cada hora (+ sob demanda) | Para cada recorrência ativa com ocorrências dentro do horizonte (default 30 dias), gera uma `PendingTransaction` — ou posta direto quando `auto_post`. Também posta transações `pending` agendadas cuja data chegou. Idempotente via índice único `(recurring_transaction_id, occurred_on)`. |
| `StatementLifecycleBackgroundService` | a cada hora | Cria faturas próximas, fecha faturas com `closing_date <= hoje` e marca `overdue` após o vencimento com saldo em aberto. Idempotente (rodar duas vezes não duplica nem re-fecha). |
| `ImportParsingBackgroundService` | a cada 30s (polling de fila) | Pega arquivos de importação `received` e faz o parsing de forma assíncrona. A falha de uma linha não aborta o arquivo. |

**"Hoje" é o dia de cada usuário.** Os dois jobs guiados por data processam todos os usuários numa
passada, mas cada linha segue o dia do seu dono no fuso dele (`UserDays`, via
`IEffectiveTimeZoneResolver`): às 16:00 UTC um usuário em Tóquio já está no dia seguinte e um em São
Paulo, não. Rodam a cada hora para o dia virar perto da meia-noite de cada usuário. Os comandos aceitam
um `Today` opcional que aplica uma data só para todos (testes, execução manual).

Os comandos on-demand correspondentes (`RunRecurrenceGeneration`, `RunStatementLifecycle`,
`RunImportParsing`, `GenerateRecurringTransactionOccurrence`) permitem disparar a mesma lógica de forma
síncrona (ex.: logo após criar uma recorrência, ou em testes).

## Garantias de idempotência

- **Recorrência:** o índice único de banco `(recurring_transaction_id, occurred_on)` é a garantia
  dura — uma dada ocorrência nunca é gerada duas vezes, mesmo sob execuções concorrentes.
- **Ciclo de fatura:** as transições de estado de uma fatura são derivadas de valores/datas via
  `SyncAmounts`, e o fechamento checa `closed_at`, então reexecutar é no-op.
- **Importação:** o job de parsing transiciona o arquivo `received → parsing → completed`; só arquivos
  `received` são pegos.

## Eventos de integração (status)

O projeto `Pottmayer.Pandora.Modules.Finances.Contracts` existe como a casa pretendida dos eventos de
integração consumidos por outros módulos (Channels é o consumidor óbvio), mas está atualmente
**vazio** — nenhum evento de integração é publicado ainda.

Eventos planejados (não implementados):

| Evento | Gatilho | Intenção do consumidor |
|---|---|---|
| `StatementClosed` | Uma fatura fecha (valor, vencimento) | "Sua fatura fechou" |
| `StatementDueSoon` | X dias antes do vencimento | Lembrete |
| `StatementOverdue` | Vencida, não paga | Alerta |
| `ImportCompleted` | Importação concluída com N sugestões | "Você tem N lançamentos para revisar" |
| `PendingTransactionsGenerated` | Job de recorrência produziu sugestões | Lembrete de revisão |

Quando implementados, seriam publicados pelos casos de uso/jobs existentes e assinados pelo módulo
Channels, com idempotência (um evento por transição) e `correlation_id` propagado do evento de
domínio até a notificação. Ver [Status de implementação](implementation-status.md).
