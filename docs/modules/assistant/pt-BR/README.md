# Módulo Assistant

> Execução de comandos em linguagem natural contra os próprios módulos do Pandora, dentro do monolito modular Pandora.
> 🇺🇸 [English version](../README.md).
>
> **Status: só plano.** Nada descrito nos docs deste módulo está implementado ainda — ver
> [product-plan.md](product-plan.md) para o escopo completo e
> [local-first-plan.md](local-first-plan.md) para a fatia de execução atual (Ollama, local-first).

O módulo **Assistant** transforma linguagem natural — digitada ou falada, no Telegram ou na barra de
comando web — em comandos executados contra os módulos já existentes do Pandora (Agenda, Notes,
Finances, ...). Não é um chatbot com opinião própria nem lugar onde regra de negócio mora: toda ação
que ele executa é um comando que o web UI também pode chamar diretamente. Se o assistant consegue
fazer algo que a API não consegue, isso é bug.

---

## Como esta documentação está organizada

Diferente dos outros módulos, o Assistant ainda não tem o conjunto completo de tópicos `en/` + `pt-BR/`
(overview, architecture, data-model, api-reference, implementation-status) — não há nada construído
para documentar. O que existe hoje:

| Documento | Idioma | O que cobre |
|---|---|---|
| [Product Plan](product-plan.md) / [en](../en/product-plan.md) | en + pt-BR | Escopo completo: superfícies (Telegram, Web), os três back-ends de LLM intercambiáveis (Ollama, OpenAI, Gemini), fases A1–A6 |
| [Plano de implementação local-first](local-first-plan.md) / [en](../en/local-first-plan.md) | en + pt-BR | A fatia de execução que está sendo construída primeiro: só Ollama, self-hosted, corta OpenAI/Gemini e as fases Notes/Finances/proativo do escopo imediato |

Quando o módulo tiver implementação real, deveria migrar para a mesma estrutura `en/` + `pt-BR/` por
tópico que os outros módulos usam (ver ex. [Identity](../../identity/pt-BR/README.md)), com
`overview.md`, `architecture.md`, `data-model.md`, `api-reference.md` e `implementation-status.md`.

---

## Fatos rápidos

- **Backend:** não iniciado. O product plan aponta para uma nova família `Pottmayer.Pandora.Modules.Assistant.*`.
- **Superfícies:** Telegram (texto + notas de voz), barra de comando web.
- **Back-ends de LLM:** Ollama (self-hosted, alvo local-first), OpenAI, Gemini — escolhido por usuário.
- **Primeira fatia de execução:** só Ollama, `qwen2.5:7b-instruct`, deploy como serviço no `docker-compose`; primeiro comando é o `create_reminder` da Agenda. Ver [local-first-plan.md](local-first-plan.md).
- **Depende de:** [Mensageria](../../../architecture/pt-BR/messaging.md) (assincronia sem broker, §3), [Agenda](../../agenda/pt-BR/README.md), [Integrations](../../integrations/pt-BR/README.md) (adiado na fatia local-first — Ollama não precisa de `ast001.credential_ref`).
