# Referência de API

[← Voltar ao índice](README.md) · Relacionados: [OAuth e Credenciais](oauth-and-credentials.md)

Caminho base: **`/api/v{version}/integrations`**. Todos os endpoints são autenticados e escopo do
usuário do token, **exceto o callback**, que é anônimo e autentica pelo `state` OAuth de uso único.
Erros vêm de falhas tipadas `Result` mapeadas pelo error mapper HTTP compartilhado.

---

## Endpoints

| Método | Caminho | Auth | Propósito |
|---|---|---|---|
| GET | `/providers` | usuário | Catálogo de provedores: nome, descrição, escopos e se o usuário conectou cada um. |
| GET | `/accounts` | usuário | Contas conectadas do usuário, com status, escopos e último erro. |
| POST | `/{provider}/connect` | usuário | Inicia (ou refaz) uma conexão; devolve a URL de consentimento. |
| GET | `/{provider}/callback` | **anônimo** | Alvo de redirect do provedor; consome `state`, guarda a conta, 302 de volta à SPA. |
| DELETE | `/accounts/{id}` | usuário | Revoga no provedor e apaga a conexão localmente. |

### GET `/providers`

Devolve o catálogo de configurações — cada provedor com seus metadados e um flag `connected`.

### GET `/accounts`

Devolve as contas conectadas (`ExternalAccountDto`): provider, display name, status, escopos,
`last_error`, timestamps. Usado pela seção de configurações e pelo banner "reconectar" quando
`status = revoked`.

### POST `/{provider}/connect`

```json
{ "redirectAfter": "/agenda/settings", "scopes": ["...override opcional..."] }
```

Devolve `{ "authorizationUrl": "https://accounts.google.com/o/oauth2/..." }`. A SPA manda o navegador
para lá. Rodar de novo para um provedor já conectado refaz o consentimento (p.ex. para ampliar escopos).

### GET `/{provider}/callback?code=&state=`

Anônimo. Valida e consome o `state`, troca o `code` usando o verifier PKCE guardado, faz upsert da
conta `int001` encriptada e faz 302 para o `redirect_after` na SPA. Um `code`/`state` ausente ou uma
troca que falha redireciona para a home com `?integration=error`.

### DELETE `/accounts/{id}`

Revoga o token no provedor, apaga a linha `int001` local e publica `ExternalAccountDisconnected`. Os
dados sincronizados de consumidores são deixados no lugar.

---

## Ainda não implementado

| Endpoint planejado | Fase |
|---|---|
| `POST /accounts/{id}/api-key` (registrar/rotacionar uma chave) | I3 |
| `openai` / `gemini` em `/providers` (formulário de chave + teste de alcance) | I3 |

Ver [product-plan.md](product-plan.md) para o roadmap.
