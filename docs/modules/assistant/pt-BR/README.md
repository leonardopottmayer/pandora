# Módulo Assistant

> Execução de comandos em linguagem natural contra os próprios módulos do Pandora, dentro do monolito modular Pandora.
> 🇺🇸 [English version](../README.md).
>
> **Status: plano.** O backend do módulo Assistant ainda não existe, mas o building block `Tars.Ai`
> (Gemini) e o armazenamento da chave no Integrations já existem (uncommitted / feito) — ver
> [product-plan.md](product-plan.md) para o escopo completo e
> [execution-plan.md](execution-plan.md) para a lista de trabalho atual (Gemini, hospedado).

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
| [Product Plan](product-plan.md) / [en](../en/product-plan.md) | en + pt-BR | Escopo completo: superfícies (Telegram, Web), provedores atrás de uma porta (Gemini agora, OpenAI depois), fases A1–A6 |
| [Plano de execução](execution-plan.md) / [en](../en/execution-plan.md) | en + pt-BR | A lista de trabalho que está sendo construída primeiro: Gemini hospedado com a chave no Integrations; corta OpenAI e as fases Notes/Finances/proativo do escopo imediato |

Quando o módulo tiver implementação real, deveria migrar para a mesma estrutura `en/` + `pt-BR/` por
tópico que os outros módulos usam (ver ex. [Identity](../../identity/pt-BR/README.md)), com
`overview.md`, `architecture.md`, `data-model.md`, `api-reference.md` e `implementation-status.md`.

---

## Fatos rápidos

- **Backend:** módulo não iniciado. O product plan aponta para uma nova família `Pottmayer.Pandora.Modules.Assistant.*`. O transporte `Tars.Ai` (chat + Gemini) já está implementado (uncommitted).
- **Superfícies:** Telegram (texto + notas de voz), barra de comando web.
- **Provedor de LLM:** Gemini (hospedado), atrás de uma porta com keyed DI; OpenAI é adição futura. Ollama/local foi abandonado.
- **Primeira fatia de execução:** Gemini via `Tars.Ai`, chave guardada por usuário no Integrations; primeira superfície é a barra de comando web, primeiro comando é o `create_reminder` da Agenda. Ver [execution-plan.md](execution-plan.md).
- **Depende de:** [Mensageria](../../../architecture/pt-BR/messaging.md) (assincronia sem broker, §3), [Agenda](../../agenda/pt-BR/README.md), [Integrations](../../integrations/pt-BR/README.md) (a chave da API do Gemini: `ast001.credential_ref` → `int001_external_account`).
