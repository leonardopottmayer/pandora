# Módulo Agenda

> A camada de tempo do Pandora — tudo que o usuário precisa *estar*, *fazer* ou *ser lembrado* —
> dentro do monólito modular.
> **Idioma:** o inglês é a documentação primária. 🇺🇸 [English version](../README.md).

O módulo **Agenda** é um módulo com três agregados distintos:

- **Eventos** — um calendário pessoal: múltiplos calendários nomeados, eventos com horário e de dia
  inteiro, recorrência, uma UI mês/semana/dia/agenda.
- **Tarefas** — coisas com um fluxo: listas, subtarefas, prioridade, data de vencimento, `done`.
- **Lembretes** — coisas sem fluxo: "me avisa às 14:00"; dispara, você reconhece ou soneca, acabou.

Os três levantam **alertas**, entregues via [Channels](../../channels/README.md) por e-mail e/ou
Telegram, com botões inline (*Concluído*, *Soneca 1h*) que agem de volta no item.

Duas regras definem o módulo: **o alerta é o único primitivo de agendamento** (todo "me avise no
tempo T" é uma linha de alerta varrida por um sweep de background) e **ocorrências são calculadas,
nunca armazenadas** (uma série recorrente é uma linha mais uma RRULE, expandida na leitura; só
*desvios* ganham linhas).

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o panorama de negócio e o vocabulário, depois leia o tópico que precisar.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | Os três agregados, princípios, linguagem ubíqua, escopo |
| 2 | [Arquitetura](architecture.md) | Organização de projetos, agregados e objetos de valor, o motor de recorrência, os sweeps, decisões |
| 3 | [Modelo de dados](data-model.md) | Catálogo de schema (`agd001`–`agd008`, tabelas de sync reservadas) |
| 4 | [Lembretes](reminders.md) | Único-disparo vs. recorrente, o ledger de dispatch por ocorrência, reconhecer/soneca |
| 5 | [Tarefas](tasks.md) | Listas, subtarefas, prioridade, vencimentos, materialização de recorrência, concluir/reabrir, alertas |
| 6 | [Calendário e Eventos](calendar-and-events.md) | Calendários, ocorrências calculadas, overrides, escopos de edição esta/esta-e-futuras/todas, Hoje |
| 7 | [Alertas e Sweep](alerts-and-sweep.md) | O alerta polimórfico, os sweeps, idempotência de dispatch, grace/look-ahead, botões inline |
| 8 | [Referência de API](api-reference.md) | Todos os endpoints sob `/api/v{n}/agenda` |
| 9 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |

O roadmap adiante (sync Google e a superfície do Assistant) fica em [product-plan.md](product-plan.md).

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Agenda.*` (.NET 10, DDD, comandos/queries no estilo CQRS).
- **Schema:** schema PostgreSQL `agenda`, tabelas com prefixo `agdXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/agenda` (Hoje, Lembretes, Tarefas, Calendário).
- **Base da API:** `/api/v{version}/agenda`, autenticada e escopo do usuário do token.
- **Migrations:** `migrations/migrations/agenda/`.
- **Alertas** são entregues via [Channels](../../channels/README.md) (`NotifyUserRequested` com botões).
