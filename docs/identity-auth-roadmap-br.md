# Identity / Auth Roadmap

## Direcao

- `Users` continua dono de `name`, `username`, `email` e `phone`
- `Identity` vira dono de `password_hash`, ativacao, reset, MFA e sessoes
- `Notifications` sera o modulo de envio de e-mail, SMS e canais futuros

## Etapas

### 1. Fundacao

- criar `idt001_account`
- mover `password` de `usr001_user` para `idt001_account`
- adaptar login para autenticar via `Identity`
- manter login por `email` ou `username`

### 2. Notifications minimo

- criar modulo `Notifications`
- comecar so com e-mail
- definir contrato simples de envio
- plugar provider fake/dev primeiro, provider real depois

### 3. Ativacao de conta

- registro cria `User`
- registro cria `IdentityAccount` com `pending_activation`
- gerar token temporario
- enviar e-mail de ativacao
- criar endpoint para consumir ativacao

### 4. Reset de senha

- endpoint para solicitar reset
- token temporario de uso unico
- e-mail com link
- endpoint para definir nova senha
- revogar sessoes antigas

### 5. Troca de senha

- usuario autenticado troca senha
- validar senha atual
- aplicar politica de senha
- enviar e-mail avisando a troca

### 6. MFA opcional

- setup de TOTP
- confirmacao por codigo
- recovery codes
- MFA habilitavel/desabilitavel nas configuracoes da conta

## Ordem recomendada

1. fundacao do `Identity`
2. `Notifications` minimo
3. ativacao de conta
4. reset de senha
5. troca de senha
6. MFA

## Primeiro passo real

Se for para comecar agora, eu faria isto primeiro:

1. criar `idt001_account`
2. migrar `password` para la
3. refatorar o login atual para usar essa nova tabela

Sem isso, todo o resto nasce em cima da estrutura errada.
