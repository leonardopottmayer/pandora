# Referência de API

[← Voltar ao índice](README.md) · Relacionados: [OAuth e Credenciais](oauth-and-credentials.md)

Caminho base: **`/api/v{version}/integrations`**. Todos os endpoints são autenticados e escopo do
usuário do token, **exceto o callback**, que é anônimo e autentica pelo `state` OAuth de uso único. Um
id de conta que não é de quem chama responde 404, igual a um que não existe. Erros vêm de falhas
tipadas `Result` mapeadas pelo error mapper HTTP compartilhado.

---

## Endpoints

| Método | Caminho | Auth | Propósito |
|---|---|---|---|
| GET | `/providers` | usuário | Catálogo de provedores: cada provedor uma vez, com o auth kind e quantas contas o usuário tem nele. |
| GET | `/accounts` | usuário | As contas do usuário, com nome, status, escopos, último erro e onde cada uma é usada. |
| GET | `/events` | usuário | Log recente de eventos de conexão (conectar/falha de refresh/revogar/desconectar), mais novos primeiro. |
| POST | `/{provider}/connect` | usuário | Inicia uma conexão OAuth (adicionar conta, ou reconectar/ampliar uma); devolve a URL de consentimento. |
| GET | `/{provider}/callback` | **anônimo** | Destino do redirect do provedor; consome o `state`, guarda a conta, 302 de volta para a SPA. |
| PUT | `/{provider}/api-key` | usuário | Adiciona uma chave de API como conta nova; devolve o id. |
| PUT | `/{provider}/password` | usuário | Salva usuário + senha (upsert pelo usuário); devolve o id da conta. |
| PUT | `/accounts/{id}/secret` | usuário | Troca a chave ou a senha de uma conta. |
| PATCH | `/accounts/{id}` | usuário | Renomeia uma conta. |
| DELETE | `/accounts/{id}` | usuário | Revoga no provedor e apaga a conexão localmente. |

### GET `/providers`

`ProviderCatalogItemDto`: `provider`, `authKind` (`oauth` \| `api-key` \| `password` — diz à SPA como
adicionar uma conta), `displayName` (para provedores digitados; nulo no OAuth), `defaultScopes` e
`accountCount`.

### GET `/accounts`

`ExternalAccountDto`: `id`, `provider`, `authKind`, `label`, `displayName` (e-mail, usuário ou chave
mascarada), `scopes`, `status`, `lastError`, `connectedAt`, `lastRefreshedAt` e `usages`
(`[{ module, purpose, detail }]`, coletados de toda `IExternalAccountUsageSource`; uma fonte que falha
é pulada).

### GET `/events?limit=`

Devolve o log recente de conexão do usuário (`IntegrationEventDto`): `eventType`, `provider`,
`detail`, `occurredAt`. `limit` padrão 50, limitado a 1..100. Alimenta a lista "Atividade recente" nas
configurações — a linha do tempo que responde por que uma sincronização parou.

### POST `/{provider}/connect`

```json
{ "redirectAfter": "/agenda/settings", "scopes": ["...override opcional..."], "accountId": "...opcional..." }
```

Devolve a URL de autorização como string JSON pura — sem objeto em volta. A SPA manda o navegador para
lá. Sem `accountId` o provedor mostra a escolha de conta e o resultado é uma conta nova (ou
reconectada); com ele, aquela conta é reconectada ou tem os escopos ampliados (o provedor recebe um
`login_hint`). Um `accountId` que não é de quem chama → 404; de outro provedor → 422.

### GET `/{provider}/callback?code=&state=`

Anônimo. Valida e consome o `state`, troca o `code` usando o verifier PKCE guardado, faz upsert da conta
`int001` encriptada **pela identidade devolvida** (`sub`) e redireciona (302) para o `redirect_after` na
SPA. Quando uma reconexão com alvo volta como outra identidade, o alvo fica intacto e o redirect leva
`?integration=other-account`. `code`/`state` ausente ou troca falha redireciona para a home com
`?integration=error`.

### PUT `/{provider}/api-key`

```json
{ "apiKey": "...", "label": "opcional" }
```

Sempre uma conta nova (uma chave não tem identidade para casar). O nome padrão é
`"<nome do provedor> ••••<últimos 4>"`. Provedor desconhecido ou que não é `api-key`, ou chave vazia → 422.

### PUT `/{provider}/password`

```json
{ "username": "eu@exemplo.com", "password": "...", "label": "opcional" }
```

Upsert por (usuário, provedor, usuário da caixa): o mesmo usuário troca a senha, outro usuário é outra
conta. O nome (padrão: o usuário) só vale na criação. Usuário ou senha ausente → 422.

### PUT `/accounts/{id}/secret` · PATCH `/accounts/{id}`

`{ "secret": "..." }` troca o segredo de uma conta `api-key`/`password` (uma conta OAuth → 422:
reconecte). `{ "label": "..." }` renomeia (vazio → 422).

### DELETE `/accounts/{id}`

Revoga o token no provedor, apaga a linha `int001` local e publica `ExternalAccountDisconnected`. Os
dados sincronizados que pertencem aos consumidores ficam no lugar.

Ver [product-plan.md](product-plan.md) para o roadmap.
