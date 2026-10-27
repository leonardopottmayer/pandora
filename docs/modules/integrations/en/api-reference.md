# API Reference

[← Back to index](../README.md) · Related: [OAuth & Credentials](oauth-and-credentials.md)

Base path: **`/api/v{version}/integrations`**. All endpoints are authenticated and scoped to the
token's user, **except the callback**, which is anonymous and authenticates by the single-use OAuth
`state`. An account id that is not the caller's answers 404, the same as one that does not exist.
Errors are mapped from typed `Result` failures via the shared HTTP error mapper.

---

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/providers` | user | Provider catalog: each provider once, with its auth kind and how many accounts the user has there. |
| GET | `/accounts` | user | The user's accounts, with label, status, scopes, last error and where each is used. |
| GET | `/events` | user | Recent connection event log (connect/refresh-failure/revoke/disconnect), newest first. |
| POST | `/{provider}/connect` | user | Start an OAuth connection (add an account, or reconnect/widen one); returns the consent URL. |
| GET | `/{provider}/callback` | **anonymous** | Provider redirect target; consumes `state`, stores the account, 302s back to the SPA. |
| PUT | `/{provider}/api-key` | user | Add an API key as a new account; returns its id. |
| PUT | `/{provider}/password` | user | Save a username + password (upsert on the username); returns the account id. |
| PUT | `/accounts/{id}/secret` | user | Replace the key or password of one account. |
| PATCH | `/accounts/{id}` | user | Rename one account. |
| DELETE | `/accounts/{id}` | user | Revoke at the provider and delete the connection locally. |

### GET `/providers`

`ProviderCatalogItemDto`: `provider`, `authKind` (`oauth` \| `api-key` \| `password` — tells the SPA
how to add an account), `displayName` (for typed-in providers; null for OAuth), `defaultScopes`, and
`accountCount`.

### GET `/accounts`

`ExternalAccountDto`: `id`, `provider`, `authKind`, `label`, `displayName` (email, username or masked
key), `scopes`, `status`, `lastError`, `connectedAt`, `lastRefreshedAt`, and `usages`
(`[{ module, purpose, detail }]`, collected from every `IExternalAccountUsageSource`; a failing source
is skipped).

### GET `/events?limit=`

Returns the user's recent connection log (`IntegrationEventDto`): `eventType`, `provider`, `detail`,
`occurredAt`. `limit` defaults to 50 and is clamped to 1..100. Backs the "Recent activity" list in
settings — the timeline that answers why a sync stopped.

### POST `/{provider}/connect`

```json
{ "redirectAfter": "/agenda/settings", "scopes": ["...optional override..."], "accountId": "...optional..." }
```

Returns the authorization URL as a bare JSON string — not wrapped in an object. The SPA sends the
browser there. Without `accountId` the provider shows its account chooser and the result is a new (or
reconnected) account; with it, that account is reconnected or its scopes widened (the provider gets a
`login_hint`). An `accountId` that is not the caller's → 404; one at another provider → 422.

### GET `/{provider}/callback?code=&state=`

Anonymous. Validates and consumes the `state`, exchanges the `code` using the stored PKCE verifier,
upserts the encrypted `int001` account **by the returned identity** (`sub`), and 302-redirects to
`redirect_after` in the SPA. When a targeted reconnect came back as another identity, the target is
left untouched and the redirect carries `?integration=other-account`. A missing `code`/`state` or a
failed exchange redirects home with `?integration=error`.

### PUT `/{provider}/api-key`

```json
{ "apiKey": "...", "label": "optional" }
```

Always a new account (a key has no identity to match). The label defaults to
`"<provider name> ••••<last 4>"`. Unknown or non-`api-key` provider, or an empty key → 422.

### PUT `/{provider}/password`

```json
{ "username": "me@example.com", "password": "...", "label": "optional" }
```

Upserts on (user, provider, username): the same username replaces its password, another username is
another account. The label (default: the username) applies only on creation. Missing username or
password → 422.

### PUT `/accounts/{id}/secret` · PATCH `/accounts/{id}`

`{ "secret": "..." }` replaces an `api-key`/`password` secret (an OAuth account → 422: reconnect it
instead). `{ "label": "..." }` renames (blank → 422).

### DELETE `/accounts/{id}`

Revokes the token at the provider, deletes the local `int001` row, and publishes
`ExternalAccountDisconnected`. Synced data owned by consumers is left in place.

See [product-plan.md](product-plan.md) for the roadmap.
