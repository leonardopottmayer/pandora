# Reversibilidade e consistência

[← Voltar ao índice](README.md) · Comandos: `VoidTransaction`, `UnvoidTransaction`, `ReverseTransaction`, `DeleteAccount`, `DeleteCard`

---

## Contexto de negócio

O módulo é **append-only para dinheiro**: tudo que já foi `posted` permanece no ledger para sempre.
**Não há hard delete** de uma transação postada. Três operações desfazem erros, cada uma para uma
situação, e todas mantêm trilha de auditoria completa.

| Operação | O que faz | Quando usar |
|---|---|---|
| **Cancelar** (`void`) | Marca a transação como `void`, mantém a linha, reverte o efeito no cache da fatura/conta. Sem linha nova. | Erro identificado depois, ou cancelar algo que ainda não devia ter efeito. Precisa manter rastro, mas sem um "fato" novo a registrar. |
| **Desfazer cancelamento** (`unvoid`) | Volta `void` → `posted`, reaplica o efeito. | "Cancelei o errado." |
| **Estornar** (`reverse`) | Cria uma transação **nova**, de efeito oposto, datada de **hoje**, ligada à original via `reversed_transaction_id`. A original **não muda**. | Algo que já é fato (já paguei, já comprei, já recebi) cuja correção é ela mesma um novo fato — estorno do banco, devolução, reembolso. Também para "desfazer" erro de cadastro sem apagar nada: original + reversão somam zero, ambos ficam visíveis. |

## Cancelar (void)

`POST /transactions/{id}/void` — `VoidTransactionCommand`. Terminal no nível do aggregate (`Void`
retorna `false` se já void → `AlreadyVoid`). O handler reverte os efeitos de cache:

- **Compra/estorno avulso de cartão** (`card_statement_id` setado): desfaz o efeito no total da fatura
  (`totalDelta = −(amount × StatementSign)`).
- **Pagamento de fatura** (`paid_statement_id` setado): desfaz a contribuição ao pago da fatura
  (`paidDelta = −amount`).
- **Transferência** (`transfer_group_id` setado): o **par** inteiro é cancelado junto, na mesma
  unidade de trabalho.
- **Parcela** (`installment_plan_id` setado): ver abaixo.

Tudo através do helper único `StatementAmountSync` para todo ajuste de cache ficar consistente.
Auditoria: `transaction.voided` (por perna, correlação compartilhada).

### Cancelar parcelas

`VoidTransaction` com `VoidEntirePlan`:
- **Parcela isolada:** só permitido se a fatura ainda está **aberta**
  (`InstallmentInClosedStatement` senão — uma parcela já faturada não pode ser cancelada).
- **Plano inteiro:** cancela só parcelas em faturas **abertas**; parcelas em faturas fechadas/pagas
  permanecem. Cada total de fatura afetado é ajustado na mesma transação de banco. Auditoria:
  `transaction.voided` por parcela + `installment-plan.voided`.

## Desfazer cancelamento (unvoid)

`POST /transactions/{id}/unvoid` — restaura `void → posted` (`Restore` no-op a menos que void),
reaplicando os mesmos deltas de cache com sinal invertido. Auditoria: `transaction.restored` (+
`installment-plan.restored` quando aplicável).

> **Limite aceito:** se a fatura mudou *depois* do void (ex.: foi paga de novo), o unvoid ainda aplica
> o delta inverso e pode gerar `RemainingAmount` negativo ("crédito"). Isso é aceito — o usuário vê o
> número e resolve manualmente, em vez de adicionar complexidade para detectar e bloquear. A mesma
> régua vale para o reverse.

## Estornar (reverse)

`POST /transactions/{id}/reverse` — `ReverseTransactionCommand`. Generaliza o `refund` para qualquer
transação postada. A reversão é uma linha nova, datada de hoje, `origin = reversal`,
`reversed_transaction_id = original.id`, descrição default `"Estorno: {original.Description}"`
(sobrescrevível). A original permanece `posted`, intocada.

**Validações:**
- Original deve estar `posted` (`NotPosted` senão).
- A original não pode já ter uma reversão (`uq_fin008_reversed_transaction_id` → `AlreadyReversed`).
  Para "desfazer uma reversão", reverta a *transação de reversão* — encadeamento é permitido, sem limite.
- `ReversalKind` deve estar definido para o kind/caso (`ReversalNotSupported` senão).

**Mapeamento do kind da reversão** (`TransactionKind.ReversalKind`):

| Original | Destino | Kind da reversão |
|---|---|---|
| `income` | conta | `expense` |
| `expense` | conta | `income` |
| `expense` | fatura | `refund` |
| `refund` | fatura | `expense` |
| `investment-contribution` | conta | `investment-redemption` |
| `investment-redemption` | conta | `investment-contribution` |
| `card-statement-payment` | conta (+ fatura paga) | `refund` |
| `opening-balance`, `adjustment`, `yield`, `statement-writeoff` | — | **não suportado** |

**Comportamento por caso:**
- **Lançamento simples de conta** (income/expense/investment-*): um espelho na mesma conta, kind oposto.
- **Par de transferência:** cria um novo par no sentido oposto, com **novo** `transfer_group_id`; cada
  perna nova liga de volta (`reversed_transaction_id`) à perna original na mesma conta.
- **Pagamento de fatura:** devolve o dinheiro à conta pagadora (`refund`) e reduz o pago da fatura
  quitada **no estado em que ela está hoje** (pode ficar negativo — aceito).
- **Compra/estorno avulso de cartão:** o espelho cai na **fatura aberta atual** (resolvida/criada como
  uma nova compra), **não** na original — espelhando como um estorno real aparece na fatura deste mês
  independentemente de quando a original foi faturada.
- **Parcela** (`installment_plan_id` setado): **não suportado** na v1 — use void/void-plan.

Auditoria: `transaction.reversed` na original (`{ reversalTransactionId }`) + `transaction.created` na
nova (`origin = reversal`, `reversedTransactionId`), compartilhando correlação. Tags **não** são
herdadas pela reversão (são fatos diferentes).

## Proteções de exclusão

`DELETE /accounts/{id}` e `DELETE /cards/{id}` **só excluem fisicamente uma entidade sem histórico**.
Se qualquer transação/fatura a referencia, o comando retorna um conflito de domínio
(`AccountErrors.HasHistory` / `Cards.HasHistory`) em vez de deixar a FK estourar — a mensagem orienta a
**arquivar** (totalmente reversível). Assim, um hard delete só é possível para cadastros errados nunca
usados; qualquer coisa com dinheiro é arquivada, cancelada/desfeita ou estornada — sempre reversível,
nunca apagada.

## Fora de escopo / decidido

- **Hard delete de transações:** descartado. Nenhum comando apaga uma linha de `fin008` com efeito
  monetário.
- **Reversão de parcela / `opening-balance` / `adjustment` / `yield` / `statement-writeoff`:** não
  suportada na v1 (sem kind oposto definido). Correções vão por void ou novo lançamento manual.
- **Reverter edições cosméticas** (`UpdateTransaction`): não incluído; a auditoria já guarda old/new
  para correção manual.
