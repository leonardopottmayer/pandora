# Fase 06 — Parcelamento

> Pré-requisitos: fase 05.
> Referência: [finances-module.md](../finances-module.md) §3.2, §6 (fin009), §8.2.

## Objetivo

Compra parcelada no cartão: um plano + N transações, uma por fatura consecutiva.

## Escopo

### Inclui
- Migrations:
  - `create-table-fin009-installment-plan` — já com o modelo completo (`origin`, `total_is_estimate`, `normalized_description`, índice `(card_id, normalized_description)`), para a fase 10 não precisar alterar;
  - `alter-table-fin008-add-installment-columns`: `installment_plan_id`, `installment_number`.
- Aggregate `InstallmentPlan`: `installment_count ≥ 2`; `installment_number` único por plano; soma das parcelas = `total_amount` quando `origin = manual`.
- Criação: `POST /transactions` com `installments = N` → cria plano (`origin = manual`) + N transações (`installment_number` 1..N) distribuídas via `IStatementResolver` nas faturas consecutivas, com divisão de centavos determinística (diferença na primeira parcela); tudo atômico.
- Cancelamento: void de uma parcela exige decisão explícita — `void` da parcela isolada ou `void` do plano inteiro (cancela parcelas em faturas ainda abertas; parcelas em fatura fechada/paga não são canceláveis).
- Visões: parcelamentos em andamento no detalhe da fatura e do cartão (`x de N`, restante).
- Auditoria: `installment-plan.created/voided`, parcelas com eventos normais de transação correlacionados ao plano.

### Não inclui
- Planos inferidos de importação e parcelas projetadas (`origin = import|projection`) — fase 10.

## Critérios de aceite
1. Compra de R$ 1000 em 3x gera 334/333/333 (ou regra equivalente documentada) somando exatamente o total, nas 3 faturas corretas.
2. Parcela em fatura fechada não pode ser voidada; o plano registra o estado de cada parcela.
3. Totais das faturas afetadas recalculados na mesma transação de banco (consistência testada).
4. `normalized_description` preenchida também em planos manuais (base de casamento futuro da fase 10).
