# Módulo Communications

> A caixa de entrada do usuário dentro do monolito modular Pandora: as mensagens que chegam *para* o
> usuário, de qualquer provedor, num lugar só. Email vem primeiro.
> 🇺🇸 [English version](../README.md).
>
> **Status: plano.** Os projetos do backend existem como template vazio (ligados no Host, sem
> funcionalidades, sem migrations). O pré-requisito, várias contas por provedor no
> [Integrations](../../integrations/pt-BR/README.md) (fase I5), está construído.

O **Communications** conecta quantas caixas de email o usuário quiser (Gmail pela Gmail API; Titan
Email e qualquer servidor IMAP/SMTP pelo MailKit) e permite ler, organizar e enviar pelo Pandora, com
regras que rodam como filtro nativo do Gmail ou no motor do próprio Pandora. É o lado de entrada; o
[Channels](../../channels/pt-BR/README.md) é o de saída (o Pandora avisando o usuário). O módulo
Radar (curadoria de notícias de tecnologia), planejado, vai se apoiar nele depois.

---

## Como esta documentação está organizada

Como o [Assistant](../../assistant/pt-BR/README.md) antes de ser construído, o Communications por
enquanto só tem os planos:

| Documento | Idioma | O que cobre |
|---|---|---|
| [Plano de Produto](product-plan.md) / [en](../en/product-plan.md) | en + pt-BR | Email completo: provedores, credenciais, modelo de dados, sincronização, organização, envio, regras (com filtros do Gmail nos dois sentidos), telas, API, erros, testes, fases M1–M4 |
| [Plano de execução](execution-plan.md) / [en](../en/execution-plan.md) | en + pt-BR | A M1 (conectar e ler), passo a passo |

Quando a M1 estiver construída, o módulo passa para o conjunto de tópicos de sempre em `en/` +
`pt-BR/` (`overview.md`, `architecture.md`, `data-model.md`, `api-reference.md`,
`implementation-status.md`).

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Communications.*` (7 projetos, template vazio).
- **Schema (planejado):** schema PostgreSQL `communications`, tabelas `com001_mailbox`…`com006_mail_rule`.
- **Credenciais:** no Integrations. Gmail é conta OAuth; Titan/IMAP é conta `password`. Cada caixa
  guarda o id da conta em que roda.
- **Armazenamento:** cabeçalhos, etiquetas e snippet são espelhados; corpo e anexos são buscados sob
  demanda, com cache.
- **Frontend (planejado):** `mail`, `mail/accounts` e `mail/rules` no `client-web`.
