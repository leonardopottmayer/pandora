# Módulo Agenda — Plano de Produto

> **Status:** Plano. Nada neste documento está implementado ainda.
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Channels](../../channels/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) ·
> [Assistant](../../assistant/pt-BR/product-plan.md)

---

## 1. O que o módulo faz

**Agenda** é a camada de tempo do Pandora: tudo que o usuário precisa *estar*, *fazer* ou *ser
lembrado*. Um módulo, três agregados distintos:

- **Eventos** — o calendário pessoal. Múltiplos calendários nomeados, eventos com horário e de dia
  inteiro, recorrência, UI mês/semana/dia/agenda. Sincronizado nos dois sentidos com o Google
  Calendar.
- **Tarefas** — coisas com workflow. Listas, subtarefas, prioridade, prazo, `concluída`.
  Sincronizadas nos dois sentidos com o Google Tasks.
- **Lembretes** — coisas sem workflow. "Me avisa às 14:00." Dispara, você reconhece ou adia, acabou.
  Semântica do Apple Reminders.

Os três podem levantar **alertas**, que o módulo entrega pelo módulo
[Channels](../../channels/pt-BR/product-plan.md) via **email**, **Telegram** ou ambos,
conforme a configuração do usuário. Alertas no Telegram levam botões inline (*Concluir*, *Adiar 1h*)
que agem de volta sobre o item.

### Por que um módulo e não três

Eventos, tarefas e lembretes compartilham coisa demais para justificar módulos separados: o motor de
recorrência, o modelo de alerta, o job de varredura de vencimentos, a maquinaria de sync e —
principalmente — o usuário lê os três numa **única tela** ("como está meu dia"). Três módulos
significariam três cópias da expansão de RRULE e um join entre módulos para a leitura principal.

Ainda assim são três **agregados**, com tabelas, invariantes e endpoints próprios. Se Tasks um dia
crescer além do módulo, sai daqui com suas tabelas intactas.

---

## 2. Nomenclatura e coordenadas

| Item | Valor |
|---|---|
| Projetos backend | `Pottmayer.Pandora.Modules.Agenda.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `agenda` |
| Prefixo de tabela | `agdXXX_`, PK `uuid_generate_v7()` |
| Base da API | `/api/v{version}/agenda` |
| Frontend | `client-web/src/modules/agenda` |
| Migrations | `migrations/migrations/agenda/` |

---

## 3. Princípios

1. **O alerta é a única primitiva de agendamento.** Eventos, tarefas e lembretes não criam cada um
   sua lógica de notificação. Todo "me avise sobre isso no instante T" é uma linha `Alert`, e um único
   job de varredura escaneia todas. *(D1)*
2. **Ocorrências são calculadas, nunca armazenadas.** Um evento recorrente é uma linha mais um RRULE.
   As leituras expandem em memória para a janela pedida. Só *desvios* da regra viram linha.
   Materializar um ano de ocorrências transformaria toda edição numa migração. *(D2)*
3. **O job de agendamento vive aqui, não no Channels — nem no broker.** A Agenda decide *quando*; o
   Channels só sabe *enviar agora*. Reagendar ou concluir um item antes de disparar é um update
   local, sem nada para cancelar a jusante. Mensagem atrasada em fila (`x-delayed-message`, TTL +
   DLX) não pode ser cancelada nem reagendada, e lembrete é exatamente a coisa que muda de horário —
   ver o [doc de mensageria](../../../architecture/pt-BR/messaging.md#6-o-que-não-vai-no-broker).
   *(D3)*
4. **Tempo é gravado absoluto, exibido local e recorrido no fuso do usuário.** `timestamptz` em todo
   lugar, mais um fuso IANA no item, porque "toda segunda às 09:00" precisa sobreviver ao horário de
   verão. *(D4)*
5. **O calendário externo é um par, não um dono.** O Pandora tem seu próprio modelo; o sync reconcilia
   dois repositórios independentes. Quem nunca conectar o Google não perde nada. *(D5)*
6. **Tudo é comandável.** Toda ação do usuário existe como um comando de aplicação com um objeto de
   parâmetros explícito, para que o [Assistant](../../assistant/pt-BR/product-plan.md) possa invocá-la
   sem HTTP e sem um caminho de código paralelo. *(D6)*

---

## 4. Linguagem ubíqua

| Termo | Significado |
|---|---|
| **Calendário** | Container nomeado e colorido de eventos. Todo usuário tem ao menos um (`default`). Pode estar vinculado a um calendário remoto. |
| **Evento** | Algo que ocupa tempo: início, fim, dia inteiro ou com horário, recorrência e local opcionais. |
| **Ocorrência** | Uma materialização de um evento recorrente no tempo. Calculada a partir do RRULE na leitura. |
| **Override** | Um desvio armazenado para uma única ocorrência: cancelada ("essa terça não tem") ou editada ("essa mudou para 15:00"). |
| **Lista de tarefas** | Container nomeado de tarefas (a *lista* do Apple Reminders, o *projeto* do Todoist). |
| **Tarefa** | Algo a fazer: status, prioridade, prazo opcional, subtarefas opcionais, recorrência opcional. |
| **Lembrete** | Um ping num instante. Sem workflow — dispara e então é reconhecido, adiado ou cancelado. |
| **Alerta** | "Me notifique sobre *assunto* com *este offset*, por *estes canais*." Polimórfico sobre evento, tarefa e lembrete. |
| **Dispatch** | O registro de que um alerta, para uma ocorrência, foi entregue ao Channels. A chave de idempotência da varredura. |
| **Varredura (sweep)** | A passada em background que acha alertas vencendo dentro de uma janela de antecipação e os despacha. |
| **Vínculo de calendário** | O pareamento declarado de um calendário local com um remoto, mais a direção. |
| **Sync link** | A linha que mapeia uma entidade local à sua contraparte remota (`remote_id`, `etag`, hashes). Evita duplicatas e ecos. |
| **Eco** | Uma mudança que o próprio Pandora empurrou, voltando no próximo pull. Suprimida comparando hashes no sync link. |

---

## 5. Modelo de domínio

### 5.1 Agregados

```
Calendar (agd001)
└── Event (agd002)                    ← raiz de agregado, referencia calendar_id
    └── EventOccurrenceOverride (agd003)   ← entidade filha

TaskList (agd004)
└── Task (agd005)                     ← raiz de agregado; auto-referência para subtarefas

Reminder (agd006)                     ← raiz de agregado, independente

Alert (agd007)                        ← filho polimórfico de Event | Task | Reminder
└── AlertDispatch (agd008)            ← livro-razão de idempotência

CalendarBinding (agd009) · SyncLink (agd010) · SyncCursor (agd011) · SyncConflict (agd012)
```

### 5.2 Catálogo de schema

Toda tabela carrega `user_id`, o quarteto de auditoria (`created_by/at`, `updated_by/at`) via
`IAuditable`, e é escopada ao usuário do token em toda leitura — mesmas regras de Notes e Finances.

**`agd001_calendar`**

| Coluna | Observações |
|---|---|
| `name`, `color`, `icon` | Exibição. |
| `is_default` | Exatamente um por usuário; garantido por índice único parcial. |
| `is_visible` | Toggle de UI; não afeta alertas. |
| `time_zone` | IANA. Default: a preferência do usuário. |
| `origin` | `local` \| `external`. Um calendário `external` nasceu de um pull e é majoritariamente somente-leitura. |
| `archived_at` | Ocultação suave. |

**`agd002_event`**

| Coluna | Observações |
|---|---|
| `calendar_id` | FK. Mover evento entre calendários é permitido. |
| `title`, `description`, `location`, `url` | `url` guarda o link da reunião. |
| `starts_at`, `ends_at` | `timestamptz`. Em dia inteiro, meia-noite no `time_zone`, fim exclusivo. |
| `is_all_day` | Dirige a renderização e o mapeamento no sync (`date` vs `dateTime`). |
| `time_zone` | IANA, por evento — a recorrência é expandida neste fuso. |
| `rrule` | String `RRULE` (RFC 5545) anulável. Nulo ⇒ ocorrência única. |
| `recurrence_ends_at` | Limite desnormalizado (`UNTIL`/`COUNT` calculado), para que consultas por intervalo possam podar por índice em vez de expandir toda linha recorrente. |
| `status` | `confirmed` \| `tentative` \| `cancelled`. |
| `transparency` | `busy` \| `free`. Mantido porque o Google faz round-trip disso. |
| `deleted_at` | Delete suave (um sync de entrada pode ressuscitar). |

**`agd003_event_occurrence_override`**

| Coluna | Observações |
|---|---|
| `event_id`, `original_starts_at` | Chave natural composta — identifica *qual* ocorrência. |
| `is_cancelled` | O caso `EXDATE`. |
| `starts_at`, `ends_at`, `title`, `description`, `location` | Anuláveis; colunas não-nulas sobrescrevem a série apenas nessa ocorrência. |

**`agd004_task_list`** — `name`, `color`, `icon`, `is_default`, `position`, `origin`, `archived_at`.

**`agd005_task`**

| Coluna | Observações |
|---|---|
| `list_id`, `parent_task_id` | Subtarefas são tarefas. Profundidade limitada a 1 nível no MVP (limite documentado, garantido no agregado). |
| `title`, `notes` | |
| `due_at`, `due_has_time` | Uma tarefa "para amanhã" não vence às 00:00 — o flag dirige a renderização e o offset padrão do alerta. |
| `priority` | `none` \| `low` \| `medium` \| `high`. |
| `status` | `todo` \| `in_progress` \| `done` \| `cancelled`. |
| `completed_at` | Setado em `done`, limpo ao reabrir. |
| `rrule` | Tarefa recorrente. Ao concluir, o agregado gera a próxima instância (ver §5.4). |
| `position` | Ordenação manual dentro da lista. |
| `deleted_at` | Delete suave. |

**`agd006_reminder`**

| Coluna | Observações |
|---|---|
| `title`, `notes` | |
| `remind_at` | Obrigatório. O instante em que dispara. |
| `time_zone`, `rrule` | Lembretes recorrentes ("todo dia útil às 08:00"). |
| `status` | `scheduled` \| `notified` \| `acknowledged` \| `snoozed` \| `cancelled`. |
| `snoozed_until` | Setado pelo *Adiar*; a varredura trata como o `remind_at` efetivo. |
| `acknowledged_at` | |

**`agd007_alert`**

| Coluna | Observações |
|---|---|
| `subject_type`, `subject_id` | `event` \| `task` \| `reminder`. Sem FK — polimórfico, validado na camada de aplicação, removido junto com o assunto. |
| `offset_minutes` | Com sinal, relativo à âncora do assunto (início do evento / prazo da tarefa / hora do lembrete). `0` = no instante. `-15` = quinze minutos antes. |
| `channels` | `null` ⇒ usa a preferência do usuário para a categoria, no Channels. Senão, array explícito (`email`, `telegram`). |
| `is_enabled` | |

Um `Reminder` nasce com um alerta em `offset_minutes = 0`. Eventos e tarefas usam por padrão a
antecedência preferida do usuário (uma preferência, não um 15 chumbado no código).

**`agd008_alert_dispatch`**

| Coluna | Observações |
|---|---|
| `alert_id`, `occurrence_starts_at` | Únicos em conjunto. É o que torna a varredura idempotente entre reinícios, desvio de relógio e janelas sobrepostas. |
| `dispatched_at`, `correlation_id` | O `correlation_id` é o mesmo valor entregue ao Channels, então uma entrega é rastreável ponta a ponta. |
| `channels_resolved` | O que foi de fato pedido, depois de aplicar defaults do usuário e horário de silêncio. |

**`agd009_calendar_binding`** — `calendar_id`, `external_account_id` (Integrations),
`remote_calendar_id`, `direction` (`bidirectional` \| `pull_only` \| `push_only`), `is_enabled`,
`last_synced_at`. A mesma forma é reusada para listas de tarefas (discriminador `subject_type`), então
o Google Tasks não precisa de uma segunda tabela.

**`agd010_sync_link`** — `provider`, `external_account_id`, `local_kind` (`event` \| `task` \|
`calendar` \| `task_list`), `local_id`, `remote_id`, `remote_etag`, `remote_updated_at`, `local_hash`,
`last_synced_at`. Único em `(provider, external_account_id, remote_id)` **e** em
`(local_kind, local_id, external_account_id)`.

**`agd011_sync_cursor`** — `external_account_id`, `remote_calendar_id`, `sync_token`,
`last_full_sync_at`, `consecutive_failures`. Um `410 Gone` do Google limpa o token e força resync
completo.

**`agd012_sync_conflict`** — log append-only das resoluções por last-write-wins: o que foi
sobrescrito, qual lado venceu, o payload descartado em JSON. Nunca vira uma fila para agir; existe
para que "o Google comeu minha edição" tenha resposta.

### 5.3 Recorrência

Um subset pragmático do RFC 5545, guardado como a string `RRULE` crua, para que o sync com o Google
seja uma cópia e não uma tradução com perda.

**Suportado:** `FREQ=DAILY|WEEKLY|MONTHLY|YEARLY`, `INTERVAL`, `BYDAY` (incluindo ordinais como `2TU`,
`-1FR`), `BYMONTHDAY`, `BYMONTH`, `COUNT`, `UNTIL`, `WKST`.

**Não suportado (rejeitado na escrita, preservado somente-leitura no sync de entrada):** `BYSETPOS`,
`BYWEEKNO`, `BYYEARDAY`, `BYHOUR`/`BYMINUTE`/`BYSECOND`.

Um evento de entrada cuja regra usa uma parte não suportada é guardado literalmente, marcado como
`recurrence_unsupported`, renderizado somente-leitura e **nunca empurrado de volta** — o Pandora não
pode rebaixar uma regra que não sabe representar.

A expansão vive em `Domain/Recurrence/`: `RecurrenceRule.Parse(string)` e
`Expand(DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone)`. É função pura, ciente de horário
de verão (expande no relógio de parede local e depois converte de volta) e coberta por uma suíte de
testes tabelada antes que qualquer outra coisa do módulo dependa dela.

> **Duplicação deliberada:** o Finances já tem seu próprio motor de recorrência, mais simples e
> não-RRULE. Eles ficam separados. Unificar é um refactor futuro possível, não um pré-requisito — e se
> acontecer, o motor RRULE é o sobrevivente e vai para o Tars.

### 5.4 Comportamentos que merecem nome

- **Concluir uma tarefa recorrente** fecha a instância atual (`done`, `completed_at`) e cria a próxima
  a partir do RRULE, carregando notas, prioridade, lista e alertas. Duas linhas, não uma linha mutável,
  para que o histórico sobreviva.
- **Adiar um lembrete** seta `snoozed_until` e `status = snoozed`. A varredura lê
  `COALESCE(snoozed_until, remind_at)`. Um adiamento nunca cria um novo dispatch para a ocorrência
  original — cria um para o horário adiado, mantendo o livro-razão honesto.
- **Editar uma ocorrência** de uma série grava um override. Editar "esta e as futuras" divide a série:
  a original ganha `UNTIL` no ponto de corte e um novo evento começa dali (a abordagem padrão do
  iCalendar, e o que o Google espera).
- **Excluir um calendário** é recusado enquanto houver eventos vivos, espelhando os guards de exclusão
  do Finances. Arquive em vez disso.

---

## 6. A varredura de alertas

Um `AlertSweepBackgroundService`, modelado sobre o `NotificationDispatcherBackgroundService`
existente, rodando em intervalo curto (padrão 60s).

```
a cada tick:
  janela = [agora - graça, agora + antecipação]   # graça cobre indisponibilidade; antecipação é 0 por padrão
  para cada alerta habilitado cujo assunto esteja vivo:
      âncoras = expandir(assunto, janela)         # 1 âncora se não recorrente, N se recorrente
      para cada âncora:
          disparo = âncora + offset_minutes
          se disparo fora da janela: continue
          se existe dispatch para (alert_id, âncora): continue    # idempotente
          canais = resolver(alert.channels, defaults do usuário, horário de silêncio)
          se canais vazio: registra dispatch como suprimido; continue
          publica NotifyUserRequested(correlation_id, user_id, categoria, template,
                                     payload, botões)
          insere linha de dispatch
```

Tudo acontece numa unidade de trabalho por alerta, então um crash no meio do tick reexecuta limpo no
próximo. A `graça` (padrão 15 minutos) faz com que um notebook que estava suspenso ainda entregue o
lembrete perdido, uma vez, marcado como atrasado — em vez de engolir em silêncio ou inundar ao acordar.

A varredura publica **`NotifyUserRequested`** (contrato do
[Channels](../../channels/pt-BR/product-plan.md#81-entrada-de-trabalho)) com a categoria, a chave de
template, o payload e — o que só a Agenda sabe — os **botões**: `(owner_module: "agenda", action,
payload)`. O `correlation_id` é o id do dispatch, então a entrega é rastreável ponta a ponta.

A Agenda não escolhe canal, endereço nem texto: a categoria (`agenda.reminder`, `agenda.task`,
`agenda.event`) resolve os canais pelas preferências do usuário, e as variantes de template por canal
vivem no Channels. O que a Agenda declara é *o que pode ser feito com a mensagem*, porque isso é
domínio dela.

> Os subscribers de `identity.*` no Channels seguem o caminho antigo — o produtor publica um fato e o
> Channels mapeia para template. Os dois caminhos coexistem, e a regra de qual usar é simples: **quem
> é dono dos botões é dono do `NotifyUserRequested`**. Notificação de segurança não tem botão.

---

## 7. Ações de entrada pelo Telegram

Quando o Channels envia uma mensagem com botões, ele registra cada botão em `chn003_interaction` com
o `owner_module` que a Agenda declarou. No clique, ele resolve esse id e publica com a chave de
roteamento **`inbound.interaction.agenda.<ação>`** — que só a fila da Agenda consome. Não há
broadcast e a Agenda não filtra eventos alheios.

O contrato recebido é `InboundInteractionReceived(userId, channel, ownerModule, action, payload,
sourceCorrelationId)`. A Agenda assina e mapeia:

| Ação | Efeito |
|---|---|
| `task_done` | Tarefa → `done`. Lembrete → `acknowledged`. |
| `snooze_10m` / `snooze_1h` / `snooze_tomorrow` | Lembrete → `snoozed` com o novo horário. |
| **Abrir** | Deep link para o client web — botão de URL, não gera interação nem evento. |

Três garantias vêm do Channels e não precisam ser reimplementadas aqui:

- **Autenticidade.** O `user_id` vem da linha de interação, não do cliente. A Agenda resolve o
  assunto pelo `payload` que ela mesma gravou na ida.
- **Uso único e expiração.** Um botão de ontem não age hoje, e um duplo clique chega uma vez só.
- **Correlação.** `sourceCorrelationId` amarra o clique ao dispatch que o gerou.

Ainda assim, agir sobre um item já tratado é um no-op com resposta amigável, não um erro — a
idempotência de domínio continua sendo responsabilidade da Agenda.

---

## 8. Sync com provedores externos

### 8.1 Forma

```
Agenda.Domain/Ports/Sync/
    ICalendarSyncProvider     ListChanged / Create / Update / Delete / ResolveCalendars
    ITaskSyncProvider         mesma forma para listas e tarefas

Agenda.Infrastructure/Sync/Google/
    GoogleCalendarSyncProvider     ← Calendar API v3, listagem incremental por syncToken
    GoogleTasksSyncProvider        ← Tasks API v1
```

Credenciais nunca moram aqui. O provider pede ao
[Integrations](../../integrations/pt-BR/product-plan.md) um access token válido via
`IExternalCredentialProvider`, que renova de forma transparente. Adicionar CalDAV ou Microsoft depois é uma
pasta nova sob `Sync/` mais o registro do provider — sem mudança no domínio da Agenda.

### 8.2 Pull

Um `SyncBackgroundService` roda por conta conectada num intervalo (padrão 5 min):

1. Lê o `sync_cursor` do calendário remoto vinculado; chama a listagem incremental do provider.
2. Para cada mudança remota, acha o `sync_link` pelo `remote_id`.
   - **Sem link** → cria local, grava o link.
   - **Link existe, `remote_etag` inalterado** → nada a fazer.
   - **Link existe, remoto mudou** → checagem de conflito (§8.4), depois aplica.
   - **Remoto excluído** → delete suave local, remove o link.
3. Guarda o novo `sync_token`. Um `410 Gone` limpa e agenda resync completo.

### 8.3 Push

Escritas locais empurram **imediatamente**, não no próximo tick — o usuário espera que um evento
criado no Pandora esteja no celular antes de largá-lo. O comando de aplicação commita a mudança local
e então enfileira um job de push; o job tem retry com backoff e é idempotente sobre o `sync_link`. Uma
falha de push nunca desfaz a escrita local; marca o link como `pending_push` e a varredura tenta de
novo.

### 8.4 Conflito: last write wins

Quando os dois lados mudaram desde `last_synced_at`, o `updated_at` mais recente vence, entidade
inteira. O perdedor é gravado em `agd012_sync_conflict` com o payload completo. Sem merge por campo —
com um único usuário humano, o custo de uma edição perdida rara é muito menor que o de um motor de
merge, e o log de conflitos torna isso recuperável na mão.

### 8.5 Supressão de eco

Toda escrita guarda `local_hash` (hash estável dos campos sincronizados) no link. No pull, se o
payload remoto recebido hashear para o `local_hash` guardado, a mudança é o eco do próprio Pandora e é
ignorada sem tocar em `updated_at`. Sem isso, dois sistemas com last-write-wins ficam em ping-pong para
sempre.

### 8.6 Não-objetivos explícitos

Participantes e convites, consulta de disponibilidade de terceiros, calendários compartilhados e
criação de Google Meet. O Pandora é single-user; um evento não tem lista de convidados. Eventos de
entrada com participantes preservam isso como JSON opaco, para que um round-trip não destrua o dado.

---

## 9. Superfície de API (esboço)

```
GET    /agenda/calendars                       POST /agenda/calendars
PATCH  /agenda/calendars/{id}                  DELETE /agenda/calendars/{id}

GET    /agenda/events?from=&to=&calendarIds=   → ocorrências expandidas, não linhas
POST   /agenda/events
PATCH  /agenda/events/{id}?scope=this|this-and-future|all
DELETE /agenda/events/{id}?scope=...

GET    /agenda/task-lists                      POST /agenda/task-lists
GET    /agenda/tasks?listId=&status=&due=      POST /agenda/tasks
PATCH  /agenda/tasks/{id}                      POST /agenda/tasks/{id}/complete
POST   /agenda/tasks/{id}/reopen               DELETE /agenda/tasks/{id}

GET    /agenda/reminders?status=&from=&to=     POST /agenda/reminders
POST   /agenda/reminders/{id}/acknowledge      POST /agenda/reminders/{id}/snooze
DELETE /agenda/reminders/{id}

POST   /agenda/{subjectType}/{id}/alerts       DELETE /agenda/alerts/{id}

GET    /agenda/today                           → visão unificada do dia: eventos + tarefas + lembretes
GET    /agenda/upcoming?days=7

GET    /agenda/sync/bindings                   POST /agenda/sync/bindings
POST   /agenda/sync/run                        → "sincronizar agora" manual
GET    /agenda/sync/conflicts
```

`GET /agenda/today` é a leitura principal do módulo e a consulta mais usada pelo Assistant. É um único
handler que expande ocorrências, mescla as três fontes e ordena por horário.

---

## 10. Frontend

`client-web/src/modules/agenda`, React + TanStack Query + antd, no padrão de Notes e Finances.

| Tela | Conteúdo |
|---|---|
| **Calendário** | Visões mês / semana / dia / agenda, arrastar para mover e redimensionar, clique-arraste para criar, toggles de visibilidade num sidebar. |
| **Tarefas** | Listas no sidebar; agrupadas por prazo (Atrasadas / Hoje / Esta semana / Depois / Sem data); concluir inline, expandir subtarefas, arrastar para reordenar. |
| **Lembretes** | Lista plana e cronológica, com adiar/reconhecer inline. |
| **Hoje** | A tela de entrada: o dia mesclado, de `GET /agenda/today`. |
| **Configurações** | Calendário e lista padrão, offsets padrão de alerta, preferências por canal, contas conectadas e vínculos de calendário, log de conflitos. |

A grade do calendário é a única peça sem precedente no codebase. Decisão adiada para a implementação,
mas o padrão é uma abordagem headless leve em vez de uma biblioteca pesada de calendário, para que a
semântica de recorrência continue sendo nossa.

---

## 11. Dependências de outros módulos

| Dependência | Por quê | Onde está planejado |
|---|---|---|
| **Identity — fuso do usuário** | Nada neste módulo é correto sem isso. `UserPreferences` hoje só tem `Theme` e `Language`; precisa de `TimeZone` (IANA), `DefaultAlertOffsetMinutes` e `WeekStartsOn`. | §12, Fase 0 |
| **Channels — multicanal** | Alertas precisam chegar no Telegram, endereços precisam ser por usuário e botões inline precisam voltar. | [Plano do Channels](../../channels/pt-BR/product-plan.md) |
| **Integrations — OAuth** | Tokens do Google, renovados de forma transparente. | [Plano do Integrations](../../integrations/pt-BR/product-plan.md) |
| **Assistant — catálogo de comandos** | A Agenda registra seus comandos; o LLM os chama. | [Plano do Assistant](../../assistant/pt-BR/product-plan.md) |

---

## 12. Roadmap

As fases são ordenadas para que algo útil chegue cedo e nada seja construído duas vezes.

### Fase 0 — Fundação *(bloqueante, quase toda fora deste módulo)*
- `UserPreferences`: adicionar `TimeZone` (IANA), `WeekStartsOn`, `DefaultAlertOffsetMinutes`;
  migration, DTO, endpoint e UI de configurações.
- Channels: renomeação, refactor multicanal, sender Telegram, vínculos de canal por usuário e entrada
  com tokens de interação. Ver o plano do Channels, fases C1–C4.
- Tars: `Communication.Telegram.Abstractions` + implementação sobre a Bot API.
- **Pronto quando:** um teste consegue enviar a mesma notificação por email e Telegram para um usuário
  vinculado.

### Fase 1 — Scaffold do módulo e Lembretes
- Sete projetos, schema `agenda`, DI, registro do módulo.
- `Reminder`, `Alert`, `AlertDispatch`; job de varredura; publicação de `NotifyUserRequested` com
  botões declarados, e as variantes de template no Channels.
- Fila `agenda.interactions` ligada a `inbound.interaction.agenda.#`; handlers de `task_done` e
  `snooze_*`.
- Endpoints de CRUD, reconhecer, adiar; botões inline ponta a ponta.
- Frontend: tela de Lembretes + configurações.
- **Pronto quando:** um lembrete criado no navegador vibra o celular no minuto certo, e *Adiar 1h* pelo
  Telegram move o lembrete.

### Fase 2 — Motor de recorrência
- `RecurrenceRule` parse e expand, testes tabelados incluindo fronteiras de horário de verão e ordinais
  no estilo `-1FR`.
- Lembretes recorrentes; idempotência de dispatch por ocorrência comprovada sob reinício.
- **Pronto quando:** "todo dia útil às 08:00" dispara exatamente uma vez por dia útil atravessando uma
  mudança de horário de verão.

### Fase 3 — Tarefas
- `TaskList`, `Task`, subtarefas, prioridade, prazo com/sem horário, concluir/reabrir, tarefas
  recorrentes.
- Alertas em tarefas; comportamento de atraso.
- Frontend: tela de Tarefas com agrupamento e conclusão inline.
- **Pronto quando:** uma tarefa semanal recorrente concluída hoje reaparece semana que vem com seus
  alertas.

### Fase 4 — Calendário e eventos
- `Calendar`, `Event`, overrides, os escopos de edição `esta / esta-e-futuras / todas`.
- Consulta por intervalo com expansão em memória; `GET /agenda/today`.
- Frontend: visões mês/semana/dia.
- **Pronto quando:** um evento recorrente editado com "esta e futuras" divide corretamente e a visão de
  dia concorda.

### Fase 5 — Sync com Google Calendar
- Módulo Integrations (ver o plano dele) entregando um access token vivo.
- `ICalendarSyncProvider` + implementação Google, cursores, links, push imediato, supressão de eco,
  last-write-wins com log de conflitos.
- Frontend: conectar conta, vincular calendários, sincronizar agora, lista de conflitos.
- **Pronto quando:** um evento criado de qualquer lado aparece no outro dentro de um ciclo de pull, e
  editar os dois lados ao mesmo tempo resolve de forma determinística com uma linha de conflito.

### Fase 6 — Sync com Google Tasks
- `ITaskSyncProvider` reusando a maquinaria de binding, link, cursor e conflito.
- **Pronto quando:** as mesmas garantias valem para listas e tarefas.

### Fase 7 — Superfície para o Assistant
- Registrar o catálogo de comandos: `create_reminder`, `create_task`, `create_event`, `complete_task`,
  `snooze_reminder`, `whats_my_day`.
- Contrato de resolução de datas relativas (o Assistant passa "agora" e o fuso; a Agenda não parseia
  linguagem).
- **Pronto quando:** "lembra de ligar pro dentista amanhã às 9" cria a linha certa a partir do Telegram.

### Além
Tags compartilhadas com Notes, anexar uma Note a um evento, quick-add em linguagem natural na web,
tempo de deslocamento, alertas por localização, import/export ICS, CalDAV, provedores Microsoft/Apple e
puxar vencimentos do Finances para a visão do dia.

---

## 13. Questões em aberto

1. **Biblioteca de calendário vs. grade própria.** Adiado para a Fase 4; não afeta nada antes disso.
2. **Profundidade de subtarefas.** Limitada a um nível no MVP. Aninhamento infinito é mais problema de
   UI que de modelo, e o próprio Google Tasks só suporta um nível — remover o limite quebraria a
   fidelidade do sync.
3. **Se o Finances migra para o motor RRULE.** Não é pré-requisito. Revisitar só se aparecer um terceiro
   consumidor de recorrência.
4. **Onde fica o horário de silêncio.** No Channels (ele é dono da política de entrega), reduzido a
   `suppress` \| `deliver_anyway` — adiar até de manhã é agendamento, e agendamento é daqui.
   Se a Agenda um dia precisar de "alertas urgentes furam o silêncio", o flag viaja no alerta e o
   Channels respeita.
