# Fase 05 — Cartões e faturas

> Pré-requisitos: fase 04.
> Referência: [finances-module.md](../finances-module.md) §3.2, §6 (fin006, fin007), §7, §8.2 (sem parcelas), §8.3, D2, D8.

## Objetivo

Ciclo completo de cartão de crédito: gastos caem na fatura certa, fatura fecha sozinha, pagamento sai de uma conta e quita (total ou parcialmente).

## Escopo

### Inclui
- Migrations:
  - `create-table-fin006-card`;
  - `create-table-fin007-card-statement` (única por `(card_id, reference_month)`);
  - `alter-table-fin008-add-card-columns`: `account_id` vira nullable, adiciona `card_statement_id`, `card_id` (denormalizado), `paid_statement_id`, CHECK XOR destino, kinds `refund` e `card_statement_payment` no CHECK.
- Aggregates:
  - `Card`: `closing_day`/`due_day` ∈ 1..28, moeda imutável, conta padrão de pagamento, arquivamento;
  - `CardStatement`: máquina de estados `open → closed → partially_paid/paid/overdue` (§7), `total_amount` cache recalculado transacionalmente, `paid_amount`.
- `IStatementResolver`: data da compra + dia de fechamento → fatura-alvo (cria se não existir; compra após fechamento → próxima); usuário pode forçar outra fatura.
- Lançamento em fatura: `expense`/`refund` com `card_statement_id`; fatura `closed` não recebe lançamento (vai para a próxima).
- Pagamento: `PayStatement(statementId, accountId, amount)` → transação `card_statement_payment` na conta com `paid_statement_id`; suporta parcial e múltiplos pagamentos; fatura em moeda ≠ conta registra `fx_rate` (questão aberta nº 3 do doc principal).
- Job `StatementLifecycleService` (diário): cria faturas próximas, fecha as com `closing_date <= hoje`, marca `overdue` após `due_date` sem quitação.
- API: `/cards` (CRUD, archive, `GET {id}/statements`, `GET {id}/available-limit`), `/statements` (`GET {id}` com lançamentos, `POST {id}/pay`, `POST {id}/close` manual).
- Auditoria: `card.*`, `statement.created/closed/payment-received/paid/overdue`.

### Não inclui
- Parcelamento (fase 06); notificações de fechamento/vencimento (fase 13).

## Critérios de aceite
1. Compra no dia do fechamento e no dia seguinte caem em faturas diferentes (regra de corte testada nas bordas, incl. dezembro→janeiro).
2. Fechamento via job é idempotente (rodar duas vezes não duplica nem re-fecha).
3. Pagamento parcial deixa `partially_paid`; soma dos pagamentos = total → `paid`; pagamento debita a conta corretamente no ledger.
4. `available-limit` = limite − Σ faturas não pagas (quando `credit_limit` informado).
5. `refund` reduz o total da fatura; total cache sempre = Σ das transações da fatura (teste de consistência).
