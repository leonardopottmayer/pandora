# Parcelamento

[← Voltar ao índice](README.md) · Aggregate: `InstallmentPlan` · Tabela: `fin009_installment_plan` · API: `/installment-plans`, `/cards/{id}/installment-plans`

---

## Contexto de negócio

Comprar parcelado é essencial no Brasil. Uma compra de cartão dividida em N parcelas é modelada como
**um plano + N transações**, uma por fatura consecutiva. Cada parcela é uma cobrança `expense` real e
efetivada na própria fatura.

Hoje só existem planos criados **manualmente**. O próprio comentário XML do agregado `InstallmentPlan`
é explícito: *"In this phase only manual plans exist... import-inferred plans (estimated total,
projected future installments) arrive in phase 10."* O schema e o value object `Origin` já permitem uma
origem `import`, e os parsers OFX/CSV já extraem o marcador de parcela da descrição para o `ImportRow`
(ver [Importação](imports.md#extração-do-marcador-de-parcela-implementado)), mas nada ainda transforma
isso em um `InstallmentPlan` — aprovar essa sugestão hoje só cria uma transação simples. Ver
[Status de Implementação](implementation-status.md).

## Regras

- **Mínimo de 2 parcelas** (`InstallmentCount >= 2`, `MinInstallments = 2`).
- **Origem:** hoje só `manual` é criada — pelo usuário, via `InstallmentPlan.CreateManual`. As parcelas
  **somam exatamente** `total_amount` (`total_is_estimate = false`). A origem `import` existe no
  domínio/schema para uma fase futura (inferida de um arquivo bancário onde só o valor da parcela
  corrente é conhecido: `total_is_estimate = true`, `total_amount = valor × count`), mas nenhum caminho
  de código a cria ainda.
- **Divisão com arredondamento de centavos** (`SplitAmount`): o total é dividido em partes arredondadas
  a centavos, com qualquer resto de arredondamento na **primeira** parcela, de modo que as partes
  somem exatamente. Exemplo: `1000.00` em 3× → `333.34 / 333.33 / 333.33`.
- **`normalized_description`**: a descrição sem o marcador de parcela (`3/12`, `03/12`, `PARC 3/12`,
  `3 de 12`), em minúsculas e com espaços colapsados. É gravada em todo plano (manual, hoje) como
  futura chave de casamento para conciliar parcelas importadas — ainda não é consumida em lugar nenhum.
- **`first_reference_month`** (`yyyy-MM`): o mês de referência da fatura da primeira parcela.

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

## Projeções (planejado, não implementado)

O design prevê que um plano inferido de importação gere suas parcelas **futuras** (N+1..count) como
transações com `origin = projection` nas faturas seguintes, para o usuário ver o comprometimento com
antecedência. O valor `EntryOrigin.Projection` já existe no código para isso, mas nada cria uma
transação com ele hoje — está sem uso. Ver [Importação](imports.md) e
[Status de Implementação](implementation-status.md).

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
