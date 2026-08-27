# Visão geral — Negócio e Princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Autenticação](authentication.md)

---

## 1. O que o módulo faz

**Identity** é dono de *quem o usuário é* e de como ele prova isso:

- **Ciclo de vida da conta** — cadastro, ativação por e-mail, desativação.
- **Autenticação** — login emitindo um **JWT access token** de curta duração e um **refresh token
  rotativo**; refresh; logout.
- **Gestão de senha** — esqueci/reset (via token por e-mail) e troca autenticada.
- **Autenticação multifator** — enrolamento TOTP, um **challenge** de step-up no login, e **códigos de
  recuperação** de uso único.
- **Preferências** — tema, idioma, fuso IANA, início da semana, e o offset de alerta padrão usado pela
  Agenda.

Eventos relevantes de segurança (ativação, reset/troca de senha, MFA on/off) são publicados como
**contratos** que o [Channels](../../channels/pt-BR/overview.md) transforma em e-mail. O próprio
Identity nunca sabe que um template existe.

## 2. Princípios centrais

1. **Nunca guardar um segredo reutilizável em texto puro.** Senhas usam hash **Argon2id**; refresh
   tokens e todo token de uso único (ativação, reset, challenge MFA, código de recuperação) são
   guardados como **hashes SHA-256**; o segredo TOTP é **encriptado em repouso** (`ISecretProtector`).
2. **Tokens são emitidos e validados pelo Tars; usuários são donos aqui.** A maquinaria de JWT vive em
   `Pottmayer.Tars.Security.Identity`; o Identity fornece o usuário, os claims e a persistência dos
   refresh tokens (`idt002`).
3. **Refresh tokens são de uso único e rotacionam.** Cada refresh consome o token apresentado
   (`consumed_at`) e emite um novo, então um refresh token roubado-e-reproduzido é detectável e a
   janela é pequena.
4. **Tokens de uso único são de uso único e curta duração.** Tokens de ativação, reset e challenge MFA
   são consumidos no uso e expiram; autenticam exatamente uma ação.
5. **Falhas de auth são uniformes.** O login não revela se o e-mail existe ou se a senha estava errada;
   esqueci-senha não revela se uma conta existe.

## 3. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Usuário** (`idt001`) | A conta: nome, username + e-mail únicos, hash de senha Argon2id, estado de ativação, flag de MFA. |
| **Access token** | Um JWT de curta duração carregando os claims do usuário. Emitido no login / refresh / conclusão do MFA. |
| **Refresh token** (`idt002`) | Um token de longa duração, uso único, **hasheado** que emite um novo access token; rotacionado a cada uso. |
| **Token de ativação** (`idt004`) | Um token hasheado de uso único enviado por e-mail no cadastro para confirmar o e-mail. |
| **Token de reset de senha** (`idt005`) | Um token hasheado de uso único enviado no "esqueci a senha". |
| **Credencial MFA** (`idt006`) | O segredo TOTP do usuário, **encriptado**; `confirmed_at` marca um enrolamento concluído. |
| **Código de recuperação** (`idt007`) | Um código de backup hasheado de uso único para quando o autenticador não está disponível. |
| **Challenge MFA** (`idt008`) | Um token hasheado de curta duração emitido após o sucesso da senha quando o MFA está ligado; trocado pelo access token por um TOTP/código de recuperação válido. |
| **Preferências** (`idt003`) | Padrões de UI + agendamento por usuário: tema, idioma, fuso, início da semana, offset de alerta padrão. |

## 4. Escopo

### No escopo (implementado — ver [Status de implementação](implementation-status.md))

O schema `identity` (`idt001`–`idt008`); cadastro + ativação por e-mail; login com JWT access + refresh
rotativo; refresh; logout; esqueci/reset e troca autenticada de senha; MFA TOTP (setup/enable/disable,
status, códigos de recuperação, challenge de login); preferências (tema/idioma/fuso/início da
semana/offset de alerta); os contratos de eventos de segurança consumidos pelo Channels; uma purga de
background de refresh tokens expirados/consumidos; e o frontend.

### Fora do escopo / futuro

| Recurso | Status |
|---|---|
| **Login social / OAuth** (login com Google) | Não implementado. Nota: *o Pandora chamando o Google como o usuário* vive em [Integrations](../../integrations/pt-BR/overview.md); login social seria uma preocupação separada do Identity. |
| **UI de múltiplas sessões / gestão de dispositivos** | Refresh tokens são guardados e purgados, mas não há lista de sessões por dispositivo. |
| **Papéis / permissões** | Sistema pessoal de usuário único; sem modelo de papéis. |
| **WebAuthn / passkeys** | Futuro; o modelo de MFA é só TOTP hoje. |
