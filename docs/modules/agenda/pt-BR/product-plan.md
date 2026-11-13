# Módulo Agenda — Roadmap (o que falta)

> **Status:** as fases **1–4** (lembretes, recorrência, tarefas, calendário/eventos), o frontend e as
> ferramentas do Assistant estão implementados, e a sincronização nos dois sentidos com o Google Agenda (fase 5). Este arquivo
> acompanha só o que **ainda não existe**: as conferências à mão da fase 5 e o Google Tasks. Para o que existe, veja os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [Lembretes](reminders.md) · [Tarefas](tasks.md) · [Calendário e Eventos](calendar-and-events.md) ·
> [Alertas e Sweep](alerts-and-sweep.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Channels](../../channels/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## Fase 5 — Sincronização com o Google Calendar *(construída em 2026-11-13; faltam as conferências à mão)*

A sincronização nos dois sentidos com calendários do Google está construída: ver
[Calendário e Eventos §6](calendar-and-events.md#6-google-agenda). As decisões que ela seguiu: o usuário
escolhe calendários do Google, e cada um vira um calendário próprio no Pandora; os lembretes do Google
viram alertas e os alertas viram lembretes popup; polling a cada 2 minutos com `syncToken` (notificações
push pedem HTTPS público); num conflito vence a edição mais recente, e a outra vai para uma lista de
conflitos.

Diferente do primeiro desenho: sem `local_hash` (a marca de pendente e o etag bastam); um evento não
entra nem sai de um calendário do Google; o envio roda no começo do ciclo seguinte, não logo depois do
commit; a direção de um calendário vem do acesso da conta a ele.

### Falta conferir à mão

Contra uma conta Google real:

- **Leitura:** vincular calendários; um evento criado, editado, movido e cancelado no Google aparecendo em
  até um ciclo; um evento recorrente com uma ocorrência alterada; um evento de dia inteiro; um lembrete do
  Google tocando pelo Pandora.
- **Escrita:** um evento criado, editado e excluído no Pandora aparecendo no Google em até um ciclo, com a
  recorrência e os lembretes; "esta ocorrência" e "esta e as próximas" numa série; o mesmo evento editado
  dos dois lados entre dois ciclos, terminando com a versão mais recente e uma linha na lista de
  conflitos; um calendário compartilhado só para leitura continuando só para leitura.

### Fora do escopo

Convidados e RSVP, anexos, cores por evento, publicar no Google um calendário nascido no Pandora,
notificações push do Google e o Google Tasks (fase 6).
## Fase 6 — Sincronização com o Google Tasks

- `ITaskSyncProvider` reaproveitando o vínculo, o link e o mecanismo de conflitos.
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
