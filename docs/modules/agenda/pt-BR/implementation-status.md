# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído versus o que está desenhado mas ainda não implementado. O roadmap
adiante fica em [product-plan.md](product-plan.md).

---

## Implementado (fases 1–4 + frontend)

| Área | Notas |
|---|---|
| **Scaffold do módulo** | Sete projetos por camada; schema `agenda`; DI + registro do módulo. |
| **Lembretes** | `agd006` único-disparo + recorrente; ledger por ocorrência `agd006x`; reconhecer/soneca (série e por ocorrência); `ReminderSweepBackgroundService`. |
| **Motor de recorrência** | `RecurrenceRule` parse + `EventExpander` expand, subconjunto RFC 5545, ciente de DST, ordinais no estilo `-1FR`; expande no `time_zone` do item. |
| **Tarefas** | `agd004` listas + `agd005` tarefas; subtarefas de um nível; prioridade; vencimento com/sem hora; concluir/reabrir; recorrente materializada uma instância por vez carregando alertas. |
| **Alertas** | `agd007` polimórfico + ledger de dispatch `agd008`; sujeitos `Task` e `Event` ligados; `TaskAlertSweepBackgroundService` + `EventAlertSweepBackgroundService`. |
| **Calendário e eventos** | `agd001` calendários, `agd002` eventos (ocorrências calculadas), `agd003` overrides; escopos de edição esta / esta-e-futuras / todas; `EventAlertSweepBackgroundService`. |
| **Hoje** | Leitura unificada `GET /agenda/today` (eventos + tarefas + lembretes). |
| **Botões de entrada** | `InboundInteractionReceivedHandler` + `TaskInteractionHandler` para `task_done` / `snooze_*` do Channels. |
| **API** | Controllers de reminders, task-lists, tasks, calendars, events, alerts, today. |
| **Frontend** | `client-web/src/modules/agenda` — Hoje, Lembretes, Tarefas, Calendário. |

### Ajustes dos formulários e carregamento do frontend

Criação/edição de eventos, tarefas e lembretes usam modais de 640 px com rolagem interna.
Campos relacionados de tarefa ficam em pares no desktop e empilham no celular. Notas, detalhes
opcionais do evento e recorrência ficam recolhidos; detalhes e notas existentes abrem preenchidos.
Datas usam DD/MM/AAAA e horários HH:mm. A edição de evento valida calendário e período; falha na
consulta da série desabilita editar/excluir em vez de presumir o alcance da recorrência.

Término de recorrência usa opções de rádio que quebram linha; alcance da edição usa seletor
na largura disponível. Alertas usam lista nativa e controles com quebra, sem APIs descontinuadas
de List/Space do Ant Design. Adiar recalcula o horário personalizado ao abrir ou trocar o lembrete,
no fuso do usuário. Hoje, Calendário, Tarefas, Lembretes e Configurações distinguem falha de
consulta de resultado vazio. Contratos, regras de recorrência e conversões de fuso são preservados.


### Desvios notáveis do plano original

- **Três sweeps, não um.** `ReminderSweep`, `TaskAlertSweep`, `EventAlertSweep` substituem o único
  `AlertSweepBackgroundService` — cada tipo de sujeito expande diferente.
- **O ledger de dispatch de lembrete é `agd006x`** (escopo do lembrete), não o polimórfico `agd008`.
  Migra para `agd008` quando o Alert cobrir lembretes.
- **`Alert.subject_type` admite `Task`/`Event`/`Reminder`; `Task` e `Event` estão ligados** (criar/
  listar/sweep), **`Reminder` não está** — lembretes mantêm seu próprio ledger `agd006x`.
- **`time_zone` é carregado por linha** (lembrete/tarefa/evento/calendário) para a recorrência expandir
  no fuso do próprio item. Quando o caller não o informa, a Agenda agora usa como padrão o
  `UserPreferences` do Identity (via porta `IUserPreferencesReader`), caindo em UTC só quando o usuário
  não tem preferência. As forms do web também enviam a preferência salva, e o editor de alertas usa
  `DefaultAlertOffsetMinutes`, na `agd013_preferences` da própria Agenda, como offset padrão.
- **As visões de semana e dia são uma grade de tempo feita à mão** (`WeekDayGrid`), não uma
  biblioteca de calendário: grade de horas com empacotamento guloso em faixas para eventos que se
  sobrepõem, tira de dia-inteiro, indicador de "agora" e clique-para-criar. A visão de mês mantém o
  `<Calendar>` do antd. As três honram `WeekStartsOn` (a matemática da semana via um `startOfWeek`
  manual; o grid de mês via o `weekStart` do locale do dayjs).
- **Tela de Configurações da Agenda** (`AgendaSettingsPage`, `/agenda/settings`): edita o início da
  semana (preferência do Identity, mostrada aqui porque só o calendário a usa) e o offset de alerta
  padrão (`agd013_preferences`), mostra o fuso como herdado do Identity, mais um seletor de
  **calendário padrão**. Promover um calendário a padrão demove o anterior (o `UpdateCalendar` o
  limpa antes, para o índice único parcial nunca ver dois).

## Ainda não implementado (desenhado / planejado)

| Área | Status | Fase |
|---|---|---|
| **Sync Google Calendar** | `agd009`–`agd012` (binding/link/cursor/conflict), `ICalendarSyncProvider` + impl Google, push/supressão de eco, log de conflito — nada construído. Depende de [Integrations](../../integrations/pt-BR/overview.md) I1 (feito). | 5 |
| **Sync Google Tasks** | `ITaskSyncProvider` reusando a maquinaria de sync. | 6 |
| **Catálogo de comandos do Assistant** | Comandos são comandáveis (D6), mas o registro de descriptors (`create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `whats_my_day`) para o Assistant não está ligado. | 7 |
| **Além** | Links Nota↔evento, quick-add NL, tempo de deslocamento, alertas por local, ICS/CalDAV, provedores Microsoft/Apple, vencimentos do Finances na visão do dia. | — |

## Pontos em aberto conhecidos

1. **Biblioteca de UI de calendário vs. grade feita à mão** — **decidido: feita à mão**
   (`WeekDayGrid`), sem nova dependência. Revisitar só se quiser arrastar-para-mover/redimensionar ou
   barras de eventos multi-dia.
2. **Profundidade de subtarefa** limitada a um nível (casa com a fidelidade do Google Tasks).
3. **Se o Finances migra para o motor RRULE** — não é pré-requisito; reavaliar se surgir um terceiro
   consumidor de recorrência.

## B11: bloco de Agenda com dados atuais

Construído, sem commit. `/Agenda` insere rich block agenda-list. Origem padrão Hoje usa a visão
unificada do dia no fuso da conta (eventos, tarefas e lembretes, com estados atuais). Outro modo
seleciona lista ativa e mostra somente tarefas Todo/InProgress dela. Ambos somente leitura: sem
concluir tarefa, reconhecer lembrete ou editar evento pelo Notes. Abrir na Agenda leva à tela
correspondente.

Configuração salva apenas `{ mode: "today" }` ou `{ mode: "tasks", listId }`; props vazias usam
today. Títulos, nomes de listas, estados, datas e resultados consultados não entram no bloco,
autosave, texto de busca ou snapshots. Trocar modo limpa lista anterior. Lista não configurada,
excluída, alheia ou arquivada mostra estado explícito, sem carregar todas as tarefas como fallback.
Valida ID entre listas ativas do usuário antes de montar consulta de tarefas.

Reutiliza hooks, services e famílias de query keys da Agenda. Mutações normais da Agenda atualizam
blocos ativos pelo mesmo cache; Atualizar invalida explicitamente a origem escolhida. Mantém regras
normais de cache/foco, sem polling ou assinatura push. Consultar/atualizar não altera nota. Datas
exibidas usam fuso da conta e formatadores existentes; decidir o dia de hoje continua no servidor.
Falha da API esconde linhas antigas e permite tentar novamente.

Visão pública retorna Conteúdo privado antes de montar consultas, mesmo com dados do dono no cache.
Nota privada exige feature Agenda e permissões da origem: Hoje exige
agenda.events.read/tasks.read/reminders.read; lista exige agenda.tasks.read/lists.read. Nota
travada/em leitura pode consultar e atualizar, mas não configurar origem/lista. Exportações
Markdown/HTML emitem somente [Agenda], sem IDs ou valores consultados; PDF pelo navegador usa a
visão do dono renderizada naquele momento. IDs/configuração sobrevivem ao editor e versões.

Sem endpoint backend, esquema, migração ou dependência backend cruzada nova. Navegador/conta real
pendentes. Próximos eventos e resumos de Finanças construídos nas fatias seguintes. Blocos Radar/Databases/Projects dependem dos respectivos módulos.

## B11: próximos eventos da Agenda

Construído, sem commit. O bloco /Agenda oferece Próximos eventos. Configuração: { mode: "events",
days: 7 | 30, calendarId?: string }; days ausente/não suportado usa 7. Sem calendarId, considera
todos os calendários ativos, independentemente da visibilidade na tela Calendário. ID configurado
deve estar na lista ativa do dono. Calendário ausente/arquivado/de outro dono fica indisponível,
sem cair em todos. Lista ativa vazia não consulta eventos.

Janela cobre hoje mais as próximas 6 ou 29 datas no fuso da conta. Cada meia-noite local é
convertida separadamente para UTC, respeitando horário de verão. Fim exclusivo: meia-noite
seguinte à última data. Mostra eventos em andamento e futuros em ordem de início; oculta
encerrados/cancelados e início no/após limite final. Eventos de dia inteiro/vários dias usam
formatador existente da Agenda, incluindo fim exclusivo. Janela resolve ao renderizar/reabrir e
Atualizar; sem polling ou timer de meia-noite.

Reutiliza useCalendars/useEventOccurrences e query keys da Agenda. Endpoint existente expande
recorrências e aplica edições/cancelamentos de ocorrência; Notes não reimplementa recorrência nem
consulta detalhe de série. Filtro envia chaves calendarIds repetidas para binding ASP.NET.
Mutações normais de eventos/calendários invalidam blocos ativos; Atualizar recarrega ambas as
famílias. Erro esconde resultados antigos e permite tentar novamente.

Exige feature Agenda, agenda.events.read e agenda.calendars.read. Nota travada/em leitura pode
consultar/atualizar, mas não configurar. Trocar origem limpa filtros. Persiste apenas dias/ID;
nomes, ocorrências e datas resolvidas não entram em autosave, histórico ou templates. Página
pública bloqueia consultas mesmo com cache do dono. Markdown/HTML mantêm [Agenda]; PDF usa a visão
do dono renderizada. Sem mudança backend, esquema ou migração.

Verificação: 909 testes frontend passaram, incluindo 16 casos novos para limites no fuso/horário
de verão, ocorrências expandidas, eventos em andamento/dia inteiro, fim exclusivo,
acesso/privacidade, configuração e invalidação por mutação. Build/typecheck, ESLint Notes e
service Agenda alterado, e antd lint Notes passaram. Backend sem alteração; suítes não rerodadas.
Navegador/conta real pendentes. Sem commit ou alteração no banco local.
