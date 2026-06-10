# Fase 12 — Relatórios e agenda

> Pré-requisitos: fase 05 (fica mais completo com 08–10).
> Referência: [finances-module.md](../finances-module.md) §3.10, §12.

## Objetivo

As visões analíticas sobre o ledger: fluxo de caixa, análise por categoria, evolução de saldo, agenda de vencimentos e a timeline de auditoria por entidade.

## Escopo

### Inclui
- Queries de leitura (Application/Queries, sem novo schema — só leitura sobre fin001–fin011):
  - `GET /reports/cash-flow?from=&to=&granularity=month` — entradas × saídas por período, por conta ou consolidado **por moeda** (sem conversão entre moedas nesta fase);
  - `GET /reports/by-category` — drill-down pai → filho, dimensões separadas sistema × usuário, filtros do extrato (período, contas, tags);
  - `GET /reports/balance-history` — evolução de saldo por conta (postado; opcional incluir projetado);
  - `GET /reports/upcoming` — agenda: faturas a vencer (com valor e status de pagamento), lançamentos agendados, próximas ocorrências de recorrência, parcelas projetadas;
  - `GET /audit?entityType=&entityId=` / `?correlationId=` — timeline completa (evolui o endpoint básico da fase 01 para a visão de linha do tempo com diffs).
- Filtro por tag e por origem em todos os relatórios aplicáveis.
- Paginação/limites de período para proteger o banco (ex.: máx. 5 anos por consulta) e índices adicionais se o plano de execução pedir.

### Não inclui
- Conversão multi-moeda consolidada (futuro — precisa de provedor de câmbio); snapshots materializados de saldo (futuro, quando o volume justificar); exportação (futuro).

## Critérios de aceite
1. Fluxo de caixa bate com o extrato para o mesmo filtro (teste de reconciliação entre queries).
2. Por categoria: transação com categoria de sistema e de usuário aparece corretamente nas duas dimensões; sem dupla contagem dentro de uma dimensão.
3. Agenda lista, em uma chamada, faturas fechadas não pagas ordenadas por vencimento + agendados + recorrências futuras do horizonte.
4. Timeline de auditoria de uma transação importada mostra a cadeia completa (arquivo → linha → sugestão → edições → aprovação).
