# Calendário e Eventos

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Alertas e Sweep](alerts-and-sweep.md)

---

Um **calendário** (`agd001`) é um container nomeado e colorido de eventos; um usuário tem ao menos um
`default`. Um **evento** (`agd002`) ocupa tempo — com horário ou dia inteiro, único ou recorrente.

## 1. Ocorrências são calculadas, nunca armazenadas (D2)

Um evento recorrente é **uma linha mais uma RRULE**. Leituras a expandem em memória para a janela
pedida (`EventExpander`); nada materializa um ano de linhas. Só *desvios* da regra ficam armazenados,
como overrides. `recurrence_ends_at` (denormalizado de UNTIL/COUNT) deixa uma query de intervalo podar
séries terminadas por índice em vez de expandir toda linha.

- `starts_at` / `ends_at` são `timestamptz`; para um evento de dia inteiro são meia-noite no `time_zone`
  do evento, fim **exclusivo**.
- A recorrência expande no `time_zone` IANA do próprio evento (D4).
- `deleted_at` é um soft delete, então um futuro sync de entrada pode ressuscitar o evento.

## 2. Overrides (`agd003`)

Uma única ocorrência pode desviar de sua série, com chave `(event_id, original_starts_at)`:

- **Cancelada** (`is_cancelled`) — o caso EXDATE: a ocorrência some da grade.
- **Editada** — as colunas de override não-nulas (`starts_at`, `ends_at`, `title`, `description`,
  `location`) substituem os valores da série naquela ocorrência; colunas NULL caem de volta para a
  série.

## 3. Escopos de edição: esta / esta-e-futuras / todas

Editar um evento recorrente oferece três escopos:

| Escopo | Efeito |
|---|---|
| **esta** | Escreve um override `agd003` para a única ocorrência. |
| **esta e futuras** | **Divide** a série: a recorrência do evento original é limitada, e uma **nova linha `agd002`** leva a regra alterada adiante. Nenhum override é escrito. |
| **todas** | Edita a linha da série `agd002` diretamente. |

É por isso que "um evento recorrente editado *esta e futuras* divide corretamente e a visão do dia
concorda."

## 4. Hoje

`GET /agenda/today` é a leitura unificada que responde "como está meu dia" — a única tela que
justifica um módulo. Compõe os eventos do dia (expandidos), tarefas devidas e lembretes numa resposta
(`GetToday`).

## 5. Comandos e endpoints

Calendários: `CreateCalendar`, `UpdateCalendar`, `DeleteCalendar` (apagar um calendário não vazio é
recusado — arquive). Eventos: `CreateEvent`, `UpdateEvent` (com escopo de edição), `DeleteEvent`. HTTP:
ver [Referência de API](api-reference.md) — `/agenda/calendars`, `/agenda/events` (o endpoint de lista é
a query de intervalo com expansão em memória), `/agenda/today`.

## 6. Google Agenda

O usuário escolhe uma conta Google e alguns calendários dela nas configurações do Agenda; cada um vira um
calendário próprio no Pandora (`origin = External`), com o nome e a cor do Google, no fuso dele. Um
**vínculo** (`agd009`) liga os dois e guarda o sync token do Google; um **link** (`agd010`) liga cada
evento do Pandora ao evento do Google. Um calendário que a conta tem ou pode editar (`accessRole`
`owner` ou `writer`) é vinculado como `read-write`: as edições vão nos dois sentidos. Um que ela só pode
ver é vinculado como `read`.

**Ciclo.** O `GoogleCalendarSyncBackgroundService` roda um ciclo por vínculo em `ok` a cada
`Pandora:Agenda:GoogleSyncInterval` (2 minutos); "Sincronizar agora" roda um na hora. O
`GoogleCalendarSync` primeiro manda as edições do Pandora (vínculo `read-write`, ver **Escrita** abaixo)
e depois:

1. lê a entrada do calendário (fuso e lembretes padrão) e os eventos, pelo `IGoogleCalendarClient`
   (REST, com o token da conta vindo do Integrations);
2. lista tudo de um ano para trás quando o vínculo não tem token, depois de um 410 do Google, e a cada
   30 dias; nos outros casos, só as mudanças desde o token, cancelamentos incluídos;
3. busca, para cada série que o motor de recorrência do Pandora não lê (`BYSETPOS`, `RDATE`…), as
   instâncias dos próximos 12 meses;
4. aplica tudo numa unit of work só, e guarda o token novo.

**Mapeamento** (`GoogleEventMapper`, uma função pura testada contra JSON do Google):

| Google | Pandora |
|---|---|
| `summary`, `description`, `location` | título (`(sem título)` quando vazio), descrição, local |
| `hangoutLink` ou o ponto de entrada de vídeo | `url` |
| `start`/`end` com `dateTime`, ou `date` no dia inteiro | instantes; dia inteiro vai da meia-noite à meia-noite seguinte (exclusiva) no fuso do calendário |
| `status` `tentative` / `cancelled` | `Tentative` / exclusão soft |
| Linha `RRULE:` | `rrule` |
| Linhas `EXDATE` | overrides cancelados (`agd003`) |
| Uma exceção (`recurringEventId` + `originalStartTime`) | um override daquela ocorrência (editado ou cancelado) |
| `reminders.overrides`, ou os `defaultReminders` do calendário | alertas (`agd007`), `offset = -minutos` |

Uma série que o Pandora não expande chega como eventos únicos, um por instância, cada um ligado ao id da
série (`series_remote_id`). Os alertas só são reescritos quando os lembretes do evento mudam no Google
(comparados por hash), então um alerta que o usuário tira aqui continua tirado até lá.

Uma mudança do Google num evento cujo link ainda está pendente fica para o envio resolver (ver
conflitos abaixo), e uma mudança com o mesmo `etag` do link é a do próprio Pandora voltando: nenhuma
das duas sobrescreve nada.

**Escrita** (vínculos `read-write`, `GoogleCalendarPush`). Toda escrita de evento num calendário
vinculado — criar, editar em qualquer escopo, excluir, pôr ou tirar um alerta — marca o link do evento
como pendente (`pending_push_at`) na mesma transação; um evento novo ganha o link ali, com um id do
Google escolhido pelo Pandora, então um insert repetido acha o próprio evento em vez de duplicar. O
ciclo seguinte manda cada evento pendente:

| Pandora | Google |
|---|---|
| um evento novo | `insert` com o id do Pandora |
| um evento editado | `patch` com `If-Match` no último etag: só os campos que o Pandora tem, então convidados e a reunião ficam |
| um evento excluído | `delete` (já sumido conta como feito) |
| "esta e as próximas" | a série original com o `UNTIL` no patch, a cauda inserida |
| uma ocorrência editada ou cancelada | patch ou delete da instância `<id da série>_<início original>` |
| alertas | lembretes `popup`: os cinco mais próximos antes do início, até quatro semanas |

O etag da resposta vai para o link, e é assim que a leitura reconhece a mudança. O que falha continua
pendente para o próximo ciclo, e o vínculo mostra a mensagem sem parar.

**Conflitos.** O 412 do Google num patch quer dizer que o evento mudou lá também. Vence a edição mais
recente: o `updated` do Google contra a hora da última edição no Pandora. Se o Pandora vence, o patch é
refeito sem `If-Match`; se o Google vence, o cursor é zerado, então o mesmo ciclo lista tudo e traz a
versão do Google por cima da do Pandora. A versão perdedora vai para a `agd011` em JSON e para a lista
de conflitos nas configurações.

**O que não muda.** Um evento não entra nem sai de um calendário do Google
(`Agenda.EventMoveAcrossGoogle`). Num vínculo `read`, criar, editar ou excluir um evento responde
`Agenda.CalendarReadOnly`, e as telas apontam para o Google. Um calendário vinculado não pode ser
excluído (`Agenda.CalendarBound`); desvincule antes.

**Falhas.** Um token recusado, que não renova, ou sem escopo de calendário deixa o vínculo em
`needs-reconnect`; qualquer outra falha conta, e cinco seguidas o deixam em `error`. Nenhum dos dois roda
de novo até "Sincronizar agora". O `ExternalAccountRevoked` deixa os vínculos da conta em
`needs-reconnect` (o usuário é avisado pelo aviso de revogação do próprio Integrations); o
`ExternalAccountDisconnected` os desvincula.

**Desvincular** remove o vínculo e os links e devolve o calendário a calendário local: os eventos ficam e
passam a ser editáveis. Nada muda no Google. A tela de conexões lista cada vínculo como uso da conta
("Agenda · nome do calendário").
