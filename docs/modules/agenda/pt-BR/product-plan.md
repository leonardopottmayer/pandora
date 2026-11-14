# Módulo Agenda — Roadmap (o que falta)

> **Status:** as fases **1–4** (lembretes, recorrência, tarefas, calendário/eventos), o frontend e as
> ferramentas do Assistant estão implementados, e a sincronização nos dois sentidos com o Google Agenda (fase 5) e o Google Tasks (fase 6). Este arquivo
> acompanha só o que **ainda não existe**: as conferências à mão da fase 6. Para o que existe, veja os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [Lembretes](reminders.md) · [Tarefas](tasks.md) · [Calendário e Eventos](calendar-and-events.md) ·
> [Alertas e Sweep](alerts-and-sweep.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Channels](../../channels/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## Fase 6 — Sincronização com o Google Tasks *(construída em 2026-11-13; faltam as conferências à mão)*

A sincronização nos dois sentidos com listas do Google Tasks está construída: ver
[Tarefas §6](tasks.md#6-google-tasks). Ela repete o padrão dos calendários (vínculo, link, marca de
pendente, log de conflitos, o mesmo job) em tabelas próprias (`agd012`, `agd014`); a `agd011` passou a
registrar tarefas também (`subject_type`).

Diferente do primeiro desenho: sem `ITaskSyncProvider` (um provedor só, então nenhuma interface); o
Google Tasks não tem sync token, então o cursor é o `updatedMin`; ele não aceita `If-Match`, então a
atualização lê a tarefa antes; quem escolhe os ids é o Google, então um insert cuja resposta se perde
pode duplicar uma tarefa.

O Google Agenda (fase 5) foi conferido à mão com uma conta real em 2026-11-13.

### Falta conferir à mão

Contra uma conta Google real, depois de reconectá-la para conceder o `auth/tasks` (e com a Google Tasks
API ativada no projeto do Cloud):

- vincular listas, e as tarefas delas (com subtarefas e concluídas) aparecendo em até um ciclo;
- uma tarefa criada, editada, concluída e excluída no Google aparecendo no Pandora, e o mesmo do Pandora
  para o Google;
- a hora do prazo e a prioridade postas no Pandora sobrevivendo a uma edição do título no Google;
- concluir uma tarefa recorrente, aqui e no Google, e a próxima instância chegando ao Google;
- a mesma tarefa editada dos dois lados entre dois ciclos, terminando com a versão mais recente e uma
  linha na lista de conflitos.

### Fora do escopo

Convidados e RSVP, anexos, cores por evento, publicar no Google um calendário ou uma lista nascidos no
Pandora, notificações push do Google, mover tarefas entre listas ou para outra tarefa-mãe, e a ordem das
tarefas.

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
