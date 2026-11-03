# Módulo Assistant

> Execução de comandos em linguagem natural contra os próprios módulos do Pandora, dentro do monolito modular Pandora.
> **Idioma:** o inglês é a documentação principal. 🇺🇸 [English version](../README.md).

O módulo **Assistant** transforma linguagem natural — digitada ou falada, no console web ou no bot de
Telegram do assistant — em comandos executados contra os módulos já existentes do Pandora (Agenda, Notes,
Finances). O modelo (Gemini) só escolhe uma tool e os argumentos; a tool é uma casca fina sobre um caso de
uso que o módulo dono já tem. Não é um chatbot com opinião nem guarda regra de negócio: se o assistant
consegue fazer algo que a API não consegue, isso é bug.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para os princípios e o vocabulário, depois vá ao tópico que precisar.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | O que faz, princípios, glossário, escopo |
| 2 | [Arquitetura](architecture.md) | Projetos, o pipeline de interpretação, catálogo de tools, confirmação, privacidade, apontar por número, caminho do Telegram, frontend, testes |
| 3 | [Modelo de dados](data-model.md) | Catálogo do schema (`ast001`–`ast004` + outbox): colunas, constraints, status |
| 4 | [Referência da API](api-reference.md) | Todos os endpoints em `/api/v{n}/assistant` |
| 5 | [Status de implementação](implementation-status.md) | O que está construído, o que mudou em relação ao plano, pontos em aberto |
| 6 | [Product Plan](product-plan.md) | O que ainda falta (OpenAI, eval set, retenção de áudio, questões em aberto) |
| 7 | [Plano de execução](execution-plan.md) | Histórico: a ordem em que o módulo foi construído (Etapa 0 → A4) |

A sequência completa do Telegram está em [Fluxo do Telegram](../../../architecture/pt-BR/telegram-assistant-flow.md).

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Assistant.*` (.NET 10, commands/queries sobre o mediator do Tars).
- **Schema:** schema PostgreSQL `assistant`, tabelas `ast001`–`ast004` + `tars_outbox_message`, PK `uuid_generate_v7()`.
- **Migrations:** `migrations/migrations/assistant/`.
- **Base da API:** `/api/v{version}/assistant`, autenticada, restrita ao usuário do token.
- **Frontend:** `client-web/src/modules/assistant` — `/assistant` (chat + histórico) e `/settings/assistant`.
- **Provedor de LLM:** Gemini via `Tars.Ai.Chat.Gemini`; modelo e chave por chamada; a chave é a conta de API key do usuário no [Integrations](../../integrations/pt-BR/README.md) (`ast001.chat_account_id`).
- **Tools:** 25, contribuídas por [Agenda](../../agenda/pt-BR/README.md), [Notes](../../notes/pt-BR/README.md) e [Finances](../../finances/pt-BR/README.md) como `IAssistantTool`s nas pastas `Application/Assistant`; o Finances também recebe arquivos compartilhados via `IAssistantFileQueue`.
- **Superfícies:** web (inline na requisição) e Telegram (subscribers de integration event no inbound do [Channels](../../channels/pt-BR/README.md); respostas pelo outbox do Assistant — [Mensageria](../../../architecture/pt-BR/messaging.md)).
