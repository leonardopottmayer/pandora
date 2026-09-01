# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [OAuth e Credenciais](oauth-and-credentials.md)

Schema PostgreSQL **`integrations`**. Convenções: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`
para timestamps, constraints nomeadas (`pk_intXXX`, `uq_intXXX_*`, `chk_intXXX_*`), enums como
`VARCHAR` + `CHECK`. Toda tabela por usuário tem `user_id NOT NULL`. Toda coluna de credencial é
guardada **encriptada** (`*_enc`) via `ISecretProtector`.

As migrations ficam em `migrations/migrations/integrations/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| int001 | `external_account` | Uma conta de terceiro conectada + suas credenciais encriptadas |
| int002 | `oauth_state` | Uma requisição de autorização em andamento (state CSRF + verifier PKCE) |
| int003 | `integration_event_log` | Histórico append-only da saúde de uma conexão (conexões, falhas de refresh, revogações, desconexões) |

---

## int001_external_account

Uma conta de terceiro conectada. Guarda as credenciais que o Pandora usa em nome do usuário,
encriptadas em repouso com uma chave que vive fora do banco.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | dono |
| `provider` | varchar(40) NOT NULL | `google` hoje; `microsoft`, `openai`, `gemini`, … depois |
| `auth_kind` | varchar(20) NOT NULL | `oauth \| api_key` |
| `provider_account_id` | varchar(255) NOT NULL | id de sujeito estável do provedor; para `api_key`, um rótulo escolhido pelo usuário |
| `display_name` | varchar(255) NULL | e-mail/handle da conta, exibido em configurações |
| `scopes` | text NOT NULL DEFAULT '' | escopos concedidos como guardados; usado para detectar re-consentimento |
| `access_token_enc` | text NULL | encriptado; curta duração (também guarda a chave de API no caso `api_key`) |
| `access_token_expires_at` | timestamptz NULL | |
| `refresh_token_enc` | text NULL | encriptado; nulo quando o provedor não emite |
| `status` | varchar(20) NOT NULL | `connected \| expired \| revoked \| needs_consent` |
| `connected_at` | timestamptz NOT NULL | |
| `last_refreshed_at` | timestamptz NULL | |
| `last_error` | text NULL | último erro de refresh/revogação, exposto em configurações |
| `created_by/created_at/updated_by/updated_at` | | colunas de auditoria |

Constraints: `pk_int001`, `chk_int001_auth_kind (oauth|api_key)`,
`chk_int001_status (connected|expired|revoked|needs_consent)`,
`uq_int001_user_provider_account (user_id, provider, provider_account_id)` — uma conta por
(usuário, provedor, conta), então duas contas Google já estão modeladas pelo `provider_account_id`
discriminante.

## int002_oauth_state

Uma requisição de autorização em andamento. O callback autentica consumindo exatamente o state que
emitiu: uso único, curta duração. O verifier PKCE fica encriptado durante o fluxo.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | quem iniciou o fluxo |
| `provider` | varchar(40) NOT NULL | |
| `state` | varchar(255) NOT NULL | o token CSRF — **único**, uso único |
| `code_verifier_enc` | text NOT NULL | verifier PKCE, encriptado |
| `redirect_after` | varchar(500) NOT NULL | para onde devolver o navegador na SPA |
| `expires_at` | timestamptz NOT NULL | TTL ~10 minutos |
| `consumed_at` | timestamptz NULL | preenchido no primeiro uso; um segundo callback com o mesmo state falha |

Constraints: `pk_int002`, `uq_int002_state (state)` — um callback resolve para exatamente uma
requisição.

## int003_integration_event_log

Um registro append-only da saúde de uma conexão. Responde "por que o sync parou três dias atrás": os
tipos de falha carregam o motivo, os tipos de ciclo de vida carregam a mudança. As linhas são escritas
e lidas, nunca alteradas.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | `uuid_generate_v7()` |
| `user_id` | uuid NOT NULL | escopa a leitura; mantido mesmo após a conta ser deletada |
| `external_account_id` | uuid NULL | a linha `int001` que isto concerne — **sem FK**, então o log sobrevive a uma desconexão que deleta a conta |
| `provider` | varchar(40) NOT NULL | |
| `event_type` | varchar(30) NOT NULL | `connected \| reconnected \| refresh_failed \| expired \| revoked \| disconnected` |
| `detail` | text NULL | o motivo da falha nos tipos de falha, senão null |
| `occurred_at` | timestamptz NOT NULL | |

Constraints: `pk_int003`, `chk_int003_event_type`, índice `ix_int003_user_occurred (user_id, occurred_at DESC)`.

As linhas são inseridas **na mesma transação** da mudança de estado que registram (padrão outbox
transacional): um revoke e seu log commitam juntos, então a linha do tempo nunca discorda do `int001`.
**Refreshes bem-sucedidos não são logados de propósito** — acontecem de hora em hora e
`int001.last_refreshed_at` já registra o último; logá-los enterraria o sinal.
