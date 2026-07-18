# Parcelamento

[← Voltar ao índice](README.md) · Aggregate: `InstallmentPlan` · Tabela: `fin009_installment_plan` · API: `/installment-plans`, `/cards/{id}/installment-plans`

---

## Contexto de negócio

Comprar parcelado é essencial no Brasil. Uma compra de cartão dividida em N parcelas é modelada como
**um plano + N transações**, uma por fatura consecutiva. Cada parcela é uma cobrança `expense` real e
efetivada na própria fatura — não são projeções (exceto as futuras inferidas de importação, ver abaixo).

## Regras

- **Mínimo de 2 parcelas** (`InstallmentCount >= 2`, `MinInstallments = 2`).
- **Origem:**
  - `manual` — criada pelo usuário. As parcelas **somam exatamente** `total_amount`
    (`total_is_estimate = false`).
  - `import` — inferida de um arquivo bancário onde só o valor da parcela corrente é conhecido
    (`total_is_estimate = true`, `total_amount = valor × count`). Ver [Importação](imports.md).
- **Divisão com arredondamento de centavos** (`SplitAmount`): o total é dividido em partes arredondadas
  a centavos, com qualquer resto de arredondamento na **primeira** parcela, de modo que as partes
  somem exatamente. Exemplo: `1000.00` em 3× → `333.34 / 333.33 / 333.33`.
- **`normalized_description`**: a descrição sem o marcador de parcela (`3/12`, `03/12`, `PARC 3/12`,
  `3 de 12`), em minúsculas e com espaços colapsados. É a **chave de casamento** usada para conciliar
  parcelas importadas com um plano existente (fase de importação). É preenchida até em planos manuais,
  para importações futuras casarem com eles.
- **`first_reference_month`** (`yyyy-MM`): o mês de referência da fatura da primeira parcela. Para
  planos inferidos de importação criados a partir da parcela N, é inferido retroativamente
  (mês da fatura atual − (N−1) meses).

## Criando uma compra parcelada

`POST /transactions` com `installments = N` (N ≥ 2):
1. Cria um `InstallmentPlan` (`origin = manual`).
2. Cria N transações `expense` (`installment_number` 1..N) distribuídas nas faturas consecutivas via
   `StatementResolver`, usando a divisão de centavos determinística.
3. Tudo é atômico; os totais das faturas afetadas são recomputados na mesma transação de banco.

## Read model

O `InstallmentPlanAssembler` monta a visão do plano: cada parcela com o mês de referência e status de
sua fatura, o **valor restante** (soma das parcelas não pagas e não canceladas) e a contagem de
parcelas **pagas** (parcelas cuja fatura está `paid`). Parcelas canceladas (void) ficam de fora de
ambas as figuras.

## Projeções (parcelas futuras inferidas de importação)

Quando um plano é inferido de uma importação, as parcelas **futuras** (N+1..count) são criadas como
transações `pending` com `origin = projection` nas faturas seguintes — para o usuário ver o
comprometimento das próximas faturas. Elas contam para o total *projetado* de uma fatura, não para o
`total_amount` postado nem para nenhum saldo. Parcelas passadas (1..N−1) **não** são geradas
automaticamente. Ver [Importação → detecção de parcelas](imports.md#detecção-de-parcelas--projeção).

## Cancelamento

Cancelar (void) uma parcela exige decisão explícita — cancelar a parcela isolada ou o plano inteiro
(que cancela parcelas ainda em faturas **abertas**; parcelas em faturas fechadas/pagas não são
canceláveis). Ver [Reversibilidade](reversibility.md).

## API

| Método | Rota | Propósito |
|---|---|---|
| GET | `/installment-plans/{id}` | Detalhe do plano (read model) |
| GET | `/cards/{id}/installment-plans` | Planos de um cartão |

Compras parceladas são criadas via `/transactions` (com `installments`), não um endpoint dedicado.

## Eventos de auditoria

`installment-plan.created` (com origem), e cada parcela carrega o evento normal `transaction.created`
correlacionado ao plano.
