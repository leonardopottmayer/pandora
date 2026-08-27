# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [OAuth e Credenciais](oauth-and-credentials.md)

---

## 1. Organização dos projetos

O módulo espelha os demais módulos do Pandora, dividido em projetos por camada sob
`backend/src/Modules/Integrations/`:

```
Pottmayer.Pandora.Modules.Integrations.
  Abstractions      → portas públicas para outros módulos: IExternalCredentialProvider,
                      IExternalAccountReader, ExternalAccessToken, ExternalAccountSummary,
                      registro IntegrationsModule, IntegrationsOptions
  Application       → Commands (StartConnection, HandleCallback, DisconnectAccount),
                      Queries (GetProviders, GetAccounts), os serviços OAuth
                      (ExternalCredentialProvider, ExternalAccountReader, OAuthProviderRegistry,
                      PkceCodes, ScopeString), DTOs, DI
  Contracts         → IntegrationEvents: ExternalAccountRevoked, ExternalAccountDisconnected
  Domain            → Aggregates (ExternalAccount, OAuthState), ValueObjects (AccountStatus,
                      AuthKind), Ports (IOAuthProvider, repositórios), Errors
  Infrastructure    → Adaptadores de provedor (Google), DI
  Persistence       → EntityConfigs, Repositories, DbContext, DI
  Presentation      → IntegrationsController, DI
```

Estilo de design: **agregados DDD** com construtores privados + factories estáticas, um `TimeProvider`
injetado para toda leitura de tempo, e uma camada de aplicação **command/query** (uma pasta por caso
de uso).

## 2. Blocos de domínio

### Agregados (`Domain/Aggregates`)

| Raiz de agregado | Responsabilidade / invariantes-chave |
|---|---|
| **ExternalAccount** | Uma conta conectada. Guarda credenciais encriptadas; transita entre `connected`/`expired`/`revoked`/`needs_consent`; `MarkRevoked` é a degradação terminal num refresh rejeitado. Única por `(user_id, provider, provider_account_id)`. |
| **OAuthState** | Uma requisição de autorização em andamento. Carrega o `state` CSRF e o verifier PKCE encriptado; uso único, com TTL; consumida exatamente uma vez pelo callback. |

### Objetos de valor (`Domain/ValueObjects`)

- **`AccountStatus`** — `connected` \| `expired` \| `revoked` \| `needs_consent`.
- **`AuthKind`** — `oauth` (tokens renováveis, Google) \| `api_key` (chave estática do usuário, OpenAI/Gemini).

### Portas (`Domain/Ports`)

- **`IOAuthProvider`** — uma por provedor: `BuildAuthorizationUrl`, `ExchangeCodeAsync`, `RefreshAsync`,
  `RevokeAsync`. Resolvida por nome via `OAuthProviderRegistry`.
- **Repositórios:** `IExternalAccountRepository`, `IOAuthStateRepository`.

### Portas publicadas (`Abstractions/Ports`)

A única superfície que outros módulos referenciam:

```csharp
public interface IExternalCredentialProvider
{
    // auth_kind = oauth — renova invisivelmente
    Task<Result<ExternalAccessToken>> GetAccessTokenAsync(Guid userId, string provider, CancellationToken ct = default);
    // auth_kind = api_key — decripta e devolve; sem expiração, sem refresh
    Task<Result<string>> GetApiKeyAsync(Guid userId, string provider, CancellationToken ct = default);
}

public interface IExternalAccountReader
{
    Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<ExternalAccountSummary?> GetAsync(Guid externalAccountId, CancellationToken ct = default);
}
```

`ExternalAccessToken` carrega a string do token, sua expiração e os escopos concedidos. É um valor
transitório — nunca persistido pelo consumidor, nunca logado.

### Vindo do Tars

`Pottmayer.Tars.Security.DataProtection` fornece `ISecretProtector` (AES-GCM sobre uma chave da
configuração/secret store): `Protect(string) → ciphertext`, `Unprotect(string) → plaintext`. Toda
coluna de credencial passa por ele.

## 3. Decisões de design

| # | Decisão | Racional (alternativa rejeitada) |
|---|---|---|
| **I1** | Consumidores recebem um access token de curta duração por uma porta; não há como ler um refresh token para fora do módulo. | Um refresh token num consumidor é uma segunda cópia das joias da coroa. |
| **I2** | Refresh é transparente dentro de `GetAccessTokenAsync` e **serializado por conta com um gate in-process** (não um advisory lock do Postgres). Dois jobs de sync concorrentes não podem queimar dois refreshes — alguns provedores rotacionam e invalidam o refresh anterior. | O monólito roda um processo, então um gate in-process basta e é mais barato que ida ao banco. Reavaliar se o host escalar horizontalmente. |
| **I3** | Credenciais são encriptadas em repouso com uma chave vinda de **fora** do banco (`ISecretProtector`). | Um dump do banco sozinho não pode render credenciais Google funcionais. |
| **I4** | Um provedor é uma entrada de config + um adaptador `IOAuthProvider`, resolvido por nome. | Adicionar Microsoft não toca no domínio. |
| **I5** | Um refresh rejeitado (`invalid_grant`) marca a conta como `revoked`, publica `ExternalAccountRevoked` e devolve falha tipada. | Uma falha de background precisa degradar de forma limpa, não entrar em loop nem quebrar. |

## 4. Regras transversais

- **Multi-tenant por usuário.** Toda tabela tem `user_id NOT NULL`; todo endpoint autenticado é escopo
  do usuário do token.
- **O callback é o único endpoint anônimo.** Autentica pelo `state` de uso único que ele mesmo emitiu —
  nunca por sessão.
- **`TimeProvider` em todo lugar.** Expiração de token e TTL do state são calculados contra o tempo
  injetado.
