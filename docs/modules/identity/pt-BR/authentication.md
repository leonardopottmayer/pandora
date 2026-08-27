# Autenticação

[← Voltar ao índice](README.md) · Relacionados: [MFA](mfa.md), [Modelo de dados](data-model.md)

---

## 1. Cadastro e ativação

`POST /identity/auth/signup` cria um `User` com hash de senha Argon2id, em estado **não confirmado**
(`email_confirmed_at` NULL), e publica **`AccountActivationRequested`** — o Channels envia por e-mail um
link de ativação de uso único apoiado em `idt004`.

`POST /identity/auth/activate` consome o token de ativação (uso único, com expiração), define
`email_confirmed_at`, e publica **`AccountActivated`**. Username e e-mail são únicos.

## 2. Login

`POST /identity/auth/signin`:

1. Busca o usuário por e-mail; verifica a senha com Argon2id. A falha é **uniforme** — a resposta não
   revela se o e-mail existe ou se a senha estava errada.
2. **Se o MFA está desligado** — emite um **access token** JWT (via Tars) e um **refresh token**
   (persistido hasheado em `idt002`), carimba `last_sign_in_at`, devolve `TokenDto`.
3. **Se o MFA está ligado** — *não* emite um access token. Emite um **challenge MFA** de curta duração
   (`idt008`) e o devolve; o cliente o conclui em `POST /identity/mfa/challenge`. Ver [MFA](mfa.md).

## 3. Access + refresh tokens

- O **access token** é um JWT de curta duração carregando os claims do usuário; é validado em toda
  requisição autenticada pelo validador JWT do Tars.
- O **refresh token** é de longa duração, **uso único**, e guardado **hasheado** (`idt002.token_hash`)
  com um snapshot de claims.

`POST /identity/auth/refresh` apresenta o refresh token: ele é buscado por hash, checado não-consumido e
não-expirado, **consumido** (`consumed_at`), e um **novo** par access + refresh é emitido (rotação). Um
refresh token reproduzido (já consumido) falha, que é como o roubo é detectável.

O `RefreshTokenPurgeBackgroundService` apaga periodicamente linhas expiradas/consumidas.

## 4. Logout

`POST /identity/auth/signout` (autenticado) consome o refresh token atual para ele não poder mais
rotacionar. O access token continua válido até expirar (JWT stateless), o que sua curta duração limita.

## 5. Gestão de senha

- **Esqueci** — `POST /identity/auth/password/forgot` publica **`PasswordResetRequested`** (o Channels
  envia por e-mail um link de reset de uso único apoiado em `idt005`). A resposta é **uniforme** e não
  revela se a conta existe.
- **Reset** — `POST /identity/auth/password/reset` consome o token de reset, define um novo hash
  Argon2id, e publica **`PasswordChanged`**.
- **Troca** — `POST /identity/auth/password/change` (autenticado) verifica a senha atual, define o novo
  hash, carimba `last_password_changed_at`, e publica **`PasswordChanged`**.

## 6. Usuário atual

`GET /identity/me` (autenticado) devolve o perfil do usuário logado (`GetCurrentUser`) — a leitura de
bootstrap da SPA.
