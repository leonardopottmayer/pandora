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
