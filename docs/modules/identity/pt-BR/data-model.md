# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Autenticação](authentication.md)

Schema PostgreSQL **`identity`**. Convenções: PK `uuid DEFAULT uuid_generate_v7()`, timestamps
`TIMESTAMPTZ`, constraints nomeadas, enums como `VARCHAR` + `CHECK`. Segredos nunca são guardados em
texto puro — tokens como **hashes** SHA-256, o segredo MFA **encriptado**.

As migrations ficam em `migrations/migrations/identity/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| idt001 | `user` | A conta |
| idt002 | `stored_refresh_token` | Refresh tokens rotativos, uso único |
| idt003 | `user_preferences` | Padrões de UI + agendamento por usuário |
| idt004 | `account_activation_token` | Tokens de ativação por e-mail |
| idt005 | `password_reset_token` | Tokens de reset de senha |
| idt006 | `mfa_credential` | Segredo TOTP encriptado |
| idt007 | `mfa_recovery_code` | Códigos de backup de uso único |
| idt008 | `mfa_challenge` | Tokens de challenge de step-up no login |

---

## idt001_user

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `name` | varchar(150) NOT NULL | |
| `username` | varchar(50) NOT NULL | **único** (`uq_idt001_username`) |
| `email` | varchar(255) NOT NULL | **único** (`uq_idt001_email`) |
| `password_hash` | text NOT NULL | Argon2id |
| `email_confirmed_at` | timestamptz NULL | definido na ativação |
| `disabled_at` | timestamptz NULL | desativação suave |
| `mfa_enabled` | bool DEFAULT false | dirige o challenge de login |
| `last_sign_in_at` / `last_password_changed_at` | timestamptz NULL | |
| `created_by/created_at/updated_by/updated_at` | | auditoria; `created_by`/`updated_by` com FK de volta para `idt001` |

## idt002_stored_refresh_token

Backing store do `IRefreshTokenService` do Tars. O token é guardado **hasheado** e é de uso único.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `key` | varchar(100) NOT NULL | **único** (`uq_idt002_key`) — o identificador do token |
| `token_hash` | varchar(64) NOT NULL | SHA-256 do token |
| `subject` | varchar(100) NOT NULL | o usuário |
| `claims_json` | text NOT NULL | snapshot de claims re-emitido no refresh |
| `expires_at` | timestamptz NOT NULL | |
| `metadata_json` | text NULL | ex. metadados de dispositivo/agente |
| `consumed_at` | timestamptz NULL | definido na rotação; um token consumido não pode dar refresh de novo |

## idt003_user_preferences

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | **único** (`uq_idt003_user_id`); FK → idt001 `ON DELETE CASCADE` |
| `theme` | varchar(20) NOT NULL | `light \| dark \| system` (`chk_idt003_theme`) |
| `language` | varchar(10) DEFAULT 'en' | `pt-BR \| en` (`chk_idt003_language`) |
| `time_zone` | varchar(64) DEFAULT 'America/Sao_Paulo' | IANA |
| `week_starts_on` | varchar(10) DEFAULT 'sunday' | `sunday…saturday` (`chk_idt003_week_starts_on`) |
| `default_alert_offset_minutes` | int DEFAULT -15 | consumido pela Agenda como offset de alerta padrão |

## idt004_account_activation_token / idt005_password_reset_token

Mesma forma — um token de uso único, hasheado, com expiração, ligado a um usuário.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid → idt001 | `ON DELETE CASCADE` |
| `token_hash` | varchar(64) NOT NULL | **único** |
| `expires_at` | timestamptz NOT NULL | |
| `consumed_at` | timestamptz NULL | uso único |

## idt006_mfa_credential

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | **único** (`uq_idt006_user_id`); FK → idt001 `ON DELETE CASCADE` |
| `secret_cipher` | text NOT NULL | o segredo TOTP, **encriptado** |
| `confirmed_at` | timestamptz NULL | definido quando o enrolamento é confirmado |
| `created_at` | timestamptz | |

## idt007_mfa_recovery_code

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | FK → idt001 `ON DELETE CASCADE`; índice `ix_idt007_user_id` |
| `code_hash` | varchar(64) NOT NULL | **único** |
| `consumed_at` | timestamptz NULL | uso único |
| `created_at` | timestamptz | |

## idt008_mfa_challenge

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | FK → idt001 `ON DELETE CASCADE`; índice `ix_idt008_user_id` |
| `token_hash` | varchar(64) NOT NULL | **único** |
| `expires_at` | timestamptz NOT NULL | curta duração |
| `consumed_at` | timestamptz NULL | uso único — trocado pelo access token |
