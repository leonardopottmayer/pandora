# Referência de API

[← Voltar ao índice](README.md) · Relacionados: [Autenticação](authentication.md), [MFA](mfa.md)

Caminho base: **`/api/v{version}/identity`**. Pontos de entrada de auth são **anônimos**; conta,
preferências e gestão de MFA exigem um access token válido. Erros vêm de falhas tipadas `Result`, com
mensagens **uniformes** onde a existência da conta não pode vazar.

---

## Auth — `/identity/auth`

| Método | Caminho | Auth | Propósito |
|---|---|---|---|
| POST | `/auth/signup` | anon | Cria uma conta (não confirmada); publica `AccountActivationRequested`. |
| POST | `/auth/signin` | anon | Verifica a senha; emite tokens, **ou** um challenge MFA se o MFA está ligado. |
| POST | `/auth/activate` | anon | Consome o token de ativação; confirma o e-mail. |
| POST | `/auth/password/forgot` | anon | Publica `PasswordResetRequested` (resposta uniforme). |
| POST | `/auth/password/reset` | anon | Consome o token de reset; define uma nova senha. |
| POST | `/auth/password/change` | usuário | Troca a senha (verifica a atual). |
| POST | `/auth/refresh` | anon | Rotaciona: consome o refresh token, emite um novo par access + refresh. |
| POST | `/auth/signout` | usuário | Consome o refresh token atual. |

## Usuário atual — `/identity`

| Método | Caminho | Auth | Propósito |
|---|---|---|---|
| GET | `/me` | usuário | O perfil do usuário logado. |

## MFA — `/identity/mfa`

| Método | Caminho | Auth | Propósito |
|---|---|---|---|
| GET | `/mfa/status` | usuário | O MFA está ligado, mais a contagem de códigos de recuperação não usados. |
| POST | `/mfa/setup` | usuário | Gera + guarda (encriptado) um segredo TOTP; devolve dados de provisionamento. |
| POST | `/mfa/enable` | usuário | Confirma um código TOTP; liga o MFA; devolve códigos de recuperação uma vez; publica `MfaEnabled`. |
| POST | `/mfa/disable` | usuário | Desliga o MFA (re-verificando um fator); publica `MfaDisabled`. |
| POST | `/mfa/recovery-codes` | usuário | Regenera os códigos de recuperação. |
| POST | `/mfa/challenge` | anon | Troca um challenge MFA de login + código TOTP/recuperação por tokens. |

## Preferências — `/identity/preferences`

| Método | Caminho | Auth | Propósito |
|---|---|---|---|
| GET | `/preferences` | usuário | Lê as preferências do usuário. |
| PUT | `/preferences` | usuário | Upsert de tema / idioma / fuso / início da semana / offset de alerta padrão. |

---

## Contratos (eventos in-process)

Publicados para o [Channels](../../channels/pt-BR/overview.md) transformar em e-mail:

`AccountActivationRequested`, `AccountActivated`, `PasswordResetRequested`, `PasswordChanged`,
`MfaEnabled`, `MfaDisabled`.
