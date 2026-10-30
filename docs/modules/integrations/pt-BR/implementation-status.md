# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído no código versus o que está desenhado mas ainda não implementado. O
roadmap à frente fica no [product-plan.md](product-plan.md).

---

## Implementado

| Área | Notas |
|---|---|
| **Estrutura do módulo** | Sete projetos em camadas; schema `integrations`; `int001`, `int002`, `int003`. |
| **Domínio** | Agregados `ExternalAccount` + `OAuthState`; objetos de valor `AccountStatus`, `AuthKind` (`oauth`/`api-key`/`password`); `IntegrationErrors`. |
| **OAuth (Google)** | `GoogleOAuthProvider` (`BuildAuthorizationUrl` com `prompt=select_account consent` e `login_hint` opcional, `ExchangeCodeAsync`, `RefreshAsync`, `RevokeAsync`), resolvido via `OAuthProviderRegistry`. |
| **Fluxo de autorização** | `StartConnection` → URL de consentimento com PKCE, opcionalmente mirando uma conta; `HandleCallback` consome o `state` de uso único, troca o code, faz upsert pelo `sub` devolvido, nunca sobrescreve uma conta-alvo com outra identidade. |
| **Refresh transparente** | `GetAccessTokenAsync` renova perto da expiração, serializado por conta com um **gate in-process**; `MarkRevoked` + `ExternalAccountRevoked` (com o nome da conta) no `invalid_grant`. |
| **Credenciais digitadas (I3, I5)** | Catálogo `CredentialProviderDescriptor` (`gemini` api-key; `titan`, `imap` password). `SaveApiKey` (sempre conta nova), `SavePassword` (upsert pelo usuário), `ReplaceSecret`, `RenameAccount`. |
| **Várias contas por provedor (I5)** | Coluna `label`; toda chamada da porta por id da conta com checagem de dono (`NotConnected` para inexistente ou alheia); `IExternalAccountReader.GetAsync(userId, accountId)`; o catálogo conta as contas por provedor. |
| **Visão de uso (I5)** | `IExternalAccountUsageSource` (implementada hoje pelo Assistant, `assistant`/`chat`, e pelo Communications, `communications`/`mail`); `GET /accounts` devolve `usages`, pulando uma fonte que falha. |
| **Desconectar** | `DisconnectAccount` revoga no provedor, apaga localmente, publica `ExternalAccountDisconnected`. |
| **Contratos** | `ExternalAccountRevoked` (`Label` acrescentado, anulável), `ExternalAccountDisconnected`. O Channels transforma o primeiro no aviso `integrations.account-revoked` ("Google · Pessoal"). |
| **Encriptação** | Toda coluna de credencial via `ISecretProtector` do Tars (AES-GCM, chave fora do banco). |
| **Log de eventos (I2)** | `int003_integration_event_log` — append-only `connected`/`reconnected`/`refresh-failed`/`expired`/`revoked`/`disconnected`, cada linha escrita na mesma transação da mudança que registra. |
| **API** | `GET /providers`, `GET /accounts`, `GET /events`, `POST /{provider}/connect`, `GET /{provider}/callback`, `PUT /{provider}/api-key`, `PUT /{provider}/password`, `PUT /accounts/{id}/secret`, `PATCH /accounts/{id}`, `DELETE /accounts/{id}`. |
| **Frontend** | Tela de conexões agrupada por provedor (renomear no lugar, adicionar conta, trocar segredo, reconectar, desconectar listando os usos) + **Atividade recente**; o seletor compartilhado `AccountSelect` (`modules/integrations/components`), usado nas configurações do Assistant e nas telas de email. |
| **Testes** | Domínio (`ExternalAccountApiKeyTests`); integração `ApiKeyTests`, `AccountsTests`, `OAuthCallbackTests` (Google falso); front `accountSelection.test.ts`. |

### Desvios notáveis do plano original

- **Serialização do refresh** usa um **gate in-process**, não `pg_advisory_xact_lock` (monólito de
  processo único).
- **Sem endpoint `reconnect`** — `connect` com `accountId` reconecta ou amplia aquela conta.
- **Refreshes bem-sucedidos não são logados** no `int003`: `int001.last_refreshed_at` já registra o
  último sucesso, então o log guarda as falhas e os eventos de ciclo de vida e pula o ruído horário.
- **`password` faz upsert, `api-key` não** (a I5 dizia que os dois criam): um usuário é uma identidade,
  então a mesma caixa salva duas vezes é uma conta só; uma chave não tem identidade.
- **As descrições `titan` / `imap`** são registradas aqui, junto com `gemini`, e não pelo
  Communications — todo provedor digitado vive num catálogo só.

## Ainda não implementado (desenhado / planejado)

| Área | Status | Fase |
|---|---|---|
| **Provedor `openai`** | A um `CredentialProviderDescriptor` de distância. | I3 |
| **Mais provedores** (Microsoft, CalDAV) | Futuro, sob demanda. | I4 |

## Pontos em aberto conhecidos

1. **Gate in-process vs. advisory lock** — ok para o monólito de processo único; revisitar se o host
   escalar horizontalmente.
