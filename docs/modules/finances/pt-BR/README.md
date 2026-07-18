# Módulo Finances

> Gestão de finanças pessoais dentro do monolito modular Pandora.
> **Idioma:** o inglês é a documentação principal. 🇺🇸 [English version](../README.md).

O módulo **Finances** dá ao usuário controle total das finanças pessoais: contas e cartões de
crédito, um ledger conferido de lançamentos, categorias e tags, recorrências, tratamento de
faturas/parcelamentos, importação de arquivos bancários (OFX/CSV), reversibilidade completa e uma
trilha de auditoria append-only.

A regra que rege o módulo inteiro: **o ledger é a única fonte da verdade.** Um saldo nunca é um campo
editável armazenado — é sempre o somatório com sinal dos lançamentos postados. Tudo que é automático
(importações, recorrências) passa por um inbox de staging antes de tocar o ledger, e toda mudança é
auditável até a sua origem.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o panorama de negócio e o vocabulário, depois vá ao tópico que
precisar. Cada arquivo traz o *contexto de negócio* (o que significa para o usuário e por quê) e as
*regras técnicas* (aggregates, invariantes, schema, endpoints).

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | Visão de negócio, princípios, linguagem ubíqua, escopo (dentro/fora) |
| 2 | [Arquitetura](architecture.md) | Estrutura dos projetos, blocos de DDD, ports, decisões de design |
| 3 | [Modelo de dados](data-model.md) | Catálogo completo do schema (`fin001`–`fin016`): colunas, constraints, índices |
| 4 | [Contas](accounts.md) | Tipos de conta, moeda, arquivamento |
| 5 | [Cartões e faturas](cards-and-statements.md) | Config do cartão, ciclo da fatura, resolver, pagamento, quitação sem caixa, reabertura, limite |
| 6 | [Parcelamento](installments.md) | Planos manuais e inferidos de importação, arredondamento de centavos, projeções |
| 7 | [Lançamentos (Ledger)](transactions.md) | Tipos e sinais, máquina de status, saldo, transferências, agendamentos |
| 8 | [Categorias e tags](categories-and-tags.md) | Categorias de sistema vs. do usuário, tags polimórficas |
| 9 | [Recorrências e inbox](recurrences-and-inbox.md) | Templates de recorrência, inbox de staging, aprovar/rejeitar/vincular/transferir |
| 10 | [Importação](imports.md) | Pipeline OFX/CSV, layouts, dedup/conciliação, detecção de parcelas, cutoff, retry |
| 11 | [Reversibilidade](reversibility.md) | Cancelar, desfazer, estornar e proteções de exclusão |
| 12 | [Auditoria e proveniência](audit-and-provenance.md) | Proveniência estrutural + event log append-only + catálogo de eventos |
| 13 | [Jobs e integração](jobs-and-integration.md) | Jobs de background, idempotência, eventos de integração (status) |
| 14 | [Referência de API](api-reference.md) | Todos os endpoints em `/api/v{n}/finances` |
| 15 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Finances.*` (.NET 10, DDD, comandos/queries estilo CQRS).
- **Schema:** schema PostgreSQL `finances`, tabelas com prefixo `finXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/finances` (React + TanStack Query).
- **Base da API:** `/api/v{version}/finances`, autenticada, escopada ao usuário do token.
- **Migrations:** `migrations/migrations/finances/`.
