# MFA (TOTP)

[← Voltar ao índice](README.md) · Relacionados: [Autenticação](authentication.md), [Modelo de dados](data-model.md)

---

A autenticação multifator do Identity é **TOTP** (códigos de app autenticador), com **códigos de
recuperação** de uso único como backup e um **challenge de step-up** no login.

## 1. Enrolamento

1. `GET /identity/mfa/status` — devolve se o MFA está ligado e, se estiver, a contagem de códigos de
   recuperação não usados (`MfaStatusDto`: `Enabled`, `RemainingRecoveryCodes`). Não expõe se há um
   setup pendente.
2. `POST /identity/mfa/setup` — gera um segredo TOTP, o guarda **encriptado** (`idt006.secret_cipher`,
   `confirmed_at` NULL), e devolve os dados de provisionamento (segredo / URI otpauth) para o QR code do
   app autenticador. Falha se o MFA já está ligado. Chamar de novo antes de confirmar substitui a
   credencial pendente anterior por um segredo novo.
3. `POST /identity/mfa/enable` — o usuário envia um código TOTP atual; no sucesso `confirmed_at` é
   definido, `user.mfa_enabled = true`, um conjunto de **códigos de recuperação** é gerado (guardado
   **hasheado**, `idt007`), e **`MfaEnabled`** é publicado (o Channels envia uma confirmação). Os
   códigos de recuperação em texto puro são devolvidos **uma vez**, aqui, e nunca mais.

## 2. Challenge no login

Quando `mfa_enabled` é true, o `signin` para após a checagem de senha e emite um **challenge MFA** de
curta duração (`idt008`, hasheado, com expiração) em vez de um access token.

`POST /identity/mfa/challenge` troca o token de challenge **mais** um fator válido pelos tokens reais:

- um **código TOTP** atual (verificado por `ITotpAuthenticator`), ou
- um **código de recuperação** (buscado por hash em `idt007`, consumido de uso único).

No sucesso o challenge é consumido e um par JWT access + refresh é emitido — a mesma saída de um login
sem MFA.

## 3. Códigos de recuperação

`POST /identity/mfa/recovery-codes` (autenticado) regenera o conjunto, invalidando os códigos antigos e
devolvendo o novo conjunto em texto puro uma vez. Cada código é de uso único (`consumed_at`).

## 4. Desabilitar

`POST /identity/mfa/disable` (autenticado, re-verificando um fator) define `user.mfa_enabled = false`,
remove a credencial e os códigos de recuperação, e publica **`MfaDisabled`** (o Channels envia uma
confirmação).

## 5. Propriedades de segurança

- O segredo TOTP é **encriptado em repouso** (`ISecretProtector`, chave fora do banco).
- Códigos de recuperação e tokens de challenge são **hasheados** e de **uso único**.
- Ligar/desligar o MFA sempre emite um e-mail de segurança, então uma mudança silenciosa é visível ao
  usuário.
