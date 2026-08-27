# Módulo Identity

> Autenticação, ciclo de vida de conta, MFA e preferências do usuário dentro do monólito modular do Pandora.
> **Idioma:** o inglês é a documentação primária. 🇺🇸 [English version](../README.md).

O módulo **Identity** é dono de *quem o usuário é*: cadastro e ativação de conta, login com JWT access
tokens e refresh tokens rotativos, reset e troca de senha, MFA baseado em TOTP com códigos de
recuperação, e preferências por usuário (tema, idioma, fuso, início da semana, offset de alerta padrão).

A regra que guia tudo: **o Identity emite e valida tokens; nunca guarda um segredo reutilizável em
texto puro.** Senhas usam hash **Argon2id**, refresh tokens e todo token de uso único são guardados
como **hashes**, e o segredo do MFA é **encriptado em repouso**. A infraestrutura de JWT em si vive no
Tars (`Pottmayer.Tars.Security.Identity`); o Identity é dono dos usuários, dos fluxos e da persistência.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o vocabulário e o escopo, depois leia o tópico que precisar.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | O que o módulo faz, princípios, linguagem ubíqua, escopo |
| 2 | [Arquitetura](architecture.md) | Organização de projetos, o agregado User e entidades, portas/serviços, decisões |
| 3 | [Modelo de dados](data-model.md) | Catálogo de schema (`idt001`–`idt008`): colunas, constraints, índices |
| 4 | [Autenticação](authentication.md) | Cadastro, ativação, login, JWT + rotação de refresh, logout, reset/troca de senha |
| 5 | [MFA](mfa.md) | Setup/enable/disable TOTP, códigos de recuperação, o challenge de login |
| 6 | [Preferências](preferences.md) | Tema, idioma, fuso, início da semana, offset de alerta padrão |
| 7 | [Referência de API](api-reference.md) | Todos os endpoints sob `/api/v{n}/identity` |
| 8 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Identity.*` (.NET 10, DDD, comandos/queries no estilo CQRS).
- **Schema:** schema PostgreSQL `identity`, tabelas com prefixo `idtXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/identity` (login, cadastro, MFA, preferências).
- **Base da API:** `/api/v{version}/identity`, com endpoints de auth anônimos e endpoints de
  conta/preferências autenticados.
- **Migrations:** `migrations/migrations/identity/`.
- **Blocos Tars:** `Pottmayer.Tars.Security.Identity` (emissão/validação de JWT + serviço de refresh),
  `Pottmayer.Tars.Security.DataProtection` (`ISecretProtector`).
