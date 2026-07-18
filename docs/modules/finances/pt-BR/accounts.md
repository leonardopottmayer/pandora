# Contas

[← Voltar ao índice](README.md) · Aggregate: `Account` · Tabela: `fin001_account` · API: `/accounts`

---

## Contexto de negócio

Uma **conta** é um repositório de saldo do usuário: dinheiro/carteira, corrente, poupança,
internacional, crypto, investimento ou outra. É onde o dinheiro realmente está. Um cartão de crédito
**não** é conta (ver [Cartões e faturas](cards-and-statements.md)). O **saldo da conta nunca é
armazenado** — é o somatório com sinal dos lançamentos postados (decisão de design D1).

## Regras

- **Tipos** (`AccountType`): `cash`, `checking`, `savings`, `international`, `crypto`, `investment`,
  `other`.
- **Moeda** (`CurrencyCode`): um código fiat ISO 4217 ou ticker crypto, validado por forma (3–10
  letras maiúsculas), normalizado para maiúsculas. **Fixa na criação** — não há mutator, então nunca
  muda. Racional: mudar a moeda de uma conta invalidaria a moeda de cada lançamento e o saldo derivado.
- **Nome** é único por usuário (`uq_fin001_user_name`).
- **Tipo é editável**, mas **moeda não** (ambos ausentes do caminho de update no design; no aggregate
  atual, `Update` reaceita `type` mas não `currency`).
- **Arquivamento** (`archived_at`): aposentadoria soft. Uma conta arquivada:
  - some da listagem padrão,
  - **rejeita mutações de negócio** (`Update` retorna `false` quando arquivada),
  - **rejeita novos lançamentos**,
  - mantém o histórico completo,
  - pode ser **desarquivada** de volta ao uso ativo.
- **Saldo inicial:** o saldo de partida da conta é registrado como um lançamento `opening-balance`
  (ledger puro, auditável), não um campo. É criado junto com a conta quando um saldo inicial é
  informado.

## Saldo

Duas figuras, ambas derivadas (ver [Lançamentos](transactions.md#saldo)):
- **Saldo postado** = Σ `signed(kind, amount)` sobre os lançamentos `posted` da conta.
- **Saldo projetado** = postado + lançamentos `pending` (agendados/futuros).

## API

| Método | Rota | Propósito |
|---|---|---|
| GET | `/accounts` | Listar (ordenado por `display_order`; filtrar arquivadas) |
| GET | `/accounts/{id}` | Detalhe |
| POST | `/accounts` | Criar (opcionalmente com saldo inicial) |
| PUT | `/accounts/{id}` | Atualizar config mutável |
| DELETE | `/accounts/{id}` | Excluir — **só se a conta não tem histórico** (ver [Reversibilidade](reversibility.md#proteções-de-exclusão)) |
| POST | `/accounts/{id}/archive` | Arquivar |
| POST | `/accounts/{id}/unarchive` | Desarquivar |
| GET | `/accounts/{id}/balance` | Saldo postado + projetado |
| GET | `/accounts/{id}/transactions` | Extrato da conta (filtros) |
| PUT | `/accounts/{id}/tags` | Substituir o conjunto de tags |

## Eventos de auditoria

`account.created`, `account.updated`, `account.archived`, `account.unarchived` — todos com diff de campos.
