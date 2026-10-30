# Google OAuth and Gemini setup

Runbook for the two Google things Pandora talks to:

- **A Google OAuth client** — lets the user connect a Google account in Integrations. Communications
  uses it for Gmail; Agenda will use it for Calendar.
- **A Gemini API key** — the Assistant's language model, pasted into Integrations.

Each part has the steps to set it up and the steps to undo it. The Google Cloud console in Portuguese
labels things differently, so each console step gives the pt-BR label too.

The first setup was done on 2026-10-29, in the Google Cloud project **Default Gemini Project**
(`gen-lang-client-0090919402`) — the project Google AI Studio created along with the Gemini key.
Both parts live in that project, which matters when undoing (see [Undo](#3-undo)).

---

## 1. Google OAuth client (Gmail)

### 1.1 Google Cloud console

All in [console.cloud.google.com](https://console.cloud.google.com), with the project selected at
the top.

1. **Enable the APIs.** *APIs & Services → Library* (*APIs e serviços → Biblioteca*): enable
   **Gmail API** and **Google Calendar API**. Without them the token works but every call answers 403.
2. **Create the consent screen.** *Google Auth Platform → Overview → Get started* (*Visão geral →
   Começar*):
   1. *App information* (*Informações do app*): app name (`Pandora`) and the support email.
   2. *Audience* (*Público*): **External** (*Externo*). *Internal* only exists for Google Workspace
      organizations, not for a `@gmail.com` account. The app starts in **Testing** mode.
   3. *Contact information* (*Dados de contato*): your email.
   4. *Finish* (*Concluir*): accept the policy, **Create** (*Criar*).
3. **Add yourself as a test user.** *Audience* (*Público-alvo*) → *Test users* (*Usuários de teste*)
   → add the Google account(s) you will connect. Only these can consent while the app is in Testing.
4. **Declare the scopes.** *Data access* (*Acesso a dados*) → *Add or remove scopes* (*Adicionar ou
   remover escopos*), then save:

   | Scope | Why |
   |---|---|
   | `openid`, `.../auth/userinfo.email` | Identify which Google account was connected |
   | `https://www.googleapis.com/auth/calendar`, `.../auth/calendar.events` | Pandora's default scopes (Agenda) |
   | `https://www.googleapis.com/auth/gmail.modify` | Communications: read mail and change labels |

   A scope missing from the list can be pasted in the manual field at the bottom of the dialog.
5. **Create the client.** *Clients* (*Clientes*) → *Create client* (*Criar cliente*) → type **Web
   application** (*Aplicativo da Web*). Under *Authorized redirect URIs* (*URIs de redirecionamento
   autorizados*) add the callback (see [1.2](#12-pandora-configuration) for which one), then copy the
   **Client ID** and the **Client secret**. The secret is shown in full only now; if it is lost, add a
   new one to the same client.

**Testing mode limits.** Google expires the refresh token of an app in Testing after **7 days**, so a
Gmail mailbox goes to "Reconnect" about once a week, and one click on it fixes it. Publishing the app
(*Audience → Publish app*) with `gmail.modify`, a restricted scope, requires Google's security
verification — not worth it for personal use.

### 1.2 Pandora configuration

In `backend/src/Host/Pottmayer.Pandora.Host/appsettings.json`:

```json
"Pandora": {
  "Integrations": {
    "SpaBaseUrl": "",
    "Google": {
      "ClientId": "<client id>.apps.googleusercontent.com",
      "ClientSecret": "<client secret>",
      "RedirectUri": "http://localhost:5173/api/v1/integrations/google/callback"
    }
  }
}
```

The callback must be **byte for byte** the one registered in the client. In local development the
SPA runs on `http://localhost:5173` (Vite) and the API on `https://localhost:61182`, so there are two
ways to set it:

| | `RedirectUri` | `SpaBaseUrl` | Trade-off |
|---|---|---|---|
| **A** (used) | `http://localhost:5173/api/v1/integrations/google/callback` | empty | Vite's proxy sends `/api` to the API, as nginx does in production, so the redirect back lands on the SPA. Breaks if Vite starts on another port. |
| B | `https://localhost:61182/api/v1/integrations/google/callback` | `http://localhost:5173` | No proxy involved; one more setting, and unlike production. |

Restart the backend afterwards: the Google provider is registered at startup only when `ClientId`
is set. That is why Gmail shows greyed out in *Add mailbox* until then.

`appsettings.json` also holds the Telegram bot tokens: **never commit the client secret**. When
committing that file, stage only the hunks that belong to the change.

**Homelab / production.** Add the production callback
`https://<domain>/api/v1/integrations/google/callback` to the same client (a client takes several
redirect URIs), leave `SpaBaseUrl` empty (nginx serves SPA and API on one origin), and pass the values
as environment variables in the compose file: `Pandora__Integrations__Google__ClientId`,
`Pandora__Integrations__Google__ClientSecret`, `Pandora__Integrations__Google__RedirectUri`.

### 1.3 Check

1. Open `/mail/accounts` → **Add mailbox** (*Adicionar caixa*): Gmail is enabled.
2. **Connect a new Google account** (*Conectar outra conta Google*) → pick the account on Google's
   screen → allow. This button asks for every scope at once.
3. Back on Pandora the modal reopens on Gmail with the account selected → **Connect**. The progress
   bar starts moving within a cycle.

---

## 2. Gemini API key (Assistant)

The personal seed (`migrations/seed/seed.local.sql`) already brings the key and the Assistant
profile. Its encrypted key only opens with the same `Tars:Security:DataProtection` key in
`appsettings.json`. If that key changed, or on a database without the seed, do this:

1. **Create the key.** [aistudio.google.com/apikey](https://aistudio.google.com/apikey) → *Create API
   key*. AI Studio puts it in a Google Cloud project — creating the first one is what made "Default
   Gemini Project". Copy the key.
2. **Store it in Pandora.** `/integrations/connections` (*Conexões*) → **Google Gemini** → **Add
   account** (*Adicionar conta*) → paste the key (the label is optional; it defaults to
   `Google Gemini ••••XXXX`). The key is encrypted at rest; the screen only ever shows the mask.
3. **Point the Assistant at it.** `/settings/assistant` (*Assistente*):
   - *Key* (*Chave*): the account just added.
   - *Model* (*Modelo*): e.g. `gemini-3.6-flash`.
   - *Confirmation* (*Confirmação*): how readily it acts without asking (*Equilibrado* is the default).
   - Turn on *Assistant enabled* (*Assistente ativado*), save, and run the reach test, which calls the
     model once to check the key and the model.

The Assistant answers on the Telegram chat linked in Channels; the texts you send it go to Google.

---

## 3. Undo

The order matters: first stop Pandora from using the credential, then revoke it at Google, then
remove the configuration.

### 3.1 Gmail and the OAuth client

1. **Pandora:**
   1. `/mail/accounts` → **Remove** (*Remover*) each Gmail mailbox. This deletes Pandora's copy of the
      messages; nothing changes in Gmail.
   2. `/integrations/connections` → **Disconnect** (*Desconectar*) the Google account.
   3. In `appsettings.json`, empty `ClientId`, `ClientSecret` (and restore `RedirectUri` /
      `SpaBaseUrl` if they were changed), then restart the backend. Gmail goes back to greyed out.
2. **Your Google account:** [myaccount.google.com/permissions](https://myaccount.google.com/permissions)
   → the Pandora app → *Remove all access*. This revokes the grant even if step 1 was skipped.
3. **Google Cloud console:**
   1. *Google Auth Platform → Clients* → delete the Pandora client. Every token issued to it stops
      working.
   2. *Data access* → remove the scopes added in [1.1](#11-google-cloud-console); *Audience* → remove
      the test users.
   3. *APIs & Services → Enabled APIs* → **Gmail API** → *Disable* (and Google Calendar API, if
      nothing else uses it).

   The consent screen itself has no delete button. It is harmless without a client; it only goes
   away with the whole project.

### 3.2 Gemini

1. `/settings/assistant` → turn the Assistant off.
2. `/integrations/connections` → **Disconnect** the Google Gemini account.
3. [aistudio.google.com/apikey](https://aistudio.google.com/apikey) → delete the key.

### 3.3 Everything at once

Deleting the Google Cloud project (*IAM & Admin → Settings → Shut down*) removes the OAuth client,
the consent screen, the enabled APIs **and the Gemini key**, since they all live in "Default Gemini
Project". Do 3.1 step 1 and 3.2 steps 1–2 in Pandora first, so it does not keep calling dead
credentials.
