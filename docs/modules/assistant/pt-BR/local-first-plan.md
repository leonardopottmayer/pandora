# Assistant — Plano de implementação (local-first)

> **Status:** Plano de execução. Recorta o [product-plan](product-plan.md) para o caminho
> **local-first** — só Ollama, self-hosted no homelab. Corta as fases A5 (OpenAI/Gemini) e A6
> (Notes/Finances/proativo) do escopo imediato.
> 🇺🇸 [English version](../en/local-first-plan.md)

Este doc é a lista de trabalho. O *porquê* de cada decisão está no [product-plan](product-plan.md); a
assincronia sem broker está em [mensageria §3](../../../architecture/pt-BR/messaging.md#3-assincronia-sem-broker).

---

## Decisões travadas

| Decisão | Valor | Motivo |
|---|---|---|
| **Provedor inicial** | Ollama (self-hosted) | Sem conta/chave de API; é o alvo declarado do homelab. |
| **Modelo alvo** | `qwen2.5:7b-instruct` (Q4_K_M) | Cabe nos 8 GB de VRAM das duas máquinas; melhor tool-calling da faixa. Confirmar no spike. |
| **Primeira superfície** | Web (barra de comando) | Roda o pipeline inline; vê-se a tool call na hora, sem a plumbing assíncrona do Telegram. |
| **Primeiro comando** | Agenda `create_reminder` | Menor superfície de argumentos (título + timestamp); prova o loop end-to-end rápido. |
| **Deploy do Ollama** | `service` no `docker-compose.yml`, profile `assistant` | Sobe junto da stack (mesmo ciclo do Postgres/nginx); backend fala por `http://ollama:11434`. |
| **Fora do escopo local-first** | OpenAI/Gemini (A5), Integrations I3 | `ast001.credential_ref` é nulo pro Ollama → **zero acoplamento com Integrations** neste arco. |

---

## Etapa 0 — Spike de tool-calling *(descartável, fora dos projetos)*

Valida a premissa que sustenta o módulo inteiro: **um 7B local emite tool call confiável em PT-BR?**
Antes disso, não se scaffolda nada.

1. Instalar o Ollama nativo na máquina de dev (RTX 3070) e `ollama pull qwen2.5:7b-instruct`.
2. Script (Python ou C# solto na scratchpad) que:
   - Define **uma** tool no formato do Ollama: `create_reminder(title: string, remindAt: string ISO-8601)`.
   - Monta o system prompt com a hora local atual, o fuso IANA e o início da semana.
   - Roda um lote de ~15–20 frases reais em português: casos limpos ("me lembra de ligar pro dentista
     amanhã às 9"), datas relativas ("sexta que vem", "daqui a 3 dias"), e ambíguos (que deveriam pedir
     confirmação ou falhar, não inventar).
   - Imprime por frase: a tool call devolvida, se `remindAt` resolveu certo, e a latência.

**Pronto quando:** o modelo acerta tool call + timestamp na grande maioria das frases limpas, numa
latência tolerável. Se passar, `qwen2.5:7b-instruct` vira o alvo fixo. Se nenhum modelo da faixa passar,
isso muda a estratégia — e é barato descobrir agora.

---

## Etapa A1 — `Tars.Ai` + perfil do Assistant

Building block de transporte no Tars + a casca do módulo + configuração por usuário.

### A1.1 — `Tars.Ai.Abstractions`
- Projeto novo em `tars/src/Ai/` seguindo o layout dos outros building blocks
  (`Pottmayer.Tars.Ai.Abstractions`).
- Contratos mínimos p/ o local-first: `IChatCompletionClient` (mensagens, definições de tool, tool
  calls, uso de tokens), o modelo compartilhado de mensagem/tool, `AiException` com separação
  permanente vs. transiente. `ITranscriptionClient` e `IEmbeddingClient` ficam **reservados** (entram
  na A4 / futuro), não implementados agora.
- Doc no Tars em `docs/ai/`.

### A1.2 — `Tars.Ai.Ollama`
- `Pottmayer.Tars.Ai.Ollama`: implementa `IChatCompletionClient` via `POST /api/chat` com `tools`.
- Endpoint e modelo configuráveis; sem credenciais.
- Mapear tool calls do Ollama → o modelo compartilhado das Abstractions.

### A1.3 — Scaffold do módulo Assistant
- Sete projetos por camada:
  `Pottmayer.Pandora.Modules.Assistant.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}`.
- Schema PostgreSQL `assistant`, prefixo `astXXX_`, PK `uuid_generate_v7()`.
- Registro do módulo + DI no Host; conexão `assistant` no `docker-compose.yml` (env
  `Tars__Data__Connections__assistant__ConnectionString`).

### A1.4 — `ast001_assistant_profile`
- Migration em `migrations/migrations/assistant/`. Colunas (subconjunto local-first):
  `user_id` (único), `chat_provider`, `chat_model`, `endpoint`, `is_enabled`, `locale_override`,
  `confirmation_level`. `credential_ref` e as colunas de transcrição ficam nuláveis/reservadas.
- Agregado `AssistantProfile` no Domain; repositório no Persistence.

### A1.5 — Configuração + teste de alcance
- `GET`/`POST /assistant/profile` (provedor/modelo/endpoint).
- `GET /assistant/providers` — lista provedores disponíveis e faz um **teste de alcance** (um ping/prompt
  curto no endpoint configurado, reportando ok/erro + latência).

### A1.6 — Frontend de configurações
- `client-web/src/modules/assistant` — tela que salva o perfil e dispara o teste de alcance,
  mostrando a resposta crua do modelo.

**Pronto quando:** a tela de configurações faz um prompt ida-e-volta num Ollama local e mostra a
resposta. (Casa com a definição de pronto da A1 do product-plan.)

---

## Etapa A2 — Pipeline de comandos (web, texto)

O coração do módulo. Frase em português → tool call validada → comando executado → confirmação.

### A2.1 — Catálogo de comandos
- Em `Assistant.Abstractions`: `AssistantCommandDescriptor(Name, Description, ParametersJsonSchema,
  Confirmation, Examples)` e `IAssistantCommandHandler(Name, ExecuteAsync(userId, args, ct))`.
- Descoberta na inicialização: o Assistant coleta todo descritor registrado e renderiza como as
  definições de tool do provedor.

### A2.2 — Agenda registra `create_reminder`
- A Agenda contribui **um** descritor (`create_reminder`) do seu `Abstractions` + um handler fino que
  mapeia os argumentos no caso de uso `CreateReminder` **já existente** e manda pelo mediator. Nenhuma
  regra de negócio duplicada. (`create_task` e o resto do catálogo entram depois, na A2 estendida / A5.)

### A2.3 — System prompt
- Único prompt versionado no código, carregando: hora local atual + fuso IANA + início da semana (do
  Identity, via `IUserPreferencesReader`), locale, e a regra de que todo timestamp volta **absoluto e
  ISO-8601**. Few-shot vindo do campo `Examples` dos descritores, em português.

### A2.4 — Schema de conversa e auditoria
- Migrations: `ast002_conversation` (expira após 30 min de silêncio), `ast003_message`,
  `ast004_command_invocation` (a trilha de auditoria: `command_name`, `arguments` jsonb, `status`,
  `result`/`error`, `provider`/`model`/`latency_ms`/`tokens_*`).

### A2.5 — `POST /assistant/interpret` (texto)
- Recebe `{ text }`. Pipeline inline: monta prompt → `IChatCompletionClient` chat+tools → valida a tool
  call (JSON schema + validador do módulo dono) → executa via handler → grava a invocação → responde a
  intenção interpretada + resultado.
- Comportamento em falha conforme §5 do product-plan (nenhuma tool casou / argumento faltando / validação
  rejeitou / provedor inacessível / JSON malformado). **Nunca alegar sucesso que não houve** — status vem
  do resultado do comando.

### A2.6 — Confirmação
- `ConfirmationPolicy` do descritor, ajustada pelo `confirmation_level` do perfil. `create_reminder` é
  `WhenAmbiguous`: executa se inequívoco; senão grava `ast004` em `pending_confirmation` (expira em 10
  min) e ecoa a intenção.
- `POST /assistant/invocations/{id}/confirm` e `/cancel`.

### A2.7 — Frontend
- Barra de comando no client chamando `/interpret`; fluxo de confirmação (Confirmar/Cancelar).
- `GET /assistant/invocations` — a trilha de auditoria, pra ver a tool call exata que o modelo produziu.

**Pronto quando:** digitar "lembrete de pagar o aluguel dia 5 às 10h" no navegador cria o lembrete
certo, e o log de invocação mostra a tool call exata.

---

## Etapa A3 — Entrada por Telegram (texto) *(esboço)*

Reusa o [Channels C4](../../channels/pt-BR/inbound-and-linking.md), já implementado. A diferença crítica
pra A2: o pipeline **não roda inline** — modelo local leva segundos e não deve receber trabalho
concorrente.

- Subscriber ligado a `inbound.message.#` faz só a parte barata: grava uma linha de invocação e retorna.
- Um **job em background deste módulo** drena as linhas **uma de cada vez** (padrão sem-broker do
  [§3](../../../architecture/pt-BR/messaging.md#3-assincronia-sem-broker)) e roda o pipeline da A2.
- Resposta via `NotifyUserRequested`; botões de confirmação com `owner_module: "assistant"`, voltando por
  `inbound.interaction.assistant.#`.

**Pronto quando:** a mesma frase digitada no Telegram cria o mesmo lembrete, e *Confirmar* funciona.

---

## Etapa A4 — Voz *(esboço)*

- Implementar `ITranscriptionClient` no Tars (adaptador de Whisper self-hosted atrás de HTTP — padrão do
  homelab).
- Áudio do Telegram lido por `IInboundMediaReader` (já existe no Channels); web grava com `MediaRecorder`
  e sobe multipart pro `/interpret`.
- Retenção de áudio é opt-in por perfil (voz é o dado mais sensível do módulo).

**Pronto quando:** um áudio no Telegram cria um lembrete, em português.

---

## Fora de escopo (por enquanto)

- **A5 — OpenAI/Gemini.** Depende de contas/chaves que não existem e do **Integrations I3** (gestão de
  API key). Reabrir quando houver chave; o design de provedor-como-porta já deixa isso plugável.
- **A6 — Notes/Finances/proativo.** `record_transaction`, `create_note`, resumos matinais. Entram depois
  que o loop da Agenda estiver sólido — cada um é um descritor + handler novo, não uma reescrita.

---

## Ordem de execução

```
Etapa 0 (spike)  ──►  A1 (Tars.Ai + perfil)  ──►  A2 (pipeline web + create_reminder)  ──►  A3 (Telegram)  ──►  A4 (voz)
     gate              gate                          gate                                    gate             gate
  modelo passa     roundtrip na UI              lembrete criado no navegador            mesma coisa no TG    áudio vira lembrete
```

Cada gate é bloqueante: não se avança sem o "pronto quando" da etapa anterior verde.
