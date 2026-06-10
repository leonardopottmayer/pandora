# Fase 04 — Lançamentos (ledger em conta)

> Pré-requisitos: fases 02 e 03.
> Referência: [finances-module.md](../finances-module.md) §3.3, §6 (fin008), §7, §8.1, §8.6, D1, D3, D7.

## Objetivo

O coração do módulo: ledger funcionando para contas — lançamentos, transferências, saldo derivado e extrato. A partir daqui, saldo nunca mais é "campo": é somatório.

## Escopo

### Inclui
- Migration `create-table-fin008-transaction` com as colunas desta fase:
  - `account_id NOT NULL` (vira nullable na fase 05, quando entra o XOR com fatura);
  - `kind` restrito por CHECK aos kinds desta fase: `opening_balance|income|expense|transfer_in|transfer_out|investment_contribution|investment_redemption|yield|adjustment` (CHECK é ampliado nas fases 05+);
  - `status` (`pending|posted|void`), `amount > 0`, `currency`, `occurred_on`, `description`, `payee`, `notes`;
  - `system_category_id` → fin002, `user_category_id` → fin003;
  - `transfer_group_id`, `fx_rate`;
  - `origin` (`manual` por enquanto), `posted_at`, `voided_at`, `void_reason`;
  - índices de §6.
- Aggregate `Transaction` (invariantes de §5.1): kind compatível com tipo de conta (`yield`/aportes/resgates só em conta `investment` — confirmar regra; ver questão em aberto local abaixo); `posted` não edita valor/destino/kind (corrige com `void` + novo); `void` terminal; campos cosméticos (descrição, categorias, notas) editáveis com diff.
- `ITransferService`: cria/cancela o par `transfer_out`/`transfer_in` com `transfer_group_id` comum na mesma transação de banco; moedas diferentes exigem os dois valores + `fx_rate`; void cancela o par inteiro.
- `IBalanceCalculator`: saldo postado e saldo projetado (incluindo `pending`) por conta.
- Saldo inicial: campo `openingBalance` na criação de conta gera transação `opening_balance` (fase 03 retrofitada aqui).
- Lançamento agendado: `status = pending` com data futura; `POST {id}/post` efetiva (postagem automática por job fica para a fase 08, junto da infra de jobs recorrentes).
- API:
  - `/transactions`: `GET` (filtros: período, conta, kind, status, categorias, texto, origem), `POST`, `PUT {id}` (cosméticos), `POST {id}/void`, `POST {id}/post`, `POST /transactions/transfer`;
  - `/accounts/{id}/balance` e `/accounts/{id}/transactions`.
- Auditoria: `transaction.created/posted/edited/voided` com diff; transferência audita os dois lados com mesma correlação.

### Não inclui
- Lançamento em fatura, `refund`, `card_statement_payment` (fase 05); parcelamento (06); tags (07); origens automáticas (08+).

## Critérios de aceite
1. Saldo de conta = `opening_balance` + Σ posted, validado por testes com todos os kinds (sinal correto por kind).
2. Transferência entre moedas diferentes registra os dois valores e `fx_rate`; o par é atômico (falha em um lado não persiste o outro) e o void cancela ambos.
3. Editar valor de transação `posted` falha; editar descrição/categoria gera evento com diff `{old, new}`.
4. Extrato filtra por todas as dimensões e pagina de forma estável (ordenação `occurred_on` + `id`).
5. Conta arquivada rejeita lançamento novo.

## Questão a fechar nesta fase
- Kinds de investimento (`investment_contribution/redemption/yield`) ficam restritos a contas `investment` ou são permitidos em qualquer conta? Proposta: restringir; `transfer` cobre movimentação entre conta corrente e investimento.
