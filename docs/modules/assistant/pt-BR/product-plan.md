# Módulo Assistant — Plano de Produto (o que ainda falta)

> **Status:** o módulo está implementado — ver [Status de implementação](implementation-status.md). Este
> arquivo guarda só o que ainda não existe. O que existe está documentado na [Visão geral](overview.md) e
> na [Arquitetura](architecture.md).
> 🇺🇸 [English version](../en/product-plan.md)

---

## 1. Segundo provedor: OpenAI

Entra quando houver um motivo real além do Gemini. O formato já está pronto:

- Tars: um cliente `Pottmayer.Tars.Ai.Chat.OpenAi` (Chat Completions com tools) registrado na factory
  com keyed DI.
- Pandora: mais uma entrada no catálogo do `GetProvidersQueryHandler`, o registro no `InfrastructureDI`,
  e um `CredentialProviderDescriptor(..., AuthKind.ApiKey)` no `CredentialProvidersDI` do Integrations.

**Pronto quando:** o usuário consegue escolher uma chave e um modelo OpenAI nas configurações e as mesmas
frases funcionam.

## 2. Eval set

Um eval set de frases reais (português primeiro), para trocar de modelo ser uma decisão medida. O teste
live opt-in (`AssistantLiveTests`, `PANDORA_LIVE_GEMINI=1`) já conduz o modelo real pelo caminho do
Telegram, mas não verifica nada das escolhas dele.

**Pronto quando:** o conjunto passa no modelo escolhido, com os números registrados.

## 3. Retenção de áudio (opt-in)

O áudio é descartado depois da transcrição. Guardá-lo seria um opt-in por perfil, já que voz é o dado
mais sensível que o módulo toca. Não iniciado.

## 4. Lacunas da confirmação

- `WhenAmbiguous` não tem sinal de ambiguidade próprio: no `balanced`, roda. Ou ler um (ex.: um argumento
  que o modelo inferiu) ou renomear a política para o que ela faz.
- Expirar chamadas retidas em segundo plano, para o histórico parar de mostrar linhas
  `pending-confirmation` velhas.

## 5. Persona e voz (ideia, sem data)

A ideia é um assistente que pareça um personagem: tem nome, um jeito de falar com algum humor, e
consegue responder por voz. O código continua chamando de `assistant`; só as telas e o prompt usam o
nome.

**De onde parte.** Áudios já chegam pelo Telegram e são transcritos pelo Gemini. A conversa é
compartilhada entre web e Telegram, e as ferramentas agem nos outros módulos. Faltam duas coisas.
Primeiro, toda resposta é uma frase fixa escrita em C# pela ferramenta ("Não encontrei isso nas suas
notas."), porque o modelo só escolhe a ferramenta. Segundo, nada fala.

### Etapa 1: nome e personalidade (texto)

- **Preferências** numa tabela nova do Assistant: o nome (um padrão de fábrica que o usuário pode
  trocar) e um estilo (sóbrio, bem-humorado, mordomo formal), com o quanto de humor como ajuste. O
  padrão fica discreto, porque piada o tempo todo cansa rápido.
- **Prompt:** o `AssistantSystemPrompt` abre com "Você é {nome}…" e o estilo, então o modelo atende
  pelo nome.
- **Respostas redigidas:** a ferramenta devolve os fatos (o que fez, o que achou), e o modelo escreve a
  resposta final na voz da persona. Isso custa uma chamada a mais ao modelo por mensagem. Listas
  numeradas continuam montadas pelo código, para o "abre o 2" seguir funcionando; a persona só comenta
  em volta delas.
- **Telas:** o chat e as configurações mostram o nome.

### Etapa 2: resposta falada (áudio entra, áudio sai)

- **Síntese de voz** sobre a resposta da etapa 1. As opções:
  - **Gemini TTS ⭐**: é o mesmo provedor e a mesma chave, e o tom é guiado por instruções em texto.
  - **ElevenLabs**: as vozes mais naturais, inclusive personalizadas. É mais um provedor e mais uma
    chave, e a cobrança é por caractere.
  - **Local (Piper ou similar)**: grátis e privado, mas soa mais sem graça.
- **Telegram:** a resposta sai como mensagem de voz (`sendVoice`, OGG/Opus). Verificar se o pacote
  Telegram do tars envia voz; se não, ele precisa desse método.
- **Web:** um botão de microfone e um player sob a resposta.
- **Preferência:** responder em áudio sempre, só quando o usuário falou, ou nunca.

### Etapa 3: conversa ao vivo (só se as etapas 1 e 2 se pagarem)

- **O que é:** falar e ouvir em fluxo contínuo, com interrupções e resposta em menos de um segundo.
- **No que difere:** é outra arquitetura. Uma API de áudio nativo em streaming (por exemplo, o Gemini
  Live) recebe áudio e devolve áudio, e chama ferramentas sozinha.
- **O que precisa:**
  - um WebSocket do navegador ao backend e dele ao provedor;
  - os `IAssistantTool` atuais expostos nesse canal;
  - confirmações faladas.
- **Onde roda:** no web e no Pandora Desktop. O bot do Telegram não faz chamada de voz.

**Privacidade:** cada etapa manda mais coisa ao provedor. A etapa 2 manda o texto da resposta ao
serviço de voz. A etapa 3 transmite a voz do usuário o tempo todo enquanto a chamada está aberta. As
[regras de privacidade](architecture.md) precisam de uma linha para cada etapa antes de construí-la.

## 6. Questões em aberto

1. **Resumos proativos** ("aqui está o seu dia" às 07:00). Por enquanto não: `list_agenda` sob pedido
   cobre, e pela regra de privacidade o resumo seria montado por template aqui, não gerado. Um resumo
   gerado precisaria de decisão própria.
2. **Streaming.** Não é necessário para executar comandos; só importa na etapa 3 de
   [persona e voz](#5-persona-e-voz-ideia-sem-data). A abstração do Tars não expõe.
3. **Retenção de conversas e invocações** (`ast002`–`ast004`), que hoje nunca são expurgadas.
