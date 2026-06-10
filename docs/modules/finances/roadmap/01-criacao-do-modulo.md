# Fase 01 — Criação do módulo

> Pré-requisitos: nenhum.
> Referência: [finances-module.md](../finances-module.md) §5 (estrutura), §6 (fin016), §9 (auditoria).

## Objetivo

Módulo Finances existindo no monolito: projetos criados, registrados no Host, schema no banco e a **trilha de auditoria funcionando antes de qualquer feature** — para que as fases seguintes já nasçam auditadas.

## Escopo

### Inclui
- Projetos `Pottmayer.Pandora.Modules.Finances.{Abstractions, Application, Contracts, Domain, Infrastructure, Persistence, Presentation}` na solution, espelhando o módulo Identity (DI, DbContext do módulo, convenções de naming).
- Registro do módulo no Host (pipeline de DI, rota base `/api/finances`).
- Migrations:
  - `create-schema-finances`;
  - `create-table-fin016-audit-event` (append-only, índices `(entity_type, entity_id, occurred_at)` e `(user_id, occurred_at)`).
- Domínio/infra de auditoria:
  - port `IAuditTrail` (gravação) no Domain;
  - mecanismo de coleta: aggregates emitem domain events → handler/interceptor na pipeline de Application persiste em fin016 **na mesma unidade de trabalho**;
  - `correlation_id` propagado pelo contexto da request/job.
- Endpoint mínimo de leitura: `GET /api/finances/audit?entityType=&entityId=&correlationId=` (paginação simples) — valida a trilha de ponta a ponta.
- Projeto de testes `Pottmayer.Pandora.Modules.Finances.Tests`.

### Não inclui
- Qualquer entidade de negócio (contas, categorias etc.) — fases 02+.

## Critérios de aceite
1. Solution compila; Host sobe com o módulo registrado.
2. Migrations aplicam e revertem limpo (`up`/`down`).
3. Um evento de auditoria gravado via `IAuditTrail` em teste de integração aparece no `GET /audit` com ator, timestamp e payload.
4. Auditoria participa da transação do chamador: rollback do caso de uso não deixa evento órfão.
