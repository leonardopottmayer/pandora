# Lançamentos (O Ledger)

[← Voltar ao índice](README.md) · Aggregate: `Transaction` · Tabela: `fin008_transaction` · API: `/transactions`

---

## Contexto de negócio

Um **lançamento** (transaction) é um movimento atômico no ledger — o coração do módulo. Todo o resto
escreve *através* dele. O valor é **sempre positivo**; a direção em que move um saldo é função do seu
**kind**. Um lançamento `posted` é imutável em valor/destino/kind — correções são feitas com `void` +
novo lançamento, ou um `reverse`, mantendo a auditoria honesta (ver [Reversibilidade](reversibility.md)).

## Kinds e sinais

`TransactionKind` — o efeito do valor no saldo de uma **conta** é `Sign`; o efeito no total de uma
**fatura** é `StatementSign`.

| Kind | Sinal conta | Sinal fatura | Destino | Significado |
|---|:---:|:---:|---|---|
| `opening-balance` | +1 | — | conta | Saldo inicial (sem contraparte) |
| `income` | +1 | — | conta | Entrada |
| `expense` | −1 | +1 | conta **ou** fatura | Saída / compra no cartão |
| `transfer-in` | +1 | — | conta | Perna de destino de uma transferência |
| `transfer-out` | −1 | — | conta | Perna de origem de uma transferência |
| `investment-contribution` | −1 | — | conta | Aporte (precisa de conta de investimento) |
| `investment-redemption` | +1 | — | conta | Resgate (precisa de conta de investimento) |
| `yield` | +1 | — | conta | Rentabilidade (precisa de conta de investimento) |
| `adjustment` | +1 | — | conta | Correção manual (sinal fixo +1) |
| `refund` | +1 | −1 | conta **ou** fatura | Crédito / estorno |
| `card-statement-payment` | −1 | — | conta | Pagar uma fatura (liga `paid_statement_id`) |
| `statement-writeoff` | **0** | — | *nenhum* | Quitação de fatura sem caixa (onboarding) |

Predicados derivados:
- `CanTargetStatement` → `expense` ou `refund` (os únicos kinds que caem em fatura).
- `IsTransferLeg` → `transfer-in`/`transfer-out` (o usuário nunca escolhe direto; o `CreateTransfer` sim).
- `RequiresInvestmentAccount` → `investment-contribution`/`investment-redemption`/`yield`
  (decisão da fase 04: restritos a contas `investment`; use uma transferência para mover entre
  corrente e investimento).
- `IsStatementPayment` → `card-statement-payment`.

## Regra de destino (XOR)

Imposta por `ck_fin008_target_xor`:
- Um lançamento normal tem **conta XOR fatura** (exatamente um).
- Um `statement-writeoff` não tem **nenhum** — só seta `paid_statement_id` (a contrapartida de uma
  dívida pré-Pandora, como o `opening-balance` não tem contraparte).
- `paid_statement_id` só é setado por `card-statement-payment` (de uma conta) ou `statement-writeoff`
  (de nenhum).

`currency` sempre é igual à moeda do destino.

## Máquina de status

```
pending ──post──▶ posted ──void──▶ void
pending ──void──▶ void
void ──restore (unvoid)──▶ posted
```

| Status | Significado |
|---|---|
| `pending` | Agendado / futuro — **não afeta o saldo postado**. |
| `posted` | Efetivado — contribui para saldos e totais. |
| `void` | Cancelado — mantido no ledger, efeito revertido. |

- **`posted` é imutável** em valor/destino/kind. Só campos cosméticos (descrição, payee, notas,
  categorias) são editáveis, sempre com diff auditado (`UpdateDetails`).
- `void` é reversível via `Restore` (unvoid) — ver [Reversibilidade](reversibility.md).
- `SignedAmount` = `amount × Sign` só quando `posted` **e** com destino em conta (0 caso contrário —
  então pendentes, void e lançamentos de fatura não movem o saldo de conta).

## Saldo

- **Saldo postado** de uma conta = Σ `SignedAmount` sobre seus lançamentos `posted`.
- **Saldo projetado** = postado + lançamentos `pending` (agendados/futuros).
- **Total de fatura** = Σ `StatementSign × amount` das transações da fatura, cacheado em
  `fin007.total_amount` e recomputado transacionalmente a cada mudança via `StatementAmountSync`.
- Parcelas projetadas (`origin = projection`, `status = pending`) contam só para o total *projetado*
  de uma fatura, nunca para o total postado nem um saldo.

Saldo nunca é armazenado (D1). Uma "correção de saldo" é um lançamento `adjustment`, auditável como
qualquer outro.

## Transferências

Uma transferência são **dois lançamentos ligados** (D3), construídos atomicamente por
`CreateTransferPair`:
- `transfer-out` na origem + `transfer-in` no destino, compartilhando um `transfer_group_id`.
- **Mesma moeda** → valores iguais. **Moedas diferentes** → ambos os valores informados mais `fx_rate`.
- As duas pernas postam de imediato e devem ser salvas juntas (falha em uma perna não persiste nenhuma).
- Cancelar/estornar uma transferência age no **par inteiro** (ver [Reversibilidade](reversibility.md)).

`POST /transactions/transfer` cria uma transferência; `POST /pending-transactions/transfer` cria uma a
partir de um par de sugestões do inbox (ver [Recorrências e inbox](recurrences-and-inbox.md)).

## Lançamentos agendados

Criar um lançamento com data futura e `post = false` gera um lançamento `pending` que não afeta o
saldo postado. `POST /transactions/{id}/post` o efetiva (ou o job de recorrência/fatura posta os que
venceram automaticamente).

## Descrições de sistema

Lançamentos gerados pelo sistema (saldo inicial, pagamento de fatura, write-off de fatura) deixam
`Description` vazio e carregam um `SystemDescription` (JSONB). O texto de exibição é renderizado desse
descritor **na leitura**, localizado (ex.: "Pagamento — junho 2026"). Lançamentos do usuário mantêm o
texto em `Description` e deixam `SystemDescription` nulo.

## API

| Método | Rota | Propósito |
|---|---|---|
| GET | `/transactions` | Listar com filtros ricos (período, conta, kind, status, categorias, texto, origem) |
| GET | `/transactions/{id}` | Detalhe |
| POST | `/transactions` | Criar (conta ou cartão; `installments` para parcelado) |
| POST | `/transactions/transfer` | Criar um par de transferência |
| PUT | `/transactions/{id}` | Editar campos cosméticos |
| POST | `/transactions/{id}/post` | Postar um lançamento agendado |
| POST | `/transactions/{id}/void` | Cancelar |
| POST | `/transactions/{id}/unvoid` | Desfazer cancelamento |
| POST | `/transactions/{id}/reverse` | Criar uma reversão-espelho |
| PUT | `/transactions/{id}/tags` | Substituir conjunto de tags |

## Eventos de auditoria

`transaction.created`, `transaction.posted`, `transaction.edited`, `transaction.voided`,
`transaction.restored`, `transaction.reversed`. Transferências auditam as duas pernas com a mesma
correlação.
