# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído no código versus o que está desenhado mas ainda não implementado. O
roadmap adiante fica em [product-plan.md](product-plan.md).

---

## Implementado (fase I1 — Core)

| Área | Notas |
|---|---|
| **Scaffold do módulo** | Sete projetos por camada; schema `integrations`; `int001`, `int002`. |
| **Domínio** | Agregados `ExternalAccount` + `OAuthState`; objetos de valor `AccountStatus`, `AuthKind`; `IntegrationErrors`. |
| **OAuth (Google)** | `GoogleOAuthProvider` (`BuildAuthorizationUrl`, `ExchangeCodeAsync`, `RefreshAsync`, `RevokeAsync`), resolvido via `OAuthProviderRegistry`. |
| **Fluxo de autorização** | `StartConnection` → URL de consentimento com PKCE; `HandleCallback` consome o `state` de uso único, troca o code, faz upsert da conta encriptada. |
| **Refresh transparente** | `ExternalCredentialProvider.GetAccessTokenAsync` renova perto da expiração, serializado por conta com um **gate in-process**; `MarkRevoked` + `ExternalAccountRevoked` em `invalid_grant`. |
| **Leitura de chave de API** | `GetApiKeyAsync` decripta e devolve a chave de uma conta `api_key`. |
| **Desconexão** | `DisconnectAccount` revoga no provedor, apaga localmente, publica `ExternalAccountDisconnected`. |
| **Portas** | `IExternalCredentialProvider`, `IExternalAccountReader`, `ExternalAccessToken`, `ExternalAccountSummary` em `Abstractions`. |
| **Contratos** | `ExternalAccountRevoked`, `ExternalAccountDisconnected` publicados. |
| **Encriptação** | Toda coluna de credencial via `ISecretProtector` do Tars (AES-GCM, chave fora do banco). |
| **API** | `GET /providers`, `GET /accounts`, `POST /{provider}/connect`, `GET /{provider}/callback`, `DELETE /accounts/{id}`. |
| **Frontend** | Seção de configurações de contas conectadas em `client-web/src/modules/integrations`. |

### Desvios notáveis do plano original

- **Serialização do refresh** usa um **gate in-process**, não `pg_advisory_xact_lock` (monólito de
  processo único).
- **Sem endpoint `reconnect`** — rodar `connect` de novo refaz o consentimento e amplia escopos.
- Os contratos (`ExternalAccountRevoked`/`ExternalAccountDisconnected`) já existem, à frente da fiação
  da fase I2 no Channels que os consome.

## Ainda não implementado (desenhado / planejado)

| Área | Status | Fase |
|---|---|---|
| **Reação de Channels à revogação** | Contratos publicados; sem subscriber no Channels nem template "reconectar" no Telegram. | I2 |
| **Log de eventos `int003`** | Desenhado, tabela não criada. | I2 |
| **Endpoints de gestão de chave de API** | `GetApiKeyAsync` existe, mas não há endpoint para registrar/rotacionar/remover chave, então nenhuma conta `api_key` pode ser criada ainda. | I3 |
| **Provedores `openai` / `gemini`** | Fora do catálogo; sem formulário de chave nem teste de alcance. | I3 |
| **Mais provedores** (Microsoft, CalDAV) | Futuro, sob demanda. | I4 |

## Pontos em aberto conhecidos

1. **Gate in-process vs. advisory lock** — suficiente para o monólito de processo único; reavaliar se o
   host escalar horizontalmente.
2. **Multi-conta por provedor** — a constraint única já permite duas contas Google; se as bindings de um
   consumidor podem cruzá-las é decisão do consumidor.
