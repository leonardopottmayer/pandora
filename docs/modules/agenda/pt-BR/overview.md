# Visão geral — Negócio e Princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

**Agenda** é a camada de tempo do Pandora: tudo que o usuário precisa *estar*, *fazer* ou *ser
lembrado*. Um módulo, três agregados distintos:

- **Eventos** — um calendário pessoal. Múltiplos calendários nomeados, eventos com horário e de dia
  inteiro, recorrência, uma UI mês/semana/dia/agenda.
- **Tarefas** — coisas com um fluxo. Listas, subtarefas, prioridade, data de vencimento, `done`.
- **Lembretes** — coisas sem fluxo. "Me avisa às 14:00." Dispara; você reconhece ou soneca; acabou.
  Semântica do Apple Reminders.

Os três podem levantar **alertas**, que o módulo entrega via [Channels](../../channels/pt-BR/overview.md)
por e-mail, Telegram, ou ambos, conforme a configuração do usuário. Alertas de Telegram carregam botões
inline (*Concluído*, *Soneca 1h*) que agem de volta no item.

### Por que um módulo e não três

Eventos, tarefas e lembretes compartilham demais para justificar módulos separados: o motor de
recorrência, o modelo de alerta, o sweep de vencimento e — crucialmente — o usuário os lê numa **única
tela** ("como está meu dia"). Três módulos significariam três cópias da expansão de RRULE e um join
entre módulos para a leitura principal. Continuam sendo três **agregados** com suas próprias tabelas,
invariantes e endpoints; se Tarefas um dia superar o módulo, sai com suas tabelas intactas.

## 2. Princípios centrais

1. **O alerta é o único primitivo de agendamento.** Eventos, tarefas e lembretes não crescem cada um
   sua própria lógica de notificação. Todo "me avise sobre isto no tempo T" é uma linha `Alert`,
   varrida por um sweep. *(D1)*
2. **Ocorrências são calculadas, nunca armazenadas.** Um evento recorrente é uma linha mais uma RRULE;
   leituras a expandem em memória para a janela pedida. Só *desvios* da regra ganham linhas.
   Materializar um ano de ocorrências tornaria toda edição uma migração. *(D2)*
3. **O job de agendamento vive aqui, não no Channels.** Agenda decide *quando*; Channels só sabe
   *enviar agora*. Um horário de vencimento é uma coluna numa linha, então reagendar ou concluir um
   item antes que ele dispare é um update local sem nada a cancelar downstream. *(D3)*
4. **Tempo é armazenado absoluto, exibido local, recorrido no fuso do usuário.** `timestamptz` em todo
   lugar, mais um fuso IANA no item, porque "toda segunda às 09:00" precisa sobreviver ao horário de
   verão. *(D4)*
5. **O calendário externo é um par, não um mestre.** O Pandora guarda seu próprio modelo; o sync
   (quando chegar) reconcilia dois stores independentes. Um usuário que nunca conecta o Google não
   perde nada. *(D5)*
6. **Tudo é comandável.** Toda ação do usuário é um comando de aplicação com um objeto de parâmetro
   explícito, então o [Assistant](../../assistant/pt-BR/product-plan.md) pode invocá-la sem nenhum
   round trip HTTP ou caminho de código paralelo. *(D6)*

## 3. Linguagem ubíqua

| Termo | Significado |
|---|---|
| **Calendário** (`agd001`) | Um container nomeado e colorido de eventos. Um usuário tem ao menos um (`default`). |
| **Evento** (`agd002`) | Algo que ocupa tempo: início, fim, dia inteiro ou com horário, recorrência e local opcionais. |
| **Ocorrência** | Uma materialização de um evento recorrente no tempo. Calculada da RRULE na leitura. |
| **Override** (`agd003`) | Um desvio armazenado para uma única ocorrência: cancelada ("esta terça não") ou editada ("esta mudou para 15:00"). |
| **Lista de tarefas** (`agd004`) | Um container nomeado de tarefas (a *lista* do Apple Reminders, o *projeto* do Todoist). |
| **Tarefa** (`agd005`) | Algo a fazer: status, prioridade, vencimento opcional, subtarefas opcionais, recorrência opcional. |
| **Lembrete** (`agd006`) | Um ping num instante. Sem fluxo — dispara, então é reconhecido, sonecado ou cancelado. |
| **Alerta** (`agd007`) | "Me avise sobre *sujeito* neste *offset*, por *estes canais*." Polimórfico sobre evento, tarefa e lembrete. |
| **Dispatch** (`agd006x` / `agd008`) | O registro de que um alerta (ou lembrete recorrente), para uma ocorrência, foi entregue ao Channels. A chave de idempotência do sweep. |
| **Sweep** | A passada de background que acha alertas devidos numa janela de look-ahead e os despacha. |
| **RRULE** | Um subconjunto do RFC 5545 armazenado literal no item, para um futuro sync com o Google ser cópia, não tradução com perda. |

## 4. Escopo

### No escopo (implementado — ver [Status de implementação](implementation-status.md))

O schema `agenda` (`agd001`–`agd008`); lembretes (único-disparo e recorrentes) com um ledger de
dispatch por ocorrência; o motor de recorrência RRULE (parse + expand, ciente de DST); tarefas com
listas, subtarefas, prioridade, vencimentos, concluir/reabrir, materialização recorrente e alertas;
calendários e eventos com ocorrências calculadas, overrides e os escopos de edição esta /
esta-e-futuras / todas; o alerta polimórfico com três sweeps de background; a leitura unificada
`GET /agenda/today`; botões inline do Telegram roteados de volta pelo Channels; e o frontend (Hoje,
Lembretes, Tarefas, Calendário).

### Fora do escopo / futuro (ver [product-plan.md](product-plan.md))

| Recurso | Status |
|---|---|
| **Sync Google Calendar** (fase 5) | Não implementado — tabelas de sync (`agd009`–`agd012`), provedores, cursores, log de conflito ausentes. Depende de [Integrations](../../integrations/pt-BR/overview.md). |
| **Sync Google Tasks** (fase 6) | Não implementado. |
| **Catálogo de comandos do Assistant** (fase 7) | Não implementado — os comandos existem e são comandáveis, mas o registro de descriptors para o Assistant não está ligado. |
| **Consumir o fuso padrão do Identity** | O `UserPreferences` do Identity já carrega `TimeZone`/`WeekStartsOn`/`DefaultAlertOffsetMinutes`. A Agenda ainda guarda um `time_zone` por item (recorrência expande no fuso do próprio item) e ainda não ligou a preferência do Identity como padrão de novos itens. |
| **Polimento da UI semana/dia, tela de Configurações da Agenda** | Parcialmente adiado no frontend. |
| **Além** | Links Nota↔evento, quick-add em linguagem natural, tempo de deslocamento, ICS/CalDAV, provedores Microsoft/Apple, vencimentos do Finances na visão do dia. |
