# Recorrências e inbox

[← Voltar ao índice](README.md) · Aggregates: `RecurringTransaction`, `PendingTransaction` · Tabelas: `fin010`, `fin011` · API: `/recurring-transactions`, `/pending-transactions`

---

## Contexto de negócio

Duas ideias conectadas:
- Uma **transação recorrente** é um template de um movimento que se repete numa agenda (aluguel,
  assinatura, salário).
- O **inbox** é uma fila única de **transações pendentes** (sugestões) aguardando a decisão do
  usuário. É o *único* lugar onde algo automático cai antes de tocar o ledger (decisão de design D4) —
  recorrências e importações alimentam ele.

## Transações recorrentes (`fin010`)

### Template + regra

- **Template:** destino `account_id` **XOR** `card_id`; `kind`; `amount` (NULL = variável/sem valor);
  `amount_is_estimate`; `description`; `payee`; categorias de sistema/usuário.
- **Regra** (`RecurrenceRule`): `frequency` (`daily|weekly|monthly|yearly`), `interval (>=1)`,
  `day_of_month (1..31)`, `weekday (0..6)`, `start_date`, `end_date`, `max_occurrences`.
- **Execução:** `status` (`active|paused|finished`), `auto_post`, `auto_generate` (default true),
  `next_occurrence_on` (cursor), `occurrences_count`.

### Regras e ciclo de vida

- **Campos estruturais são imutáveis** após a criação: destino, frequência, intervalo, âncoras de dia
  e data inicial não mudam (mantém as ocorrências passadas consistentes). Só campos voltados ao futuro
  (`end_date`, `max_occurrences`, `auto_post`, `auto_generate`) e cosméticos são editáveis via
  `UpdateTemplate`.
- **Cálculo da próxima ocorrência** (`NextOccurrenceAfter`): daily/weekly somam dias; monthly/yearly
  fazem clamp de `day_of_month` para o último dia válido do mês-alvo (ex.: dia 31 em fevereiro → 28/29).
- **Término** (`IsTerminated`): para quando `occurrences_count >= max_occurrences`, ou a próxima data
  passa de `end_date`. `AdvanceCursor` incrementa a contagem, move o cursor e vira o status para
  `finished` quando terminado.
- **Máquina de estados:** `active ⇄ paused`, `active → finished`. Uma recorrência pausada não gera.
- `POST {id}/pause`, `POST {id}/resume`, `POST {id}/generate` (gera uma ocorrência sob demanda).

### Geração (o job)

O job `RecurrenceGenerationService` roda diariamente (e sob demanda): para cada recorrência ativa com
ocorrências dentro do horizonte (default 30 dias), gera uma `PendingTransaction` (source = recurrence)
— ou **posta direto** se `auto_post` — e avança o cursor. **Idempotente:** o índice único
`(recurring_transaction_id, occurred_on)` garante que uma recorrência nunca gere a mesma data duas
vezes. `auto_generate = false` opta a recorrência fora da geração automática (só produz ocorrências
sob demanda). Ver [Jobs e integração](jobs-and-integration.md).

## Transações pendentes / o inbox (`fin011`)

Uma transação pendente é um movimento proposto. Fica editável enquanto `pending`; uma vez decidida, o
desfecho é terminal.

### Invariantes

- **`original_payload` é imutável** — um snapshot JSON da sugestão inicial, nunca alterado. O payload
  editável fica nas colunas normais; o diff entre os dois está sempre disponível.
- Source é `recurrence` (⇒ `recurring_transaction_id`) ou `import` (⇒ `import_row_id`).
- Status `pending → approved | rejected` é terminal (`Approve`/`Reject` retornam `false` se já
  decidido).

### Ações

| Ação | Efeito |
|---|---|
| **Editar** (`PUT {id}`) | Substitui o payload editável (`UpdatePayload`); `original_payload` intacto. Cada edição audita um diff. |
| **Aprovar** (`POST {id}/approve`) | Cria a `Transaction` real a partir do payload atual e a liga de volta (`transaction_id`). Terminal. |
| **Rejeitar** (`POST {id}/reject`) | Encerra a sugestão com motivo opcional. Terminal. |
| **Vincular a existente** (`POST {id}/link`) | Resolve a sugestão apontando-a para uma transação existente que o usuário identificou como o mesmo movimento — sem criar transação nova. Marcada `rejected` + `dedup_status = matched`, motivo `linked-to-existing-transaction`, registra a transação casada para a relação ser auditável. |
| **Aprovar em lote** (`POST /approve-batch`) | Aprova várias de uma vez. |
| **Transferir do pendente** (`POST /transfer`) | Transforma um par de sugestões do inbox em um par de transferência (ex.: um `transfer-out` visto na importação de uma conta e o `transfer-in` em outra). |

### Detalhes da aprovação

`Approve` copia **cada campo da sugestão como ela está agora** (edições já embutidas):
- **Destino conta** → cria uma transação de conta postada.
- **Destino cartão** → resolve/cria a fatura (honrando `suggested_statement_id` quando presente, ex.:
  uma linha importada já casada com um ciclo), cria a transação de fatura e re-sincroniza o total.
- A nova transação é ligada de volta à origem: `MarkAsImport(pending.Id)` ou
  `MarkAsRecurrence(recurringId, pending.Id)`, setando `origin` — então a proveniência é rastreável de
  qualquer lado.

Sugestões vindas de importação carregam badges de dedup/conciliação (duplicata/suspeita/casada) e
links para a transação relacionada; ver [Importação](imports.md).

## API

| Método | Rota | Propósito |
|---|---|---|
| GET / POST / PUT / DELETE | `/recurring-transactions`, `/{id}` | CRUD |
| POST | `/recurring-transactions/{id}/pause` · `/resume` · `/generate` | Controle e geração sob demanda |
| GET | `/pending-transactions` | Inbox (filtros: source, conta/cartão, período, status) |
| PUT | `/pending-transactions/{id}` | Editar proposta |
| POST | `/pending-transactions/{id}/approve` · `/reject` · `/link` | Decidir |
| POST | `/pending-transactions/approve-batch` | Aprovar em lote |
| POST | `/pending-transactions/transfer` | Montar uma transferência a partir de duas sugestões |

## Eventos de auditoria

`recurring.created/updated/deleted/paused/resumed/finished/occurrence-generated`;
`pending.created/edited/approved/rejected/linked`.
