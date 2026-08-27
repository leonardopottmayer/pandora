# OAuth e Credenciais

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. Fluxo de autorização

Fluxo authorization-code do lado do servidor com **PKCE**. A SPA nunca vê um token.

```
1. SPA        → POST /integrations/google/connect { redirectAfter, scopes? }
                ← { authorizationUrl }               (int002 state + verifier encriptado persistidos)
2. Navegador  → tela de consentimento do provedor
3. Provedor   → GET /integrations/google/callback?code=&state=
4. Backend    → valida e consome o state (uso único), troca o code com o verifier,
                faz upsert em int001 (tokens encriptados), 302 para redirect_after na SPA
5. SPA        → GET /integrations/accounts          ← mostra a conexão como ativa
```

O callback (`GET /{provider}/callback`) é o único endpoint **anônimo**; autentica pelo `state` de uso
único que ele mesmo emitiu. Um `code`/`state` ausente ou em branco, ou um state já consumido ou
expirado, redireciona para a home com um resultado de erro em vez de lançar exceção.

**Re-consentimento / escopos ampliados.** Rodar `connect` de novo para um provedor já conectado refaz
o consentimento e atualiza os `scopes` guardados — é assim que um recurso futuro (p.ex. ativar sync de
tarefas) amplia permissões. Não há endpoint `reconnect` separado; `connect` cobre isso.

### Especificidades do Google

- `access_type=offline` e `prompt=consent` no primeiro connect, para realmente receber um refresh token.
- Escopos: escopos de calendário para o sync de calendário do Agenda; escopos de tarefas adicionados
  quando o sync de tarefas é ativado.
- Um projeto no Google Cloud com a tela de consentimento OAuth configurada é um **pré-requisito de
  deployment** (client id/secret via `GoogleOAuthOptions`), documentado em `docs/deployment/`.

## 2. Refresh transparente

`GetAccessTokenAsync` é o caminho quente — um job de sync o chama a cada poucos minutos:

1. Se o access token em cache ainda tem margem antes de expirar, devolve-o.
2. Caso contrário, **serializa o refresh por conta com um gate in-process** (não um advisory lock do
   Postgres), relê, e só renova se ainda for necessário. Dois chamadores concorrentes não podem queimar
   dois refreshes — alguns provedores invalidam o refresh anterior na rotação.
3. Persiste o novo par, encriptado; atualiza `last_refreshed_at`.
4. Em `invalid_grant`, chama `MarkRevoked`, publica `ExternalAccountRevoked` e devolve falha tipada
   (`IntegrationErrors.AccountRevoked`) — nunca uma exceção no job do chamador.

> **Nota de design.** O plano originalmente pedia um `pg_advisory_xact_lock`. A implementação usa um
> gate in-process porque o monólito roda como processo único; reavaliar se o host escalar horizontalmente.

## 3. Encriptação em repouso

Toda coluna de credencial (`access_token_enc`, `refresh_token_enc`, `code_verifier_enc`) é escrita
por `ISecretProtector` (Tars `Security.DataProtection`, AES-GCM). A chave é lida da configuração — uma
variável de ambiente no Docker, um secret montado no homelab — **nunca do banco**. Essa separação é o
ponto inteiro: um dump do banco sozinho não pode render credenciais funcionais.

## 4. Chaves de API (`auth_kind = api_key`)

Para provedores que autenticam com uma chave estática do usuário (OpenAI, Gemini), uma conta é
guardada com `auth_kind = api_key`, a chave em `access_token_enc` encriptada e sem refresh token.
`GetApiKeyAsync(userId, provider)` decripta e devolve; falha com `NotAnApiKey` se a conta não for de
chave de API.

> **Status.** O caminho de leitura (`GetApiKeyAsync`) está implementado. Os endpoints para *registrar /
> rotacionar / remover* uma chave de API, e as entradas de catálogo `openai`/`gemini`, são a fase I3 —
> ver [product-plan.md](product-plan.md).

## 5. Revogação e desconexão

- **Desconectar** (`DELETE /accounts/{id}`) revoga o token no provedor (`RevokeAsync`) e então apaga a
  conta local, publicando `ExternalAccountDisconnected`. Um consumidor (Agenda) deve desativar as
  bindings que a usavam **deixando os dados sincronizados no lugar** — desconectar o Google não pode
  apagar os eventos do usuário.
- **Revogação no provedor** (o usuário revoga o acesso na página da conta Google) aparece como um
  `invalid_grant` no próximo refresh → `status = revoked` + `ExternalAccountRevoked`.

Ambos os contratos (`ExternalAccountRevoked`, `ExternalAccountDisconnected`) são **publicados hoje**. O
subscriber do Channels que transforma uma revogação numa mensagem "reconectar" no Telegram é a fase I2.
