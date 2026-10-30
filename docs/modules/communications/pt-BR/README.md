# Módulo Communications

> A caixa de entrada do usuário dentro do monolito modular Pandora: as mensagens que chegam *para* o
> usuário, de qualquer provedor, num lugar só. Email vem primeiro.
> 🇺🇸 [English version](../README.md).
>
> **Status: M1 (conectar e ler) implementada; falta a verificação real.** Gmail, Titan e qualquer
> servidor IMAP conectam, sincronizam os cabeçalhos do mais novo para o mais antigo e abrem as
> mensagens num leitor em sandbox. Organizar (M2), enviar (M3) e regras (M4) estão planejados.

O **Communications** conecta quantas caixas de email o usuário quiser (Gmail pela Gmail API; Titan
Email e qualquer servidor IMAP/SMTP pelo MailKit) e permite ler, organizar e enviar pelo Pandora, com
regras que rodam como filtro nativo do Gmail ou no motor do próprio Pandora. É o lado de entrada; o
[Channels](../../channels/pt-BR/README.md) é o de saída (o Pandora avisando o usuário). O módulo
Radar (curadoria de notícias de tecnologia), planejado, vai se apoiar nele depois.

---

## Como esta documentação está organizada

A M1 está construída, mas o módulo ainda não tem o conjunto de tópicos de sempre: o que foi
construído, e onde difere do plano, está registrado passo a passo nas notas **Construído** do plano
de execução.

| Documento | Idioma | O que cobre |
|---|---|---|
| [Plano de Produto](product-plan.md) / [en](../en/product-plan.md) | en + pt-BR | Email completo: provedores, credenciais, modelo de dados, sincronização, organização, envio, regras (com filtros do Gmail nos dois sentidos), telas, API, erros, testes, fases M1–M4 |
| [Plano de execução](execution-plan.md) / [en](../en/execution-plan.md) | en + pt-BR | A M1 (conectar e ler), passo a passo, com o que cada passo construiu |

O conjunto de tópicos de sempre em `en/` + `pt-BR/` (`overview.md`, `architecture.md`,
`data-model.md`, `api-reference.md`, `implementation-status.md`) vem quando a M1 passar na
verificação real.

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Communications.*` (7 projetos). IMAP pelo MailKit, Gmail
  pela API REST (sem o SDK do Google), corpo das mensagens lido com MimeKit.
- **Schema:** schema PostgreSQL `communications`, tabelas `com001_mailbox`…`com005_mail_content`
  (`com006_mail_rule` é da M4), migrations em `migrations/migrations/communications/`.
- **Sincronização:** um job em segundo plano roda um ciclo por caixa a cada 2 minutos (etiquetas,
  mudanças, um lote da carga inicial); "Sincronizar agora" roda um na hora. O `MailMessageReceived`
  sai pelo outbox.
- **Base da API:** `/api/v1/communications` — caixas, etiquetas, mensagens, anexos.
- **Credenciais:** no Integrations. Gmail é conta OAuth; Titan/IMAP é conta `password`. Cada caixa
  guarda o id da conta em que roda.
- **Armazenamento:** cabeçalhos, etiquetas e snippet são espelhados; corpo e anexos são buscados sob
  demanda, com cache.
- **Frontend:** `mail` e `mail/accounts` em `client-web/src/modules/communications` (`mail/rules` é
  da M4). O HTML das mensagens é limpo pelo `lib/prepareMailHtml` e mostrado num iframe em sandbox com
  CSP; imagens externas ficam bloqueadas até "Mostrar imagens".
