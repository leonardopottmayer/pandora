# Módulo Agenda — Roadmap (o que falta)

> **Status:** as fases **1–4** (lembretes, recorrência, tarefas, calendário/eventos), o frontend e as
> ferramentas do Assistant estão implementados, e a sincronização Google → Pandora (5a). Este arquivo
> acompanha só o que **ainda não existe**: a sincronização Pandora → Google e o Google Tasks. Para o que existe, veja os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [Lembretes](reminders.md) · [Tarefas](tasks.md) · [Calendário e Eventos](calendar-and-events.md) ·
> [Alertas e Sweep](alerts-and-sweep.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Channels](../../channels/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## Fase 5 — Sincronização com o Google Calendar *(5a construída em 2026-11-09; 5b a seguir)*

Sincronização nos dois sentidos entre calendários do Pandora e do Google, em duas etapas. A **5a**
(Google → Pandora, somente leitura) está construída: ver
[Calendário e Eventos §6](calendar-and-events.md#6-google-agenda-google--pandora). A **5b** manda as
edições do Pandora para o Google.

### Decisões

| # | Decisão | Escolhido | Em vez de |
|---|---|---|---|
| G1 | Direção | Dois sentidos, em duas entregas: leitura primeiro, escrita depois | Os dois de uma vez; só leitura; só escrita |
| G2 | Quais calendários | O usuário escolhe calendários do Google; cada um vira um calendário próprio no Pandora (1:1), com nome e cor do Google, ligado àquela conta | Todos os calendários da conta; mesclar num calendário do Pandora que já existe |
| G3 | Alertas nos eventos do Google | Os lembretes do evento no Google viram alertas do Pandora | Sem alertas por padrão; o alerta padrão do Pandora em todo evento |
| G4 | Conflitos (5b) | Vence a edição mais recente, pelo horário de alteração; a versão perdedora fica numa lista de conflitos | O Google sempre vence; perguntar a cada conflito |
| G5 | Onde fica o motor | Dentro do Agenda: um cliente do Google atrás do `IGoogleCalendarClient`, um job de sincronização e uma cópia local, no formato da sincronização do Gmail no Communications | Um módulo de sincronização genérico; ler o Google ao vivo a cada tela |
| G6 | Atualização | Polling a cada 2 minutos com o `syncToken` do Google, mais "Sincronizar agora" | Notificações push do Google (exigem HTTPS público, que o homelab não tem) |

### 5b — Pandora → Google

- **Dados:** `agd009.direction` passa a `read-write` (a coluna existe, hoje sempre `read`); a `agd010`
  ganha `local_hash` e `pending_push_at`; nova `agd011_sync_conflict` (`event_id`, `binding_id`, qual
  lado perdeu, a versão perdedora em jsonb, `detected_at`).
- **A trava de somente leitura** (`ExternalCalendars`) deixa passar os eventos de um vínculo
  `read-write`.
- **Escritas locais** num calendário vinculado preenchem `pending_push_at` no link na mesma transação. O
  envio roda logo depois do commit; o que falhar o próximo ciclo tenta de novo. Criar um evento o
  insere no Google; excluir o exclui lá.
- **Supressão de eco:** o `etag` e o `local_hash` do link separam uma mudança que o Pandora mandou de uma
  mudança feita no Google.
- **Escopos de edição:** "esta ocorrência" altera a instância; "esta e futuras" encerra a série original
  com UNTIL e insere a nova, como o Pandora já faz localmente; "todas" altera a série.
- **Alertas** editados no Pandora vão para o Google como lembretes `popup`.
- **Conflito:** quando os dois lados mudaram desde a última sincronização, vence a alteração mais nova;
  a versão perdedora vai para a `agd011` e aparece numa lista de conflitos nas configurações.
- **Escopo:** escrever exige `calendar` ou `calendar.events`; o consentimento padrão do Google já pede
  `calendar`.
- **Testes:** envio, eco e conflito contra o Google falso do `GoogleCalendarSyncTests`.

**Pronto quando:** um evento criado ou editado no Pandora aparece no Google em um ciclo, com a
recorrência e os lembretes certos, e editar os dois lados ao mesmo tempo termina com a versão mais nova
e uma linha de conflito.

### Falta verificar à mão (5a)

Com uma conta Google real: vincular calendários, um evento criado, editado, movido e cancelado no
Google aparecendo em um ciclo, um evento recorrente com uma ocorrência alterada, um evento de dia
inteiro, e um lembrete do Google tocando pelo Pandora.

### Fora do escopo

Convidados e respostas de presença, anexos, cores por evento, publicar no Google um calendário que
nasceu no Pandora, notificações push do Google, e o Google Tasks (fase 6).
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
