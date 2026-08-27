# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Authentication](authentication.md)

PostgreSQL schema **`identity`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`
timestamps, named constraints, enums as `VARCHAR` + `CHECK`. Secrets are never stored in the clear —
tokens as SHA-256 **hashes**, the MFA secret **encrypted**.

Migrations live in `migrations/migrations/identity/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| idt001 | `user` | The account |
| idt002 | `stored_refresh_token` | Rotating, single-use refresh tokens |
| idt003 | `user_preferences` | Per-user UI + scheduling defaults |
| idt004 | `account_activation_token` | Email activation tokens |
| idt005 | `password_reset_token` | Password reset tokens |
| idt006 | `mfa_credential` | Encrypted TOTP secret |
| idt007 | `mfa_recovery_code` | Single-use backup codes |
| idt008 | `mfa_challenge` | Sign-in step-up challenge tokens |

---

## idt001_user

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `name` | varchar(150) NOT NULL | |
| `username` | varchar(50) NOT NULL | **unique** (`uq_idt001_username`) |
| `email` | varchar(255) NOT NULL | **unique** (`uq_idt001_email`) |
| `password_hash` | text NOT NULL | Argon2id |
| `email_confirmed_at` | timestamptz NULL | set on activation |
| `disabled_at` | timestamptz NULL | soft disable |
| `mfa_enabled` | bool DEFAULT false | drives the sign-in challenge |
| `last_sign_in_at` / `last_password_changed_at` | timestamptz NULL | |
| `created_by/created_at/updated_by/updated_at` | | audit; `created_by`/`updated_by` FK back to `idt001` |

## idt002_stored_refresh_token

Backs the Tars `IRefreshTokenService`. The token is stored **hashed** and is single-use.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `key` | varchar(100) NOT NULL | **unique** (`uq_idt002_key`) — the token identifier |
| `token_hash` | varchar(64) NOT NULL | SHA-256 of the token |
| `subject` | varchar(100) NOT NULL | the user |
| `claims_json` | text NOT NULL | claims snapshot re-issued on refresh |
| `expires_at` | timestamptz NOT NULL | |
| `metadata_json` | text NULL | e.g. device/agent metadata |
| `consumed_at` | timestamptz NULL | set on rotation; a consumed token cannot refresh again |

## idt003_user_preferences

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | **unique** (`uq_idt003_user_id`); FK → idt001 `ON DELETE CASCADE` |
| `theme` | varchar(20) NOT NULL | `light \| dark \| system` (`chk_idt003_theme`) |
| `language` | varchar(10) DEFAULT 'en' | `pt-BR \| en` (`chk_idt003_language`) |
| `time_zone` | varchar(64) DEFAULT 'America/Sao_Paulo' | IANA |
| `week_starts_on` | varchar(10) DEFAULT 'sunday' | `sunday…saturday` (`chk_idt003_week_starts_on`) |
| `default_alert_offset_minutes` | int DEFAULT -15 | consumed by Agenda as the default alert offset |

## idt004_account_activation_token / idt005_password_reset_token

Same shape — a single-use, hashed, expiring token tied to a user.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid → idt001 | `ON DELETE CASCADE` |
| `token_hash` | varchar(64) NOT NULL | **unique** |
| `expires_at` | timestamptz NOT NULL | |
| `consumed_at` | timestamptz NULL | single use |

## idt006_mfa_credential

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | **unique** (`uq_idt006_user_id`); FK → idt001 `ON DELETE CASCADE` |
| `secret_cipher` | text NOT NULL | the TOTP secret, **encrypted** |
| `confirmed_at` | timestamptz NULL | set when enrolment is confirmed |
| `created_at` | timestamptz | |

## idt007_mfa_recovery_code

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | FK → idt001 `ON DELETE CASCADE`; index `ix_idt007_user_id` |
| `code_hash` | varchar(64) NOT NULL | **unique** |
| `consumed_at` | timestamptz NULL | single use |
| `created_at` | timestamptz | |

## idt008_mfa_challenge

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | FK → idt001 `ON DELETE CASCADE`; index `ix_idt008_user_id` |
| `token_hash` | varchar(64) NOT NULL | **unique** |
| `expires_at` | timestamptz NOT NULL | short-lived |
| `consumed_at` | timestamptz NULL | single use — exchanged for the access token |
