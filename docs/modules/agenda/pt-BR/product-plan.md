# Módulo Agenda — Roadmap (o que falta)

> **Status:** as fases **1–4** (lembretes, recorrência, tarefas, calendário/eventos), o frontend e as
> ferramentas do Assistant estão implementados. Este arquivo acompanha só o que **ainda não existe**: a
> sincronização com o Google. Para o que existe, veja os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [Lembretes](reminders.md) · [Tarefas](tasks.md) · [Calendário e Eventos](calendar-and-events.md) ·
> [Alertas e Sweep](alerts-and-sweep.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Channels](../../channels/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## Fase 5 — Sincronização com o Google Calendar *(próxima; desbloqueada pela I1 do Integrations)*

- Tabelas de sincronização `agd009`–`agd012`: `calendar_binding` (local ↔ remoto + direção), `sync_link`
  (`remote_id`, `etag`, hashes — evita duplicatas e ecos), `sync_cursor`, `sync_conflict`.
- `ICalendarSyncProvider` + uma implementação Google, que obtém um access token válido do
  [Integrations](../../integrations/pt-BR/README.md) (`IExternalCredentialProvider`, já construído).
- Push imediato nas escritas locais; supressão de eco comparando hashes no sync link; a última escrita
  vence, com log de conflito.
- Frontend: conectar conta, vincular calendários, sincronizar agora, lista de conflitos.
- **Pronto quando:** um evento criado em qualquer lado aparece no outro em um ciclo de leitura, e editar
  os dois lados ao mesmo tempo resolve de forma determinística, com uma linha de conflito.

## Fase 6 — Sincronização com o Google Tasks

- `ITaskSyncProvider` reaproveitando o vínculo, o link, o cursor e o mecanismo de conflitos.
- **Pronto quando:** as mesmas garantias valem para listas de tarefas e tarefas.

## Além *(sem data)*

Tags compartilhadas com o Notes, anexar uma nota a um evento, criação rápida em linguagem natural na
web, tempo de deslocamento, alertas por localização, importação/exportação ICS, CalDAV, provedores
Microsoft/Apple e trazer os vencimentos do Finances para a visão do dia.

---

## Questões em aberto

1. **Profundidade das subtarefas.** Limitada a um nível (o próprio Google Tasks suporta um nível;
   tirar o limite quebra a fidelidade da sincronização).
2. **Se o Finances migra para o motor RRULE.** Não é pré-requisito; rever só se aparecer um terceiro
   consumidor de recorrência.
3. **Onde ficam as horas de silêncio.** No Channels (que é dono da política de entrega), reduzidas a
   `suppress` \| `deliver-anyway`. Se o Agenda um dia precisar de "alertas urgentes furam o silêncio", a
   flag vai no alerta e o Channels respeita.
