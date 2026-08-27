# API Reference

[← Back to index](../README.md) · Related: [OAuth & Credentials](oauth-and-credentials.md)

Base path: **`/api/v{version}/integrations`**. All endpoints are authenticated and scoped to the
token's user, **except the callback**, which is anonymous and authenticates by the single-use OAuth
`state`. Errors are mapped from typed `Result` failures via the shared HTTP error mapper.

---

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/providers` | user | Provider catalog: name, description, scopes, and whether the user has connected each. |
| GET | `/accounts` | user | The user's connected accounts, with status, scopes and last error. |
| POST | `/{provider}/connect` | user | Start (or re-run) a connection; returns the provider consent URL. |
| GET | `/{provider}/callback` | **anonymous** | Provider redirect target; consumes `state`, stores the account, 302s back to the SPA. |
| DELETE | `/accounts/{id}` | user | Revoke at the provider and delete the connection locally. |

### GET `/providers`

Returns the settings catalog — each provider with its metadata and a `connected` flag.

### GET `/accounts`

Returns connected accounts (`ExternalAccountDto`): provider, display name, status, scopes,
`last_error`, timestamps. Used by the settings section and the "reconnect needed" banner when
`status = revoked`.

### POST `/{provider}/connect`

```json
{ "redirectAfter": "/agenda/settings", "scopes": ["...optional override..."] }
```

Returns `{ "authorizationUrl": "https://accounts.google.com/o/oauth2/..." }`. The SPA sends the
browser there. Re-running for an already-connected provider re-consents (e.g. to widen scopes).

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
