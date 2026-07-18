# Auditoria e proveniência

[← Voltar ao índice](README.md) · Tabela: `fin016_audit_event` · API: `/audit`

---

## Contexto de negócio

Toda mudança relevante é respondível depois do fato: *quem* fez *o quê*, *quando*, e *de onde veio um
lançamento*. Dois mecanismos complementares cobrem isso (decisão de design D6).

## 1. Proveniência estrutural (chaves estrangeiras)

A cadeia de FK responde "de onde isso veio" sem nenhum texto livre:

```
Transaction.pending_transaction_id → PendingTransaction.import_row_id → ImportRow.import_file_id
                                   → PendingTransaction.recurring_transaction_id
Transaction.reversed_transaction_id → a transação original
Transaction.installment_plan_id     → o plano
```

Snapshots de apoio:
- `ImportRow.raw_data` preserva os bytes originais do arquivo para a linha.
- `PendingTransaction.original_payload` preserva a sugestão inicial, imutável.

Então, de qualquer transação, você caminha de volta até a linha exata da importação (com seu conteúdo
bruto) e a sugestão de que o usuário partiu — e de qualquer arquivo de importação você lista tudo que
ele produziu.

## 2. Event log append-only (`fin016_audit_event`)

Responde "o que aconteceu e o que mudou". Toda mutação relevante grava um evento na **mesma unidade de
trabalho** da mudança (a auditoria nunca fica atrás do dado). Cada evento carrega:

- `user_id` (dono do dado) e `actor_user_id` (quem agiu; **NULL = sistema/job**),
- `entity_type` + `entity_id`,
- `event_type`,
- `data` JSONB — um diff de campos `{ field: { old, new } }` e/ou detalhe específico do evento,
- `correlation_id` — agrupa tudo de uma operação (ex.: uma importação inteira, as duas pernas de uma
  transferência, o par original+nova de uma reversão),
- `occurred_at`.

A tabela **não tem UPDATE/DELETE** por política de aplicação. Os índices suportam os dois padrões de
leitura: por entidade (`entity_type, entity_id, occurred_at`) e por timeline do usuário
(`user_id, occurred_at`).

### Exemplo respondível pelo modelo

*"A transação T veio da linha 42 de `extrato-nubank-maio.ofx`, sugerida com descrição `PAG*JoseSilva` e
sem categoria; o usuário editou a descrição para `Aluguel maio` e definiu a categoria `rent` em
2026-06-10 14:32, e aprovou às 14:33"* — cada passo é um evento estruturado, sem precisar de texto livre.

## Catálogo de eventos

| Tipo de entidade | Tipos de evento |
|---|---|
| `account` | `account.created`, `account.updated`, `account.deleted`, `account.archived`, `account.unarchived` |
| `card` | `card.created`, `card.updated`, `card.deleted`, `card.archived`, `card.unarchived` |
| `statement` | `statement.created`, `statement.closed`, `statement.reopened`, `statement.payment-received`, `statement.paid`, `statement.overdue`, `statement.settled-without-cash` |
| `transaction` | `transaction.created`, `transaction.edited`, `transaction.posted`, `transaction.voided`, `transaction.restored`, `transaction.reversed` |
| `installment-plan` | `installment-plan.created`, `installment-plan.voided`, `installment-plan.restored` |
| `recurring-transaction` | `recurring.created`, `recurring.updated`, `recurring.deleted`, `recurring.paused`, `recurring.resumed`, `recurring.finished`, `recurring.occurrence-generated` |
| `pending-transaction` | `pending.created`, `pending.edited`, `pending.approved`, `pending.rejected`, `pending.linked` |
| `user-category` | `category.created`, `category.updated`, `category.activated`, `category.deactivated` |
| `tag` | `tag.created`, `tag.updated`, `tag.deleted`, `tag.linked`, `tag.unlinked` |
| `import-file` / `import-row` | Eventos do pipeline de importação, todos amarrados pelo `correlation_id` do arquivo |

## Notas de implementação

- Eventos são gravados via `RecordAsync` do data context, a partir dos command handlers, então
  participam da mesma transação da mutação.
- Jobs gravam com `actor_user_id = user_id` para dado do usuário mas representam ação do sistema; um
  ator NULL denota eventos puramente do sistema.

## API

| Método | Rota | Propósito |
|---|---|---|
| GET | `/audit?entityType=&entityId=` | Timeline de uma entidade |
| GET | `/audit?correlationId=` | Tudo de uma operação (ex.: uma importação inteira) |

Esta é a única leitura estilo-relatório atualmente implementada; relatórios mais amplos estão
planejados (ver [Status de implementação](implementation-status.md)).
