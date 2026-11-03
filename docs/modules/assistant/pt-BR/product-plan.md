# Módulo Assistant — Plano de Produto (o que ainda falta)

> **Status:** o módulo está implementado — ver [Status de implementação](implementation-status.md). Este
> arquivo guarda só o que ainda não existe. O que existe está documentado na [Visão geral](overview.md) e
> na [Arquitetura](architecture.md); a ordem original de construção está no [plano de execução](execution-plan.md).
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

## 5. Questões em aberto

1. **Resumos proativos** ("aqui está o seu dia" às 07:00). Por enquanto não: `list_agenda` sob pedido
   cobre, e pela regra de privacidade o resumo seria montado por template aqui, não gerado. Um resumo
   gerado precisaria de decisão própria.
2. **Streaming.** Não é necessário para executar comandos; só para um modo conversacional, se um dia
   existir. A abstração do Tars não expõe.
3. **Retenção de conversas e invocações** (`ast002`–`ast004`), que hoje nunca são expurgadas.
