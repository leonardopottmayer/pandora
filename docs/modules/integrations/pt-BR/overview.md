# Visão geral — Limite e Princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

**Integrations** é dono das credenciais que o Pandora usa **em nome do usuário** para chamar serviços
de terceiros. Ele cuida de:

- O **fluxo OAuth authorization-code com PKCE**: montar a URL de consentimento, persistir a requisição
  em andamento e consumir o callback.
- **Armazenamento de tokens**, encriptado em repouso — access tokens, refresh tokens, escopos concedidos.
- **Refresh transparente**: quem pede um access token recebe um válido ou uma falha tipada; nunca vê
  nem implementa refresh.
- **Revogação e desconexão**: revogar no provedor e apagar a conta local.
- **Chaves de API fornecidas pelo usuário** (`auth_kind = api_key`) como alternativa ao OAuth, para
  provedores que autenticam com uma chave estática.

É, de propósito, o menor módulo do Pandora e responde exatamente uma pergunta:

> "Me dê uma credencial válida do usuário *U* no provedor *P*."

[Agenda](../../agenda/pt-BR/product-plan.md) é seu primeiro consumidor — precisa de um token Google
para sincronizar calendários e tarefas. Assistant (chaves de LLM hospedado) e Finances (open finance)
são segundos consumidores plausíveis, e é por isso que o cofre de credenciais é separado desde o dia
um: nenhum deles precisa mexer no schema de outro módulo depois.

## 2. O que ele não é

- **Não é um motor de sincronização.** Ele não sabe o que é um calendário, um evento ou uma tarefa.
  Cursores de sync, mapeamentos de entidades e resolução de conflitos vivem no módulo consumidor
  (Agenda). Integrations entrega tokens; Agenda os usa.
- **Não é dono de um *canal*.** Um chat id do Telegram é um **endereço** onde o Pandora alcança o
  usuário, e um bot token é uma credencial de **deployment** — ambos vivem em
  [Channels](../../channels/pt-BR/product-plan.md). O limite em uma linha:

> **Integrations:** o Pandora chama um terceiro *como* o usuário. **Channels:** o Pandora fala *com* o usuário.

Nada disso aparece na UI: configurações tem uma seção **Contas conectadas**, composta de duas
preocupações de backend. Unidade de UI não é unidade de módulo.

## 3. Princípios centrais

1. **Tokens nunca saem do módulo em forma armazenável.** Consumidores recebem um access token de curta
   duração por uma porta, não por um repositório. Não existe `GetRefreshToken`. *(Decisão I1.)*
2. **Refresh é invisível.** `GetAccessTokenAsync` devolve algo válido ou falha. Quem chama nunca
   implementa refresh, e existe exatamente um lugar que pode disputar por ele. *(I2.)*
3. **Encriptado em repouso, sempre.** Refresh tokens são as joias da coroa: um roubado lê o calendário
   e o e-mail reais do usuário indefinidamente. São encriptados com uma chave que **não** está no
   banco. *(I3.)*
4. **Provedores são configuração mais um adaptador.** Adicionar Microsoft é um client-id, uma entrada
   no catálogo e uma implementação de `IOAuthProvider`. O domínio não muda. *(I4.)*
5. **Uma conexão revogada degrada, nunca quebra.** Um consumidor pedindo token de uma conta revogada
   recebe uma falha tipada (e, uma vez ligada, uma notificação ao usuário), não um stack trace num job
   de background. *(I5.)*

## 4. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Conta externa** | Uma conta de terceiro conectada (`int001`). Guarda as credenciais encriptadas que o Pandora usa em nome do usuário. Identificada por `(user_id, provider, provider_account_id)`. |
| **Provedor** | Um serviço de terceiro ao qual o Pandora se conecta: `google` hoje; `microsoft`, `openai`, `gemini` e outros depois. |
| **Tipo de auth** | Como uma conta autentica: `oauth` (tokens renováveis) ou `api_key` (chave estática do usuário). |
| **OAuth state** | Uma requisição de autorização em andamento (`int002`). O `state` é o token CSRF de uso único; o `code_verifier` do PKCE é guardado encriptado durante o fluxo. |
| **Access token** | Uma credencial OAuth de curta duração, devolvida aos consumidores como um `ExternalAccessToken` transitório (token + expiração + escopos). Nunca persistido pelo consumidor. |
| **Refresh token** | Uma credencial OAuth de longa duração usada só dentro do módulo para emitir novos access tokens. Encriptada em repouso, nunca entregue. |
| **Escopos** | As permissões concedidas, guardadas para detectar que um novo recurso exige re-consentimento. |
| **Secret protector** | O `ISecretProtector` do Tars (AES-GCM) que encripta/decripta cada coluna de credencial, com uma chave vinda de fora do banco. |

## 5. Escopo

### No escopo (implementado — ver [Status de implementação](implementation-status.md))

O schema `integrations` (`int001`, `int002`); o provedor OAuth Google; o ciclo completo
connect → consentimento → callback → armazenamento com PKCE; refresh de token transparente e
serializado; desconexão com revogação no provedor; as portas `IExternalCredentialProvider` /
`IExternalAccountReader`; os endpoints de leitura providers/accounts; e os contratos
`ExternalAccountRevoked` / `ExternalAccountDisconnected`.

### Fora do escopo / futuro (ver [product-plan.md](product-plan.md))

| Recurso | Status |
|---|---|
| **Reação de Channels à revogação** (Telegram "reconectar") | Contratos publicados; ainda sem subscriber/template no Channels (fase I2). |
| **Log de eventos `int003`** | Desenhado, não criado (fase I2). |
| **Endpoints de gestão de chave de API** (registrar/rotacionar/remover) + catálogo `openai`/`gemini` | O caminho de leitura (`GetApiKeyAsync`) existe; ainda não há como criar uma conta `api_key` (fase I3). |
| **Mais provedores** (Microsoft, CalDAV) | Futuro, sob demanda (fase I4). |
