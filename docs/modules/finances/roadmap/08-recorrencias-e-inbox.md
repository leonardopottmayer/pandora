# Fase 08 — Recorrências e inbox de revisão

> Pré-requisitos: fase 05 (recorrência pode mirar conta ou cartão).
> Referência: [finances-module.md](../finances-module.md) §3.6, §3.8, §6 (fin010, fin011), §7, §8.4, D4.

## Objetivo

A primeira origem automática de lançamentos: recorrências geram **transações pendentes** num inbox único de revisão (que a fase 09 reaproveita para importação), ou postam direto com `auto_post`.

## Escopo

### Inclui
- Migrations:
  - `create-table-fin011-pending-transaction` — nesta fase com `source = 'recurrence'` apenas (CHECK ampliado na fase 09); payload proposto, `original_payload` JSONB imutável, decisão (`status`, `decided_at/by`, `rejection_reason`, `transaction_id`);
  - `create-table-fin010-recurring-transaction` (template + regra + cursor);
  - `alter-table-fin008-add-origin-refs`: `pending_transaction_id` → fin011, `recurring_transaction_id` → fin010; `origin` passa a aceitar `recurrence`.
- Aggregates:
  - `RecurringTransaction`: VO `RecurrenceRule` (frequency/interval/day_of_month com clamp de fim de mês/weekday/start/end/max_occurrences) calculando próximas ocorrências; estados `active ⇄ paused → finished`; cursor `next_occurrence_on` só avança; edição de template afeta só ocorrências futuras;
  - `PendingTransaction`: `original_payload` imutável; `pending → approved/rejected` terminais; aprovação valida payload e cria a `Transaction` com proveniência completa.
- Job `RecurrenceGenerationService` (diário + on-demand ao criar/editar): gera pendentes dentro do horizonte (30 dias, configurável via Options); `auto_post = true` posta direto (auditado, ator = sistema). **Idempotente**: (recorrência, data de ocorrência) nunca gera duas vezes.
- Postagem automática de transações agendadas (`status = pending` com data atingida) entra no mesmo job — pendência herdada da fase 04.
- API:
  - `/recurring-transactions`: CRUD, `POST {id}/pause`, `POST {id}/resume`;
  - `/pending-transactions`: `GET` (inbox: filtros por source, conta/cartão, período), `PUT {id}` (editar proposta — cada edição audita diff contra o estado atual), `POST {id}/approve`, `POST {id}/reject`, `POST /approve-batch`.
- Auditoria: `recurring.*` (incl. `occurrence-generated`), `pending.created/edited/approved/rejected`.

### Não inclui
- Pendentes de importação, campos de duplicata/conciliação e de parcela em fin011 (fases 09 e 10).

## Critérios de aceite
1. Recorrência mensal dia 31 gera dia 28/29/30 nos meses curtos (clamp testado).
2. Rodar o job N vezes no mesmo dia não duplica pendentes nem posts.
3. Aprovar pendente cria transação com `origin = recurrence` e cadeia `transaction → pending → recurring` íntegra; rejeitar não cria nada; ambos terminais.
4. Edição da proposta preserva `original_payload` intacto e o diff aparece na auditoria.
5. `auto_post` em cartão respeita a decisão da questão aberta nº 5 do doc principal (fechar antes de implementar).
