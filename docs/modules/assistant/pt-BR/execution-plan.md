# Assistant — Plano de execução (Gemini)

> **Status:** Plano de execução. Recorta o [product-plan](product-plan.md) para o caminho que está
> sendo construído primeiro: **provedor único Gemini (hospedado)**, chave por usuário guardada no
> Integrations. Corta OpenAI e a fase A6 (Notes/Finances/proativo) do escopo imediato.
> 🇺🇸 [English version](../en/execution-plan.md)

Este doc é a lista de trabalho. O *porquê* de cada decisão está no [product-plan](product-plan.md); a
assincronia sem broker está em [mensageria §3](../../../architecture/pt-BR/messaging.md#3-assincronia-sem-broker).

> **O que mudou em relação ao plano anterior (local-first / Ollama):** a ideia de rodar um modelo
> local foi abandonada — não vale o custo/qualidade. O alvo agora é **Gemini hospedado**. Isso
> reverte a decisão de "zero acoplamento com Integrations": a chave do Gemini **é** guardada no
> Integrations e buscada por chamada. Ver [Decisões travadas](#decisões-travadas).

---

## O que já existe (não refazer)

Dois pré-requisitos que o plano antigo colocava na fase A1 **já estão prontos** — só faltou o teste
com a API real, que é o gate da Etapa 0.

### Tars — família `Ai` (uncommitted, 17 testes verdes)

Namespacing **por capacidade**, não por provedor (deixa `Ai.Transcription.*` / `Ai.Embedding.*`
entrarem depois sem reorganizar):

| Projeto | Conteúdo real |
|---|---|
| `Pottmayer.Tars.Ai.Abstractions` | `AiException` com `IsPermanent` (permanente vs. transiente), compartilhado por todas as capacidades. |
| `Pottmayer.Tars.Ai.Chat.Abstractions` | `IAiChatCompletionClient.CompleteAsync(ChatRequest)`, `IAiChatCompletionClientFactory.GetClient(provider)`, e os models: `ChatRequest(Model, Messages, Tools?, Temperature?, ApiKey?)`, `ChatCompletion`, `ChatMessage`/`ChatRole`, `ToolDefinition(Name, Description, ParametersJsonSchema)`, `ToolCall(Name, Arguments, Id?)`, `TokenUsage`. |
| `Pottmayer.Tars.Ai.Chat` | `KeyedAiChatCompletionClientFactory` (resolve por keyed DI) + `AddTarsAiClientFactory`. |
| `Pottmayer.Tars.Ai.Chat.Gemini` | `GeminiAiChatCompletionClient` (`ProviderName = "gemini"`), sobre `v1beta/models/{model}:generateContent`, chave no header `x-goog-api-key`; options `Tars:Ai:Chat:Gemini`; mapper wire, classificador de erro. DI: `AddTarsAiChatGeminiOptions`, `AddTarsAiChatGeminiHttpClient`, `AddTarsAiChatCompletionClientGemini`. |

Dois traços que o pipeline do Pandora vai usar diretamente:

- **Modelo e chave por chamada.** `ChatRequest.Model` e `ChatRequest.ApiKey` vêm em cada request —
  não são default de app. É exatamente o formato "cada usuário traz a sua chave". Quando a chave vem
  vazia, cai no default das options. O `ToString` do `ChatRequest` mascara a chave.
- **`Temperature: 0`** para a saída determinística que um pipeline de comando quer.

Falta: transcrição e embeddings **não têm nem projeto** — entram na A4/futuro.

### Pandora Integrations (implementado)

- `IExternalCredentialProvider.GetApiKeyAsync(userId, "gemini")` devolve a chave **em plaintext**,
  descriptografada de `int001_external_account` via `ISecretProtector`. `AuthKind.ApiKey`.
- `ApiKeyProviderDescriptor("gemini", "Google Gemini")` já registrado; comando `SaveApiKey` cifra em
  repouso. Adicionar OpenAI depois é uma linha.

---

## Decisões travadas

| Decisão | Valor | Motivo |
|---|---|---|
| **Provedor** | Gemini (hospedado) | Ollama abandonado. Tool-calling forte em PT-BR sem manter GPU/homelab. Transporte já implementado no Tars. |
| **Modelo alvo** | um Gemini rápido (ex.: `gemini-2.5-flash`) | Latência baixa e bom function-calling. Confirmar na Etapa 0; é `ChatRequest.Model`, trocável sem deploy. |
| **Chave da API** | `int001_external_account` (`auth_kind = api-key`, provider `gemini`) | Cofre cifrado único. `ast001.credential_ref` aponta pra lá; buscada por chamada e passada em `ChatRequest.ApiKey`. **Acoplamento com Integrations é o mecanismo**, não um a-evitar. |
| **Primeira superfície** | Web (barra de comando) | Roda o pipeline inline; vê-se a tool call na hora, sem a plumbing assíncrona do Telegram. |
| **Primeiro comando** | Agenda `create_reminder` | Menor superfície de argumentos (título + timestamp); prova o loop end-to-end rápido. |
| **Fora do escopo agora** | OpenAI, A6 (Notes/Finances/proativo) | OpenAI é "mais um descritor de provider" quando fizer sentido; A6 é descritor+handler por comando. |

---

## Etapa 0 — Primeira chamada real ao Gemini + commit do `Tars.Ai`

O `Tars.Ai` está verde nos testes mas **nunca falou com a API de verdade** — foi por isso que não
comitamos. Esta etapa fecha esse gap; é o que restou do antigo "spike" (o risco de um 7B local em
PT-BR não existe mais).

1. Obter uma chave do Google AI Studio e configurá-la (options `Tars:Ai:Chat:Gemini` **ou** passada
   em `ChatRequest.ApiKey`).
2. Um script solto na scratchpad (C# ou HTTP direto) que:
   - Define **uma** tool `create_reminder(title: string, remindAt: string ISO-8601)` como
     `ToolDefinition`.
   - Monta o system prompt com a hora local atual, o fuso IANA e o início da semana.
   - Roda ~15–20 frases reais em português: casos limpos ("me lembra de ligar pro dentista amanhã às
     9"), datas relativas ("sexta que vem", "daqui a 3 dias"), e ambíguos (que deveriam pedir
     confirmação ou falhar, não inventar).
   - Imprime por frase: a `ToolCall` devolvida, se `remindAt` resolveu certo, e a latência.
3. Confirmar o modelo alvo.

**Pronto quando:** o Gemini acerta tool call + timestamp na grande maioria das frases limpas, numa
latência tolerável — e o `Tars.Ai` está **comitado**.

---

## Etapa A1 — Casca do módulo + perfil do Assistant

O transporte já existe (acima). O que falta na A1 é só o lado Pandora: scaffold do módulo,
configuração por usuário e o registro do Gemini no Host.

### A1.1 — Scaffold do módulo Assistant
- Sete projetos por camada:
  `Pottmayer.Pandora.Modules.Assistant.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}`.
- Schema PostgreSQL `assistant`, prefixo `astXXX_`, PK `uuid_generate_v7()`.
- Registro do módulo + DI no Host; conexão `assistant` no `docker-compose.yml` (env
  `Tars__Data__Connections__assistant__ConnectionString`).

### A1.2 — Registrar o Gemini no Host
- No Host: `AddTarsAiClientFactory()` + `AddTarsAiChatGeminiOptions()` +
  `AddTarsAiChatGeminiHttpClient()` + `AddTarsAiChatCompletionClientGemini()`.
- **Sem** chave default nas options — a chave vem por chamada, do Integrations.

### A1.3 — `ast001_assistant_profile`
- Migration em `migrations/migrations/assistant/`. Colunas (subconjunto atual):
  `user_id` (único), `chat_provider` (`"gemini"`), `chat_model` (o id do modelo Gemini),
  `credential_ref` (→ `int001_external_account`), `is_enabled`, `locale_override`,
  `confirmation_level`. Colunas de transcrição e `endpoint` ficam nuláveis/reservadas.
- Agregado `AssistantProfile` no Domain; repositório no Persistence.

### A1.4 — Configuração + teste de alcance
- `GET`/`POST /assistant/profile` (provedor/modelo/`credential_ref`).
- `GET /assistant/providers` — lista provedores disponíveis (hoje só `gemini`) e faz um **teste de
  alcance**: busca a chave do usuário via `GetApiKeyAsync`, monta um `ChatRequest` mínimo, chama o
  `IAiChatCompletionClient` e reporta ok/erro + latência. Distingue "sem chave configurada" de
  "chave rejeitada" (via `AiException.IsPermanent`).

### A1.5 — Frontend de configurações
- `client-web/src/modules/assistant` — tela que salva o perfil (escolhe modelo, aponta a conta
  Gemini do Integrations) e dispara o teste de alcance, mostrando a resposta crua do modelo.

**Pronto quando:** a tela de configurações faz um prompt ida-e-volta no Gemini, usando a chave do
usuário guardada no Integrations, e mostra a resposta.

---

## Etapa A2 — Pipeline de comandos (web, texto)

O coração do módulo. Frase em português → tool call validada → comando executado → confirmação.

### A2.1 — Catálogo de comandos
- Em `Assistant.Abstractions`: `AssistantCommandDescriptor(Name, Description, ParametersJsonSchema,
  Confirmation, Examples)` e `IAssistantCommandHandler(Name, ExecuteAsync(userId, args, ct))`.
- Descoberta na inicialização: o Assistant coleta todo descritor registrado e o renderiza como
  `ToolDefinition[]` para o `ChatRequest`.

### A2.2 — Agenda registra `create_reminder`
- A Agenda contribui **um** descritor (`create_reminder`) do seu `Abstractions` + um handler fino que
  mapeia os argumentos no caso de uso `CreateReminder` **já existente** e manda pelo mediator. Nenhuma
  regra de negócio duplicada. (`create_task` e o resto do catálogo entram depois.)

### A2.3 — System prompt
- Único prompt versionado no código, carregando: hora local atual + fuso IANA + início da semana (do
  Identity, via `IUserPreferencesReader`), locale, e a regra de que todo timestamp volta **absoluto e
  ISO-8601**. Few-shot vindo do campo `Examples` dos descritores, em português.

### A2.4 — Schema de conversa e auditoria
- Migrations: `ast002_conversation` (expira após 30 min de silêncio), `ast003_message`,
  `ast004_command_invocation` (a trilha de auditoria: `command_name`, `arguments` jsonb, `status`,
  `result`/`error`, `provider`/`model`/`latency_ms`/`tokens_*` — de `TokenUsage`).

### A2.5 — `POST /assistant/interpret` (texto)
- Recebe `{ text }`. Pipeline inline:
  1. carrega o perfil; busca a chave via `IExternalCredentialProvider.GetApiKeyAsync(userId, "gemini")`;
  2. monta prompt + `ToolDefinition[]` do catálogo;
  3. `IAiChatCompletionClient.CompleteAsync(new ChatRequest(model, messages, tools, Temperature: 0, ApiKey: key))`;
  4. valida a `ToolCall` (JSON schema + validador do módulo dono);
  5. executa via handler;
  6. grava a invocação; responde a intenção interpretada + resultado.
- Comportamento em falha conforme §5 do product-plan (nenhuma tool casou / argumento faltando /
  validação rejeitou / provedor inacessível / JSON malformado). `AiException.IsPermanent` decide se
  vale retry. **Nunca alegar sucesso que não houve** — status vem do resultado do comando.
- **Single-turn aqui, por design.** `messages` é só `[system, user]`; o pipeline persiste a conversa e
  suas mensagens (`ast002`/`ast003`), mas ainda não realimenta o histórico no prompt. Reenviar as
  mensagens recentes — agnóstico de canal, limitado por tempo — é a [A3](#etapa-a3--entrada-telegram-texto-esboço).

### A2.6 — Confirmação
- `ConfirmationPolicy` do descritor, ajustada pelo `confirmation_level` do perfil. `create_reminder`
  é `WhenAmbiguous`: executa se inequívoco; senão grava `ast004` em `pending-confirmation` (expira em
  10 min) e ecoa a intenção.
- `POST /assistant/invocations/{id}/confirm` e `/cancel`.

### A2.7 — Frontend
- Barra de comando no client chamando `/interpret`; fluxo de confirmação (Confirmar/Cancelar).
- `GET /assistant/invocations` — a trilha de auditoria, pra ver a tool call exata que o modelo produziu.

**Pronto quando:** digitar "lembrete de pagar o aluguel dia 5 às 10h" no navegador cria o lembrete
certo, e o log de invocação mostra a tool call exata.

---

## Etapa A3 — Entrada por Telegram (texto) *(esboço)*

Reusa o [Channels C4](../../channels/pt-BR/inbound-and-linking.md), já implementado. A diferença pra
A2: o pipeline **não roda inline**. Com Gemini hospedado o motivo não é mais "não sobrecarregar o
modelo local" — é **latência e durabilidade**: uma chamada de rede de vários segundos não deve
segurar o subscriber de entrada, e uma execução que morre no meio tem que ser uma linha com estado,
não uma mensagem perdida.

- Subscriber ligado a `inbound.message.#` faz só a parte barata: grava uma linha de invocação e retorna.
- Um **job em background deste módulo** drena as linhas **uma de cada vez** (padrão sem-broker do
  [§3](../../../architecture/pt-BR/messaging.md#3-assincronia-sem-broker)) e roda o pipeline da A2.
- Resposta via `NotifyUserRequested`; botões de confirmação com `owner_module: "assistant"`, voltando
  por `inbound.interaction.assistant.#`.

**Contexto multi-turno (decidido na A2, entregue aqui).** A A2 monta o prompt como `[system, user]` —
sem histórico. A partir da A3 o pipeline passa a realimentar as **últimas mensagens da conversa** antes
da frase atual (`[system, …histórico…, user]`), para que um follow-up ("na verdade, muda pra 11h",
"sim", "e amanhã também") seja entendido. Isso é **agnóstico de canal**: a mesma conversa (`ast002` +
`ast003`, já construídas na A2) serve tanto a barra web quanto o Telegram. A janela de contexto é
**limitada por tempo** — só mensagens dentro do `Conversation.IdleTimeout` (hoje 30 min de silêncio, o
mesmo que faz uma conversa lapsar); passado isso, começa um fio novo e nada do anterior é reenviado.
Isso mantém sob controle o custo de tokens e o quanto de texto sai para o Google (a questão de
privacidade). Um teto de N mensagens (além do teto de tempo) entra junto.

**Pronto quando:** a mesma frase digitada no Telegram cria o mesmo lembrete, *Confirmar* funciona, e um
follow-up dentro da janela (web **ou** Telegram) é interpretado usando o contexto das mensagens anteriores.

---

## Etapa A4 — Voz *(esboço)*

Com Gemini já no lugar, o caminho natural é a **entrada de áudio do próprio Gemini** — não um Whisper
self-hosted (aquela era decisão do arco local). Isso pode dispensar um `ITranscriptionClient`
separado, ou implementá-lo como uma capacidade fina sobre o mesmo provedor. Decidir na hora.

- Áudio do Telegram lido por `IInboundMediaReader` (já existe no Channels); web grava com
  `MediaRecorder` e sobe multipart pro `/interpret`.
- Retenção de áudio é opt-in por perfil (voz é o dado mais sensível do módulo).

**Pronto quando:** um áudio no Telegram cria um lembrete, em português.

---

## Fora de escopo (por enquanto)

- **OpenAI como segundo provedor.** O design provedor-como-porta (keyed DI no Tars +
  `ApiKeyProviderDescriptor` no Integrations) já deixa plugável: um projeto `Ai.Chat.OpenAi` + uma
  linha no registro. Entra quando houver motivo real de tê-lo além do Gemini.
- **A6 — Notes/Finances/proativo.** `record_transaction`, `create_note`, resumos matinais. Entram
  depois que o loop da Agenda estiver sólido — cada um é um descritor + handler novo, não uma reescrita.

---

## Questão que subiu de prioridade: dado pessoal sai de casa

No arco local (Ollama) tudo ficava na máquina. Com **Gemini hospedado, todo enunciado sai de casa** —
e comandos de leitura como `list_agenda` mandariam títulos de eventos para o Google. Isso deixou de
ser uma questão "para resolver antes da A5" e passou a valer **agora**, porque só existe provedor
hospedado. Mínimo aceitável para o primeiro release:

- Um aviso claro por perfil de que os enunciados vão para o Google.
- No começo, o catálogo é só **escrita** (`create_reminder`), então não vaza histórico — leituras
  (`list_agenda`, `search_items`) entram junto com uma decisão explícita sobre isso.

---

## Ordem de execução

```
Etapa 0 (chamada real + commit)  ──►  A1 (casca + perfil)  ──►  A2 (pipeline web + create_reminder)  ──►  A3 (Telegram)  ──►  A4 (voz)
        gate                             gate                       gate                                    gate            gate
   Gemini responde, Tars.Ai         roundtrip na UI            lembrete criado no navegador           mesma coisa no TG   áudio vira lembrete
   comitado                         (chave via Integrations)
```

Cada gate é bloqueante: não se avança sem o "pronto quando" da etapa anterior verde.
