# Fase 11 — Regras de categorização

> Pré-requisitos: fase 09.
> Referência: [finances-module.md](../finances-module.md) §3.7, §6 (fin015).

## Objetivo

Auto-categorizar o que entra pelo pipeline de sugestões: "descrição contém UBER → Transport/Ride Hailing".

## Escopo

### Inclui
- Migration `create-table-fin015-categorization-rule` (prioridade única por usuário).
- Aggregate `CategorizationRule`: `match_field` (`description|payee`), `match_type` (`contains|equals|regex` — regex validada e com timeout de execução), categoria de sistema e/ou de usuário aplicadas.
- `ICategorySuggester`: aplicado na **geração de sugestões** (importação; recorrência já traz categoria do template) — primeira regra que casa, por prioridade; preenche categoria sugerida + `suggestion_confidence`.
- Reaplicação manual: endpoint para reprocessar sugestões pendentes após criar/editar regras.
- API `/categorization-rules`: CRUD, ativar/desativar, reordenação de prioridade em lote.
- Auditoria: `rule.*`; sugestão registra qual regra a categorizou (no payload do evento `pending.created`).

### Não inclui
- Aprendizado automático a partir das edições do usuário (futuro; o histórico de auditoria já guarda o insumo).

## Critérios de aceite
1. Importação com regra `contains UBER` categoriza as linhas certas; sem regra que case, categoria fica vazia (ou `is_other` se decidido) para o usuário preencher.
2. Prioridade respeitada quando duas regras casam; reordenação reflete imediatamente.
3. Regex inválida é rejeitada na criação; regex patológica não trava o pipeline (timeout).
4. Reprocessar pendentes aplica regras novas só a sugestões ainda `pending` e audita a mudança como edição.
