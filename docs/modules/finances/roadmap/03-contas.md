# Fase 03 — Contas

> Pré-requisitos: fase 01.
> Referência: [finances-module.md](../finances-module.md) §3.1, §6 (fin001).

## Objetivo

Cadastro de contas completo (tipos, moeda, arquivamento). Saldo ainda não existe — chega com o ledger na fase 04.

## Escopo

### Inclui
- Migration `create-table-fin001-account`.
- Aggregate `Account`: tipos `cash|checking|savings|international|crypto|investment|other`; moeda (`CurrencyCode`: ISO 4217 + tickers crypto permitidos) **imutável após criação**; nome único por usuário; `archived_at` (arquivada não aceita mutações de negócio).
- Value objects `CurrencyCode` e `Money` no Domain (Money já preparado para a fase 04: soma/subtração só entre moedas iguais).
- API `/accounts`: CRUD, `POST {id}/archive`, `POST {id}/unarchive`, listagem com ordenação (`display_order`) e filtro de arquivadas.
- Auditoria: `account.created/updated/archived/unarchived`.

### Não inclui
- Saldo inicial e saldo atual — fase 04 (saldo inicial vira transação `opening_balance`; a API de criação de conta ganha o campo `openingBalance` lá).
- `GET {id}/transactions` — fase 04.

## Critérios de aceite
1. CRUD completo com validações (tipo, moeda, unicidade de nome).
2. Tentativa de alterar moeda de conta existente falha com erro de domínio.
3. Conta arquivada: não aparece na listagem default, não aceita edição de negócio, e é reativável.
4. Auditoria de todas as mutações.
