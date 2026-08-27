# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Autenticação](authentication.md)

---

## 1. Organização dos projetos

Projetos por camada sob `backend/src/Modules/Identity/`:

```
Pottmayer.Pandora.Modules.Identity.
  Abstractions      → registro do módulo + superfície pública
  Application       → Commands (SignUp, SignIn, RefreshToken, SignOut, Activation, ChangePassword,
                      PasswordReset, Mfa/*, UpsertPreferences, PurgeRefreshTokens),
                      Queries (GetCurrentUser, GetPreferences, Mfa/GetMfaStatus), Dtos, Security, DI
  Contracts         → IntegrationEvents: AccountActivationRequested, AccountActivated,
                      PasswordResetRequested, PasswordChanged, MfaEnabled, MfaDisabled
  Domain            → agregado User, entidades, ValueObjects, Ports (repositórios + serviços), Errors
  Infrastructure    → hasher Argon2, autenticador TOTP, emissor/validador JWT (Tars), job de purga de
                      refresh, DI
  Persistence       → EntityConfigs, Repositories, IdentityDbContext, DI
  Presentation      → AuthController, MeController, MfaController, PreferencesController, DI
```

## 2. Blocos de domínio

### Agregado e entidades (`Domain`)

| Tipo | Papel |
|---|---|
| **User** (raiz de agregado, `idt001`) | Nome, username + e-mail únicos, `password_hash` Argon2id, `email_confirmed_at`, `disabled_at`, `mfa_enabled`, timestamps de login/senha. |
| **UserPreferences** (`idt003`) | Tema, idioma, `TimeZone` (IANA), `WeekStartsOn` (`DayOfWeek`), `DefaultAlertOffsetMinutes`. Uma por usuário. |
| **StoredRefreshToken** (`idt002`) | Refresh token hasheado, uso único, com snapshot de claims, expiração e `consumed_at`. |
| **AccountActivationToken** (`idt004`) | Token de ativação por e-mail, hasheado, uso único. |
| **PasswordResetToken** (`idt005`) | Token de reset hasheado, uso único. |
| **MfaCredential** (`idt006`) | Segredo TOTP encriptado + `confirmed_at`. |
| **MfaRecoveryCode** (`idt007`) | Código de backup hasheado, uso único. |
| **MfaChallenge** (`idt008`) | Token de step-up hasheado, curta duração, emitido após o sucesso da senha quando o MFA está ligado. |

### Objetos de valor (`Domain/ValueObjects`)

`AppTheme` (`light` \| `dark` \| `system`), `AppLanguage` (`pt-BR` \| `en`).

### Portas (`Domain/Ports`)

- **Serviços:** `IPasswordHasher` (Argon2id — `Argon2PasswordHasher`), `ITotpAuthenticator` (verifica/
  gera TOTP), `ISecretProtector` (encripta o segredo MFA — do Tars DataProtection).
- **Repositórios:** `IUserRepository`, `IRefreshTokenRepository`, `IActivationTokenRepository`,
  `IPasswordResetTokenRepository`, `IMfaCredentialRepository`, `IMfaRecoveryCodeRepository`,
  `IMfaChallengeRepository`.

## 3. Blocos Tars

- **`Pottmayer.Tars.Security.Identity`** — emissão/validação de JWT (`AddTarsIdentityJwtTokenIssuer` /
  `…JwtTokenValidator`) e o `IRefreshTokenService`, com backing store no `idt002` do Identity. O
  formato do access token, a assinatura e a validação são do Tars; o usuário e a persistência do
  refresh são do Pandora.
- **`Pottmayer.Tars.Security.DataProtection`** — `ISecretProtector` (AES-GCM) para o segredo MFA.

## 4. Jobs de background

`RefreshTokenPurgeBackgroundService` (`PurgeRefreshTokens`) apaga periodicamente refresh tokens
expirados e consumidos, evitando que o `idt002` cresça sem limite.

## 5. Contratos (eventos in-process)

O Identity publica fatos; os subscribers do Channels os transformam em e-mail (o Identity nunca nomeia
um template):

`AccountActivationRequested`, `AccountActivated`, `PasswordResetRequested`, `PasswordChanged`,
`MfaEnabled`, `MfaDisabled`.

## 6. Decisões de design

| # | Decisão | Racional |
|---|---|---|
| **1** | Argon2id para senhas. | Memory-hard, moderno; resiste a cracking em GPU melhor que bcrypt/PBKDF2. |
| **2** | Guardar só hashes de tokens (refresh, ativação, reset, challenge, recuperação). | Um dump do banco não rende token utilizável; o texto puro existe só em trânsito. |
| **3** | Refresh tokens rotacionam e são de uso único (`consumed_at`). | Janela de replay pequena; um token reusado é detectável. |
| **4** | Segredo MFA encriptado com uma chave fora do banco. | Um dump sozinho não pode reconstruir o TOTP de um usuário. |
| **5** | Infra de JWT no Tars, usuários aqui. | O formato do token é reutilizável entre sistemas (roberto também); o modelo de usuário é do Pandora. |
| **6** | Eventos de segurança são contratos, não e-mails diretos. | O Identity fica alheio a canais/templates; a política de entrega vive no Channels. |

## 7. Regras transversais

- **Anônimo vs. autenticado.** Pontos de entrada de auth (`signup`, `signin`, `activate`,
  `password/forgot`, `password/reset`, `refresh`, `mfa/challenge`) são anônimos; conta, preferências e
  gestão de MFA exigem um access token válido.
- **Falhas uniformes.** Login e esqueci-senha não revelam a existência da conta.
- **`TimeProvider` em todo lugar** — expiração de tokens e TTLs são calculados contra o tempo injetado.
