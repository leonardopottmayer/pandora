# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído no código versus o que está desenhado mas ainda não implementado.

---

## Implementado

| Área | Notas |
|---|---|
| **Scaffold do módulo** | Sete projetos por camada; schema `identity`; `idt001`–`idt008`. |
| **Usuário + senha** | Agregado `User`; hash Argon2id (`Argon2PasswordHasher`); username/e-mail únicos. |
| **Cadastro + ativação** | `SignUp`; `AccountActivationRequested` → e-mail; `activate` consome `idt004`; `AccountActivated`. |
| **Login** | Verificação Argon2id; falha uniforme; JWT access token + refresh rotativo (`idt002`), via Tars. |
| **Refresh + logout** | `refresh` rotaciona tokens de uso único (`consumed_at`); `signout` consome; `RefreshTokenPurgeBackgroundService` limpa. |
| **Reset/troca de senha** | `forgot` (uniforme) → `PasswordResetRequested`; `reset` consome `idt005`; `change` autenticado; `PasswordChanged`. |
| **MFA (TOTP)** | `setup`/`enable`/`disable`/`status`; segredo encriptado (`idt006`, `ISecretProtector`); códigos de recuperação hasheados de uso único (`idt007`); challenge de login (`idt008`); `MfaEnabled`/`MfaDisabled`. |
| **Preferências** | `idt003` — tema, idioma, **fuso, início da semana, offset de alerta padrão**; `GET`/`PUT` com validação. |
| **Contratos** | Seis eventos de segurança consumidos pelos subscribers do Channels. |
| **Frontend** | `client-web/src/modules/identity` — login, cadastro, MFA, preferências. |

## Fatos notáveis para outros módulos

- **O Identity carrega o fuso IANA, o início da semana e o offset de alerta padrão** (`idt003`) — o
  trio que o plano da Agenda listava como pré-requisito de "fase 0". Está **construído e exposto** via
  `PUT /identity/preferences`. Consumi-lo por completo (padrões de item na Agenda, quiet hours do
  Channels) é trabalho de follow-up *naqueles* módulos, não aqui. Ver [Preferências](preferences.md).
- **E-mails de segurança** fluem inteiramente pelo caminho fato→template do Channels; o Identity não
  nomeia template.

## Ainda não implementado (futuro)

| Área | Status |
|---|---|
| **Login social / OAuth** (ex. login com Google) | Não implementado. (Distinto de [Integrations](../../integrations/pt-BR/overview.md), que é o Pandora chamando o Google *como* o usuário.) |
| **UI de gestão de sessões por dispositivo** | Refresh tokens são guardados + purgados, mas não há tela de lista de sessões / revogar por dispositivo. |
| **WebAuthn / passkeys** | Futuro — o MFA é só TOTP hoje. |
| **Papéis / permissões** | Não modelado — sistema pessoal de usuário único. |
