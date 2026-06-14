# Roadmap de implementação — Módulo Finances

Sequência de fases para implementar o módulo descrito em [finances-module.md](../finances-module.md). Cada fase é entregável de ponta a ponta (migrations + domínio + API), compila e passa testes ao final, e só depende das anteriores.

Esta sequência granular é a ordem oficial de implementação; a seção 13 do documento principal agrupa as mesmas entregas em 4 macro-fases.

> Hardening pós-06/07: ver [reversibilidade-e-consistencia.md](../reversibilidade-e-consistencia.md) — corrige sincronização de fatura no `void`, adiciona `unvoid` e rollback real (hard delete) para ações desfeitas a tempo.

| Fase | Arquivo | Entrega | Depende de |
|---|---|---|---|
| 01 | [01-criacao-do-modulo.md](01-criacao-do-modulo.md) | Scaffolding do módulo, schema `finances`, trilha de auditoria (fin016) | — |
| 02 | [02-categorias.md](02-categorias.md) | Categorias do sistema (seed) e do usuário (fin002, fin003) | 01 |
| 03 | [03-contas.md](03-contas.md) | CRUD de contas (fin001) | 01 |
| 04 | [04-lancamentos.md](04-lancamentos.md) | Ledger: lançamentos em conta, transferências, saldo, extrato (fin008) | 02, 03 |
| 05 | [05-cartoes-e-faturas.md](05-cartoes-e-faturas.md) | Cartões, faturas, fechamento e pagamento (fin006, fin007) | 04 |
| 06 | [06-parcelamento.md](06-parcelamento.md) | Compras parceladas no cartão (fin009) | 05 |
| 07 | [07-tags.md](07-tags.md) | Tags e vínculos polimórficos (fin004, fin005) | 04 |
| 08 | [08-recorrencias-e-inbox.md](08-recorrencias-e-inbox.md) | Inbox de revisão (fin011) + recorrências (fin010) + job de geração | 05 |
| 09 | [09-importacao-ofx.md](09-importacao-ofx.md) | Pipeline de importação + OFX + dedup/conciliação (fin012–fin014) | 08 |
| 10 | [10-importacao-csv-e-parcelas.md](10-importacao-csv-e-parcelas.md) | CSV com layouts customizados + detecção de parcelas e projeções | 06, 09 |
| 11 | [11-regras-de-categorizacao.md](11-regras-de-categorizacao.md) | Regras de categorização automática (fin015) | 09 |
| 12 | [12-relatorios-e-agenda.md](12-relatorios-e-agenda.md) | Relatórios, agenda de vencimentos, timeline de auditoria | 05 (melhora com 08–10) |
| 13 | [13-integracao-notifications.md](13-integracao-notifications.md) | Integration events → módulo Notifications | 05, 08, 09 |

## Convenções (valem para todas as fases)

- Estrutura de projetos espelhando Identity/Notifications: `Pottmayer.Pandora.Modules.Finances.{Abstractions, Application, Contracts, Domain, Infrastructure, Persistence, Presentation}`.
- Migrations na pasta `migrations/` do repo, padrão `YYYYMMDDHHMMSS-create-table-finXXX-<nome>` com `.up.sql`/`.down.sql`; PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`, colunas de auditoria, constraints nomeadas (`pk_finXXX`, `uq_finXXX_*`, `fk_finXXX_*`, `ck_finXXX_*`).
- Aggregates: `sealed class`, `AggregateRoot<Guid>`, `IAuditable`, construtor privado + factory estática, `TimeProvider` injetado.
- Toda mutação relevante grava evento em `fin016_audit_event` (mesma unidade de trabalho) — desde a fase 02, nenhuma feature entra sem auditoria.
- Todo endpoint autenticado e escopado ao usuário do token; recursos de outro usuário retornam 404 (não 403).
- Cada fase inclui testes (unidade no domínio; integração nos fluxos principais) e atualização do front quando aplicável é tratada à parte.
