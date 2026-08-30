# Módulo Agenda — Roadmap (trabalho restante)

> **Status:** as fases **1–4** (lembretes, recorrência, tarefas, calendário/eventos) e o frontend estão
> implementadas. Este arquivo agora rastreia só o que **ainda não foi construído** — sync Google e a
> superfície do Assistant. Para o que existe, ver os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [Lembretes](reminders.md) · [Tarefas](tasks.md) · [Calendário e Eventos](calendar-and-events.md) ·
> [Alertas e Sweep](alerts-and-sweep.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Channels](../../channels/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## Recapitulação de design (já decidido e construído)

Os três agregados, os princípios (D1–D6), o motor de recorrência, o modelo de alerta e os sweeps,
ocorrências calculadas, overrides e os escopos de edição estão todos documentados nos arquivos linkados
acima e estão **construídos**. O que resta é sync externo e o catálogo do Assistant.

---

## Fase 5 — Sync Google Calendar *(próxima; desbloqueada por Integrations I1)*

- Tabelas de sync `agd009`–`agd012`: `calendar_binding` (local ↔ remoto + direção), `sync_link`
  (`remote_id`, `etag`, hashes — evita duplicatas e ecos), `sync_cursor`, `sync_conflict`.
- `ICalendarSyncProvider` + uma implementação Google, obtendo um access token vivo do
  [Integrations](../../integrations/pt-BR/product-plan.md) (`IExternalCredentialProvider`, já construído).
- Push imediato em escritas locais; supressão de eco comparando hashes no sync link; last-write-wins com
  um log de conflito.
- Frontend: conectar conta, vincular calendários, sync-now, lista de conflitos.
- **Pronto quando:** um evento criado de qualquer lado aparece no outro dentro de um ciclo de pull, e
  editar os dois lados ao mesmo tempo resolve deterministicamente com uma linha de conflito.

## Fase 6 — Sync Google Tasks

- `ITaskSyncProvider` reusando a maquinaria de binding, link, cursor e conflito.
- **Pronto quando:** as mesmas garantias valem para listas de tarefas e tarefas.

## Fase 7 — Superfície do Assistant

- Registrar o catálogo de comandos para o [Assistant](../../assistant/pt-BR/product-plan.md):
  `create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `whats_my_day`.
  Os comandos de aplicação já existem (D6); isto é registro de descriptors, não nova lógica de domínio.
- Contrato de resolução de data relativa (o Assistant passa "agora" e o fuso; Agenda não parseia nada).
- **Pronto quando:** "lembra de ligar pro dentista amanhã às 9" cria a linha certa a partir do Telegram.

## Follow-up transversal

- **Consumir o fuso padrão do Identity.** **Feito.** A Agenda ainda guarda um `time_zone` por item
  (recorrência expande no fuso do próprio item), mas quando o caller não o informa, os create handlers
  o usam como padrão a partir do `UserPreferences` do Identity via porta `IUserPreferencesReader`,
  caindo em UTC só quando não há preferência. As forms do web enviam a preferência salva e o editor de
  alertas usa `DefaultAlertOffsetMinutes` como offset padrão. `WeekStartsOn` também já é honrado, nas
  três visões do calendário (ver abaixo).
- **Visões de semana e dia do calendário.** **Feito.** Uma grade de tempo feita à mão (`WeekDayGrid`)
  substituiu o placeholder: grade de horas, empacotamento guloso em faixas para eventos sobrepostos,
  tira de dia-inteiro, indicador de "agora", clique-para-criar e navegação unificada anterior/próximo/
  hoje. `WeekStartsOn` é honrado via um `startOfWeek` manual (matemática da semana) e o `weekStart` do
  locale do dayjs (o grid de mês do antd).
- **Tela de Configurações da Agenda.** **Feito.** A `AgendaSettingsPage` (`/agenda/settings`) expõe os
  padrões de agendamento (fuso, início da semana, offset de alerta padrão) via o contexto de
  preferências compartilhado e adiciona um seletor de calendário padrão; promover um calendário a
  padrão demove o anterior para o índice único parcial se manter.

## Além *(não agendado)*

Tags compartilhadas com Notes, anexar uma Nota a um evento, quick-add em linguagem natural na web,
tempo de deslocamento, alertas por local, import/export ICS, CalDAV, provedores Microsoft/Apple, e
puxar vencimentos do Finances para a visão do dia.

---

## Perguntas em aberto

1. **Biblioteca de UI de calendário vs. grade feita à mão.** Afeta só o polimento da visão semana/dia.
2. **Profundidade de subtarefa.** Limitada a um nível (o próprio Google Tasks suporta um nível; levantar
   o limite quebra a fidelidade do sync).
3. **Se o Finances migra para o motor RRULE.** Não é pré-requisito; reavaliar só se surgir um terceiro
   consumidor de recorrência.
4. **Colocação de quiet hours.** No Channels (ele é dono da política de entrega), reduzida a `suppress` \|
   `deliver_anyway`. Se a Agenda um dia precisar de "alertas urgentes furam quiet hours", o flag viaja no
   alerta e o Channels o honra.
