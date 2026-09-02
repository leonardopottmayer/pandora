# API Reference

[← Back to index](../README.md) · Related: [OAuth & Credentials](oauth-and-credentials.md)

Base path: **`/api/v{version}/integrations`**. All endpoints are authenticated and scoped to the
token's user, **except the callback**, which is anonymous and authenticates by the single-use OAuth
`state`. Errors are mapped from typed `Result` failures via the shared HTTP error mapper.

---

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/providers` | user | Provider catalog: name, default scopes, and whether the user has connected each (with status). |
| GET | `/accounts` | user | The user's connected accounts, with status, scopes and last error. |
| GET | `/events` | user | Recent connection event log (connect/refresh-failure/revoke/disconnect), newest first. |
| POST | `/{provider}/connect` | user | Start (or re-run) a connection; returns the provider consent URL. |
| GET | `/{provider}/callback` | **anonymous** | Provider redirect target; consumes `state`, stores the account, 302s back to the SPA. |
| DELETE | `/accounts/{id}` | user | Revoke at the provider and delete the connection locally. |

### GET `/providers`

Returns the settings catalog (`ProviderCatalogItemDto`): `provider`, `defaultScopes`, `connected`, and
`status` (the connected account's status, or `null` when not connected). There is no `description`
field — the SPA labels a provider from its key (see `providerLabel` in `client-web`).

### GET `/accounts`

Returns connected accounts (`ExternalAccountDto`): `id`, `provider`, `authKind`, `displayName`,
`scopes`, `status`, `lastError`, `connectedAt`, `lastRefreshedAt`. Used by the settings section and
the "reconnect needed" banner when `status = revoked`.

### GET `/events?limit=`

Returns the user's recent connection log (`IntegrationEventDto`): `eventType`, `provider`, `detail`,
`occurredAt`. `limit` defaults to 50 and is clamped to 1..100. Backs the "Recent activity" list in
settings — the timeline that answers why a sync stopped.

### POST `/{provider}/connect`

```json
{ "redirectAfter": "/agenda/settings", "scopes": ["...optional override..."] }
```

Returns the authorization URL as a bare JSON string, e.g. `"https://accounts.google.com/o/oauth2/..."`
— not wrapped in an object. The SPA sends the browser there. Re-running for an already-connected
provider re-consents (e.g. to widen scopes).

### GET `/{provider}/callback?code=&state=`

Anonymous. Validates and consumes the `state`, exchanges the `code` using the stored PKCE verifier,
upserts the encrypted `int001` account, and 302-redirects to `redirect_after` in the SPA. A missing
`code`/`state` or a failed exchange redirects home with `?integration=error`.

### DELETE `/accounts/{id}`

Revokes the token at the provider, deletes the local `int001` row, and publishes
`ExternalAccountDisconnected`. Synced data owned by consumers is left in place.

---

## Not yet implemented

| Planned endpoint | Phase |
|---|---|
| `POST /accounts/{id}/api-key` (register/rotate an API key) | I3 |
| `openai` / `gemini` in `/providers` (key form + reachability test) | I3 |

See [product-plan.md](product-plan.md) for the roadmap.
