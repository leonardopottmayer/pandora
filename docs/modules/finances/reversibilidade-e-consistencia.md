# Reversibilidade e consistência de estado — plano

> Referência: [finances-module.md](finances-module.md) (D1 — saldo/derivados a partir do ledger).
> Motivação: revisão de 2026-06-14 identificou que alguns fluxos de cancelamento deixam
> `CardStatement` (fin007) em estado inconsistente com as transações (fin008) que o originaram, e
> levantou o desejo de uma forma de "desfazer" mais forte do que `void` para o módulo como um todo.
> Revisão de 2026-06-14 (2): decidido que esse "desfazer mais forte" não deve ser um hard delete —
> mesmo para erros de cadastro, faz mais sentido criar uma **transação inversa ligada à original**
> (estorno datado), nunca apagar registros. O módulo fica 100% append-only para tudo que já foi
> postado.

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
- O usuário quer, além de cancelar (`void`)/compensar, uma forma de **"desfazer" um lançamento já
  postado criando um lançamento espelho** (sinal contrário, datado de hoje, ligado ao original) —
  inclusive para erros de cadastro corrigidos na hora. Não há hard delete em nenhum cenário: o
  ledger nunca perde uma linha que já foi postada.

## 2. Vocabulário (para não confundir as operações)

| Termo | O que faz | Quando usar |
|---|---|---|
| **Cancelar** (`void`) | Marca a transação como `void`, mantém a linha, reverte o efeito no cache da fatura/conta. Não cria linha nova. | Erro identificado depois que outras coisas já podem ter acontecido, ou correção de algo que ainda não devia ter efeito (ex.: transação `pending` lançada errada). Precisa manter rastro, mas sem um "fato" novo a registrar. |
| **Desfazer cancelamento** (`unvoid`) | Volta `void` → `posted`, reaplica o efeito. | "Cancelei errado". |
| **Reverter / Estornar** (`reverse`) | Cria uma **transação nova**, de sinal/efeito oposto, **ligada à original** via `reversed_transaction_id`, datada de hoje (data em que o estorno é percebido/feito). A transação original **não muda** — fica `posted`, com seu rastro intacto. | Algo que já é fato consumado — já paguei, já comprei, já recebi — e a correção é, ela mesma, um novo fato (estorno do banco/loja, devolução, pagamento a mais corrigido). Também serve para "desfazer" erros de cadastro **sem apagar nada**: o par original+reversão soma zero, mas ambos ficam visíveis e auditáveis. |

Não existe mais uma quarta operação de "rollback"/hard delete. Tudo que já foi `posted` permanece
no ledger para sempre — `void`/`unvoid` mudam o *status* de uma linha existente; `reverse` adiciona
uma linha nova que neutraliza o efeito da primeira. As duas primeiras (cancelar, desfazer
cancelamento) já são suportadas pelo modelo de dados atual — só precisam de correções/complementos
pontuais (Etapas 1–2). A reversão genérica (Etapa 4) é a peça nova, e reaproveita o conceito de
`refund` que já existe para faturas, generalizando-o para qualquer tipo de lançamento.

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
complexidade de detectar e bloquear esse caso (CLAUDE.md — simplicidade). A mesma régua é reusada
pela Etapa 4.

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
  engano, sem nenhuma transação). Tudo que tem dinheiro envolvido só pode ser arquivado,
  cancelado (`void`/`unvoid`) ou revertido (`reverse`, Etapa 4) — sempre reversível, nunca
  apagado.

**Critérios de aceite**:
1. `DeleteCard`/`DeleteAccount` em entidade sem nenhuma transação/fatura → remove normalmente
   (comportamento atual preservado).
2. `DeleteCard`/`DeleteAccount` em entidade com histórico → 409 com erro de domínio claro, sem
   exceção de banco.

## 6. Etapa 4 — Reversão genérica (`reverse`, transação espelho ligada à original)

**Problema**: hoje a única forma de "neutralizar" o efeito de uma transação `posted` é `void`, que
não deixa rastro de *quando* a correção aconteceu nem de que houve um segundo fato (ex.: o banco
estornou o valor numa data posterior). E `refund` já existe como "lançamento espelho", mas só para
faturas de cartão (`CanTargetStatement`), criado manualmente via `CreateTransaction` sem nenhum
vínculo estrutural com a transação que está sendo corrigida.

**Proposta**: generalizar `refund` para **qualquer** lançamento via um novo comando
`ReverseTransactionCommand`, que cria uma transação nova — mesmo destino (conta ou fatura *atual*),
sinal/efeito oposto, datada de hoje — e grava `reversed_transaction_id` apontando para a original.
A original **não é alterada** (continua `posted`, com seu próprio histórico).

### 6.1 Schema

Migration nova em `fin008_transaction`:

```sql
ALTER TABLE finances.fin008_transaction
  ADD COLUMN reversed_transaction_id uuid NULL
    CONSTRAINT fk_fin008_reversed_transaction_id REFERENCES finances.fin008_transaction (id),
  ADD CONSTRAINT uq_fin008_reversed_transaction_id UNIQUE (reversed_transaction_id),
  ADD CONSTRAINT ck_fin008_reversed_transaction_not_self
    CHECK (reversed_transaction_id IS NULL OR reversed_transaction_id <> id);

CREATE INDEX ix_fin008_reversed_transaction_id ON finances.fin008_transaction (reversed_transaction_id);
```

- `UNIQUE` garante **uma reversão por transação**: para "desfazer a reversão", o usuário reverte a
  *transação de reversão* (cria uma nova linha apontando para ela) — encadeamento permitido, sem
  limite de profundidade. A UI pode seguir a cadeia para exibir "Estorno → Estorno do estorno → …".
- `origin` (fin008) ganha um novo valor possível, `'reversal'`, para diferenciar de `'manual'` na
  listagem/relatórios (ajuste no `ck_fin008_origin` se existir, ou documentar o valor adicional).

### 6.2 Mapeamento de kind (transação → reversão)

`TransactionKind` ganha `ReversalKind()`:

| Kind original | Destino | Kind da reversão | Observação |
|---|---|---|---|
| `income` | conta | `expense` | |
| `expense` | conta | `income` | |
| `expense` | fatura (`CanTargetStatement`) | `refund` | `StatementSign` oposto (+1 → -1) |
| `refund` | fatura | `expense` | `StatementSign` oposto (-1 → +1) |
| `investment-contribution` | conta | `investment-redemption` | |
| `investment-redemption` | conta | `investment-contribution` | |
| `transfer-out` / `transfer-in` (par) | contas | novo par reverso (`transfer-in`/`transfer-out` trocados) | ver 6.3 |
| `card-statement-payment` | conta + fatura paga | `refund` (conta) | ver 6.3 — também ajusta `PaidAmount` da fatura |
| `opening-balance`, `adjustment`, `yield` | — | **não suportado (v1)** | sem kind de sinal oposto definido hoje; ver §8 |

### 6.3 Casos de implementação

- **Transação simples de conta** (`income`/`expense`/`investment-*`, sem `TransferGroupId`,
  `InstallmentPlanId`, `CardStatementId` ou `PaidStatementId`): cria uma transação na **mesma
  conta**, `occurred_on = hoje`, `kind = ReversalKind`, `amount = original.Amount`,
  `reversed_transaction_id = original.Id`, descrição default `"Estorno: {original.Description}"`
  (editável pelo usuário antes de confirmar).
- **Compra/reembolso avulso em fatura** (`CardStatementId` set, `InstallmentPlanId` null):
  resolve a fatura **atual** (aberta) via `IStatementResolver` para a data de hoje (cria a fatura
  se necessário — mesmo fluxo de `CreateTransaction`); cria a transação nessa fatura com
  `ReversalKind` e aplica `StatementAmountSync` (Etapa 1) com o delta correspondente **na fatura
  atual** — a fatura original (possivelmente já `closed`/`paid`) não é tocada. Isso reflete o
  comportamento real: um estorno de uma compra de meses atrás aparece como crédito na fatura
  corrente.
- **Par de transferência** (`TransferGroupId` set): cria um novo par (`transfer-in`/`transfer-out`
  trocados em relação ao original), `occurred_on = hoje`, novo `transfer_group_id`; cada perna nova
  tem `reversed_transaction_id` apontando para a perna original correspondente.
- **Pagamento de fatura** (`PaidStatementId` set): cria uma transação na mesma conta,
  `kind = refund` (sinal +1, dinheiro volta pra conta), `reversed_transaction_id = original.Id`; e
  aplica `StatementAmountSync(paidDelta = -original.Amount)` na fatura referenciada por
  `PaidStatementId`, no estado em que ela estiver **hoje** (mesma régua de "aceitar
  `RemainingAmount` negativo" da Etapa 2).
- **Parcela de `InstallmentPlan`**: fora de escopo v1 (ver §8) — `Transactions.ReversalNotSupported`.
- **`opening-balance`/`adjustment`/`yield`**: fora de escopo v1 (ver §8) —
  `Transactions.ReversalNotSupported`.

### 6.4 Comando, endpoint, erros

- `ReverseTransactionCommand(transactionId, description?)` → `POST /transactions/{id}/reverse`.
- Validações:
  - transação deve estar `posted` → senão `Transactions.NotPosted`;
  - `reversed_transaction_id` de outra transação não pode já apontar para esta (`UNIQUE`) →
    `Transactions.AlreadyReversed` ("esta transação já foi revertida; para desfazer, reverta a
    transação de reversão");
  - `ReversalKind()` indefinido para o `kind`/caso → `Transactions.ReversalNotSupported`.
- **Auditoria**: `transaction.reversed` na original (`data: { reversalTransactionId }`) +
  `transaction.created` na nova (mesmo evento já existente, com `origin = 'reversal'` e
  `reversedTransactionId` no payload).
- **Tags**: a transação de reversão **não** herda as tags da original automaticamente (são fatos
  diferentes); usuário pode tagueá-la depois normalmente.

### 6.5 Critérios de aceite

1. Reverter `expense` em conta → cria `income` na mesma conta, hoje, `reversed_transaction_id`
   setado; saldo da conta após a reversão = saldo antes da transação original (par soma zero).
2. Reverter compra avulsa em fatura `closed`/`paid` → cria `refund` na fatura **atual** (aberta),
   `TotalAmount` da fatura atual ajustado via `StatementAmountSync`; fatura original inalterada.
3. Reverter par de transferência → cria novo par trocado, `transfer_group_id` novo, cada perna
   ligada à perna original via `reversed_transaction_id`.
4. Reverter pagamento de fatura → cria `refund` na conta (saldo volta) + `PaidAmount` da fatura
   paga reduzido (`StatementAmountSync`, aceitando "crédito"/negativo se a fatura mudou desde o
   pagamento).
5. Reverter uma transação de reversão (encadeamento) → permitido, cria nova reversão apontando
   para a transação-reversão.
6. Reverter transação `pending` ou `void` → `Transactions.NotPosted`.
7. Reverter transação que já tem uma reversão → `Transactions.AlreadyReversed`.
8. Reverter parcela de `InstallmentPlan`, ou `opening-balance`/`adjustment`/`yield` →
   `Transactions.ReversalNotSupported`.
9. Testes de integração cobrindo os cenários 1–8.

## 7. Ordem de implementação recomendada

1. **Etapa 1** — corrige o bug relatado e é pré-requisito do resto (Unvoid e Reversão reusam o
   mesmo helper `StatementAmountSync`).
2. **Etapa 3** — independente, pequena, remove um risco de erro 500.
3. **Etapa 2** (`Unvoid`) — reusa o helper da Etapa 1.
4. **Etapa 4** (Reversão) — reusa o helper da Etapa 1 + a régua de "aceitar crédito negativo" da
   Etapa 2. Sem dependência de uma janela de elegibilidade (diferente da proposta anterior de
   rollback): uma reversão pode ser criada em qualquer momento, sempre como linha nova.

## 8. Fora de escopo / decisões já tomadas

- **Hard delete de transações**: descartado. Tudo que já foi `posted` permanece no ledger para
  sempre; `void`/`unvoid` mudam status, `reverse` adiciona uma linha nova. Não há comando que
  apague uma linha de `fin008` com efeito monetário.
- **Reversão de `InstallmentPlan`**: v1 não suporta `reverse` em transação de parcela. Enquanto
  isso, `void`/`VoidEntirePlan` (fase 06, com os ajustes da Etapa 1) continua sendo o caminho para
  corrigir parcelamentos errados. Reversão de parcelamento fica para revisão futura — decisão
  pendente: reverter parcela isolada (cria 1 `refund` na fatura atual pelo valor da parcela, sem
  alterar o plano) vs. reverter o plano inteiro (N reversões, uma por parcela, todas na fatura
  atual ou cada uma na fatura original da parcela).
- **Reversão de `opening-balance`/`adjustment`/`yield`**: sem kind de sinal oposto definido hoje
  (`Adjustment.Sign` é fixo `+1`). Corrigir esses casos continua via `void` (se ainda `pending`)
  ou novo lançamento manual de `adjustment`. Decisão pendente: introduzir uma forma de
  `adjustment` com sinal negativo (ex.: novo kind `adjustment-debit`, ou permitir sinal explícito)
  — avaliar quando aparecer um caso real.
- **Filtrar `void`/transações de reversão dos relatórios/somatórios**: ajuste de query nas fases
  de relatórios (12) — não tratado aqui. Como a reversão sempre soma zero com a original, o efeito
  em totais já é neutro; o ajuste é só para não exibir os dois lançamentos como "ruído" em listagens
  detalhadas, se desejado.
- **Semântica de `VoidEntirePlan` com parcelas já faturadas** (parcelas em fatura fechada/paga
  permanecem ativas e contando) — comportamento atual da fase 06 é mantido.
- **Reverter edições cosméticas** (`UpdateTransaction`) — não incluído; risco baixo, audit já guarda
  old/new para correção manual.
- **Herança de tags na reversão** — a transação de reversão não copia tags da original (decisão
  tomada em 6.4); se necessário no futuro, é mudança aditiva e isolada.
