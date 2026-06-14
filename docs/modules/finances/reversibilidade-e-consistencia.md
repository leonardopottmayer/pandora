# Reversibilidade e consistência de estado — plano

> Referência: [finances-module.md](finances-module.md) (D1 — saldo/derivados a partir do ledger).
> Motivação: revisão de 2026-06-14 identificou que alguns fluxos de cancelamento deixam
> `CardStatement` (fin007) em estado inconsistente com as transações (fin008) que o originaram, e
> levantou o desejo de um "desfazer" mais forte do que `void` para o módulo como um todo.

## 1. Contexto

O total/pago de uma fatura (`CardStatement.TotalAmount`/`PaidAmount`) é um **cache incremental**:
cada handler que cria ou cancela uma transação ligada à fatura faz sua própria conta (`+=`/`-=`) e
chama `SyncAmounts` para recomputar o status derivado (`open`/`closed`/`partially-paid`/`paid`/
`overdue`). Hoje esse ajuste existe em 4 lugares com lógica duplicada, e **dois casos comuns não
fazem o ajuste no cancelamento**:

1. Voidar a transação de **pagamento de fatura** (`kind = card-statement-payment`, PIX/transferência
   da conta) não devolve o valor a `PaidAmount` — a fatura continua `paid`.
2. Voidar uma **compra ou reembolso avulso no cartão** (sem parcelamento) não devolve o valor a
   `TotalAmount`.

Além disso:
- `Void` é terminal — não existe "desfazer o void".
- `DeleteCard`/`DeleteAccount` não verificam se existem faturas/transações associadas antes de
  remover; como há FK sem `ON DELETE CASCADE` (`fk_fin007_card_id`, `fk_fin008_card_id`,
  `fk_fin008_account_id`, `fk_fin008_paid_statement_id`), qualquer cartão/conta já usado faz o
  delete estourar uma violação de FK não tratada.
- O usuário quer, além de cancelar (`void`)/compensar (`refund`), uma opção de **rollback real**
  (apagar o registro e desfazer o efeito), para os casos em que ainda é seguro fazer isso — evitando
  pares `+100`/`-100` permanentes no extrato para erros corrigidos na hora.

## 2. Vocabulário (para não confundir as 3 operações)

| Termo | O que faz | Quando usar |
|---|---|---|
| **Cancelar** (`void`) | Marca a transação como `void`, mantém a linha, reverte o efeito no cache da fatura/conta | Erro identificado depois que outras coisas já podem ter acontecido; precisa manter rastro |
| **Desfazer cancelamento** (`unvoid`) | Volta `void` → `posted`, reaplica o efeito | "Cancelei errado" |
| **Estornar** (`refund`/lançamento espelho) | Cria uma transação nova de sinal oposto | Algo que já é fato consumado no mundo real (já paguei, banco já processou) e precisa de uma reversão **datada** |
| **Rollback** (hard delete) | Remove a linha de verdade e reverte o efeito, sem deixar rastro no ledger | Erro corrigido **antes** de qualquer outra coisa depender do efeito causado |

As três primeiras (cancelar, desfazer cancelamento, estornar) já são suportadas pelo modelo de
dados atual — só precisam de correções/complementos pontuais (Etapas 1–2). O rollback (Etapa 4) é
a peça nova.

## 3. Etapa 1 — Helper único de sincronização de fatura + corrigir os 2 casos do void

**Problema**: lógica de `TotalAmount`/`PaidAmount` duplicada em `CreateTransaction` (x2),
`PayStatement` e `VoidTransaction` (só para parcelas). Os dois casos que faltam (pagamento de
fatura e compra/reembolso avulso) usam exatamente o mesmo padrão dos que já funcionam.

**Proposta**:
- Extrair `Application/Services/StatementAmountSync` (ou método estático) com uma função do tipo
  `Apply(statement, totalDelta, paidDelta, today, timeProvider)` que encapsula
  `SyncAmounts(statement.TotalAmount + totalDelta, statement.PaidAmount + paidDelta, today, timeProvider)`.
- Reescrever os 4 pontos existentes (`CreateTransaction` x2, `PayStatement`,
  `VoidTransactionCommandHandler.VoidInstallmentAsync`) para usar o helper — sem mudar o
  comportamento atual.
- Em `VoidTransactionCommandHandler`, no caminho genérico (`InstallmentPlanId is null` e
  `TransferGroupId is null`), antes de marcar `Void`:
  - se `entry.CardStatementId is not null` (compra/reembolso avulso): buscar a `CardStatement` e
    aplicar `totalDelta = -(entry.Amount * entry.Kind.StatementSign)`;
  - se `entry.PaidStatementId is not null` (pagamento de fatura): buscar a `CardStatement` paga e
    aplicar `paidDelta = -entry.Amount`.
- Gravar `statements.UpdateAsync` + manter o evento `transaction.voided` já existente (não precisa
  de evento novo — o estado da fatura é derivado).

**Critérios de aceite**:
1. Cenário do bug relatado: compra parcelada 3x → `PayStatement` integral (fatura `paid`) → `void`
   da transação de pagamento → fatura volta a `closed`/`partially-paid`/`overdue` conforme as
   datas, `PaidAmount` volta a 0.
2. Compra avulsa (não parcelada) em fatura `open` → `void` → `TotalAmount` volta ao valor anterior,
   `RemainingAmount`/status recalculados.
3. `refund` avulso em fatura `open` → `void` → `TotalAmount` volta ao valor anterior (sinal
   contrário ao do `expense`).
4. Testes de integração cobrindo os 3 cenários acima, mais o caso já existente de void de parcela
   (regressão).

## 4. Etapa 2 — `Unvoid` (desfazer cancelamento)

**Problema**: `Transaction.Void` é terminal (`bool Void(...)`: `if (IsVoid) return false`). Não há
caminho de volta.

**Proposta**:
- Novo método de domínio `Transaction.Restore(TimeProvider)`: só age se `IsVoid`; volta
  `Status = Posted`, limpa `VoidedAt`/`VoidReason`.
- Novo comando `UnvoidTransactionCommand` (mesmo formato de `VoidTransactionCommand`), espelhando
  `VoidTransactionCommandHandler`:
  - transferência: restaura as duas pernas do grupo;
  - parcela: restaura só as parcelas que o void anterior efetivamente cancelou (statement ainda
    `open` no momento do void — registrado implicitamente pelo fato de terem sido voidadas
    individualmente);
  - compra/reembolso/pagamento de fatura: usa o `StatementAmountSync` da Etapa 1 com os deltas
    invertidos.
- Endpoint `POST /transactions/{id}/unvoid`. Auditoria `transaction.restored` (+
  `installment-plan.restored` quando aplicável).

**Limite assumido (não bloquear)**: se algo mudou na fatura *depois* do void (ex.: foi paga de
novo), o `unvoid` ainda aplica o delta inverso — pode gerar `RemainingAmount` negativo
("crédito"). Isso é aceitável: o usuário vê o número e resolve manualmente. Não vale a
complexidade de detectar e bloquear esse caso (CLAUDE.md — simplicidade).

**Critérios de aceite**:
1. Void seguido de unvoid de cada um dos 4 casos (avulsa, parcela, transferência, pagamento de
   fatura) retorna a fatura/conta exatamente ao estado anterior ao void.
2. Unvoid de transação que não está `void` → erro de domínio (`Transactions.NotVoid`).

## 5. Etapa 3 — Bloquear delete de cartão/conta com histórico

**Problema**: `DeleteCardCommandHandler`/`DeleteAccountCommandHandler` chamam `RemoveAsync` sem
checar uso. FKs `fk_fin007_card_id`, `fk_fin008_card_id`, `fk_fin008_account_id`,
`fk_fin008_paid_statement_id` (sem cascade) fazem o `SaveChanges` estourar `DbUpdateException` —
provável 500 não tratado.

**Proposta**:
- `DeleteCardCommandHandler`: antes do `RemoveAsync`, checar via `ICardStatementRepository`/
  `ITransactionRepository` se existe qualquer fatura ou transação para o cartão. Se sim, retornar
  `Error.Conflict("Cards.HasHistory", "...")`. Mesma ideia para `DeleteAccountCommandHandler`
  (`Error.Conflict("Accounts.HasHistory", "...")`) verificando `ITransactionRepository`.
- Mensagem de erro orienta a arquivar em vez de excluir (arquivar já é 100% reversível via
  `SetCardArchived`/`SetAccountArchived`).
- Resultado prático: **delete físico só é possível para cadastros nunca usados** (criados por
  engano, sem nenhuma transação). Tudo que tem dinheiro envolvido só pode ser arquivado ou
  voidado/rollback — sempre reversível.

**Critérios de aceite**:
1. `DeleteCard`/`DeleteAccount` em entidade sem nenhuma transação/fatura → remove normalmente
   (comportamento atual preservado).
2. `DeleteCard`/`DeleteAccount` em entidade com histórico → 409 com erro de domínio claro, sem
   exceção de banco.

## 6. Etapa 4 — Rollback real (hard delete) para a "última ação"

**Problema**: `void`/`refund` deixam rastro (`+100`/`-100`) mesmo quando o erro foi corrigido
imediatamente, antes de qualquer outra coisa depender do efeito.

**Proposta**: oferecer **"Desfazer"** como hard delete, mas **apenas quando a operação a desfazer
ainda é a última coisa que aconteceu** no(s) recurso(s) afetado(s) — ou seja, nada posterior
depende do efeito que ela causou. Fora dessa janela, a UI oferece `void`/`refund` (já corrigidos
nas Etapas 1–2).

### 6.1 Regra de elegibilidade por tipo de operação

| Operação | Elegível para rollback se... |
|---|---|
| Transação simples de conta (`income`/`expense`/etc.) | Nenhuma outra transação posterior (`PostedAt`/`CreatedAt`) na mesma conta |
| Par de transferência | Nenhuma transação posterior em **nenhuma** das duas contas desde a transferência |
| Compra/reembolso avulso no cartão | Fatura ainda `open` **e** essa é a transação mais recente da fatura |
| Compra parcelada (plano inteiro) | Todas as N parcelas estão em faturas `open` **e** cada uma é a transação mais recente da sua fatura |
| Pagamento de fatura | Nenhuma alteração de `PaidAmount`/`TotalAmount` da fatura desde o pagamento (nenhuma transação/pagamento posterior) |
| Arquivar/desarquivar conta/cartão | N/A — já é reversível via toggle, não precisa de rollback |

A checagem "é a transação mais recente de X" é uma query simples (`MAX(CreatedAt)` no escopo —
conta, ou fatura, ou par de contas). Se a transação/plano a desfazer não é a mais recente naquele
escopo, a operação não é elegível.

### 6.2 Implementação

- Novo comando `RollbackTransactionCommand` (`DELETE /transactions/{id}`), reaproveitando:
  - `StatementAmountSync` da Etapa 1 para reverter o delta (mesmo cálculo do void, mas sem marcar
    `void` — a linha é removida);
  - a query de elegibilidade (nova, em `ITransactionRepository`, ex.:
    `IsMostRecentInScopeAsync(transaction, ct)`).
- Se não elegível → `Error.Conflict("Transactions.NotEligibleForRollback", "...")`, e a API/frontend
  oferece `void` como alternativa.
- Se elegível:
  - parcela isolada → erro (`Installments.RollbackRequiresWholePlan`): rollback de parcelamento é
    tudo ou nada (mesma régua do `VoidEntirePlan`, mas aqui simplificada porque elegibilidade já
    exige que todas as parcelas estejam em faturas `open`);
  - plano inteiro elegível → apaga as N transações + o `InstallmentPlan`, reverte o delta em cada
    fatura afetada;
  - transferência → apaga as duas pernas;
  - demais casos → apaga a transação, reverte o delta (se houver fatura associada).
- **Auditoria**: gravar `transaction.rolled-back` (e `installment-plan.rolled-back` quando
  aplicável) com um snapshot dos dados removidos (igual ao padrão já usado em `DeleteTag`, que
  audita os vínculos antes de deixar o cascade agir). O `transaction.created` original **não** é
  removido do audit — auditoria é histórico de ações, não livro contábil; o rollback não deve ser
  "invisível" para quem audita, só não deve contar nos saldos.
- **Tags**: se a transação tiver `tag_link` (fin005, sem FK física), removê-los explicitamente
  antes do delete (mesmo motivo do `DeleteTag` — sem isso ficam órfãos).

### 6.3 Critérios de aceite

1. Criar transação simples → rollback imediato → linha não existe mais, saldo da conta idêntico ao
   anterior à criação, auditoria mostra `created` + `rolled-back`.
2. Criar transação → criar outra transação na mesma conta → tentar rollback da primeira → 409
   `NotEligibleForRollback`; `void` continua disponível e funciona (Etapa 1).
3. Compra parcelada 3x (3 faturas `open`) → rollback do plano → as 3 transações e o plano somem,
   3 faturas voltam aos totais anteriores.
4. Compra parcelada 3x, mas fatura da parcela 1 já fechada → rollback do plano → 409 (não
   elegível); `void` com `VoidEntirePlan` continua sendo o caminho (comportamento atual,
   documentado na fase 06).
5. Pagar fatura → rollback do pagamento (nada mais aconteceu) → fatura volta ao estado anterior,
   transação de pagamento removida.

## 7. Ordem de implementação recomendada

1. **Etapa 1** — corrige o bug relatado e é pré-requisito de todo o resto (Unvoid e Rollback reusam
   o mesmo helper).
2. **Etapa 3** — independente, pequena, remove um risco de erro 500.
3. **Etapa 2** (`Unvoid`) — reusa o helper da Etapa 1.
4. **Etapa 4** (Rollback) — maior, reusa helper da Etapa 1 + a régua de elegibilidade é nova.

## 8. Fora de escopo / decisões já tomadas

- **Filtrar `void` dos relatórios/somatórios**: complementar ao rollback, resolve o incômodo visual
  para os casos em que rollback não é elegível e `void`/`refund` é o caminho. Não é tratado aqui —
  é ajuste de query nas fases de relatórios (12).
- **Semântica de `VoidEntirePlan` com parcelas já faturadas** (parcelas em fatura fechada/paga
  permanecem ativas e contando) — comportamento atual da fase 06 é mantido; o rollback (Etapa 4)
  simplesmente não fica disponível nesse caso, caindo para `void` parcial existente.
- **Reverter edições cosméticas** (`UpdateTransaction`) — não incluído; risco baixo, audit já guarda
  old/new para correção manual.
