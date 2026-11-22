# Módulo Communications

> A caixa de entrada do usuário dentro do monolito modular Pandora: as mensagens que chegam *para* o
> usuário, de qualquer provedor, num lugar só. Email vem primeiro.
> 🇺🇸 [English version](../README.md).
>
> **Status: M1 (conectar e ler), M2 (organizar), M3 (enviar) e M4 (regras) prontas, verificadas com contas reais.** Gmail, Titan e qualquer servidor IMAP conectam,
> sincronizam os cabeçalhos do mais novo para o mais antigo, abrem as mensagens num leitor em sandbox, organizam o email nos dois sentidos, enviam e mantêm regras: filtros do
> Gmail nos dois sentidos, motor do Pandora nos outros.

O **Communications** conecta quantas caixas de email o usuário quiser (Gmail pela Gmail API; Titan
Email e qualquer servidor IMAP/SMTP pelo MailKit) e permite ler, organizar e enviar pelo Pandora, com
regras que rodam como filtro nativo do Gmail ou no motor do próprio Pandora. É o lado de entrada; o
[Channels](../../channels/pt-BR/README.md) é o de saída (o Pandora avisando o usuário). O módulo
Radar (curadoria de notícias de tecnologia), planejado, vai se apoiar nele depois.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para a fronteira e o vocabulário, depois leia o tópico que precisar.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | O que o módulo faz e não faz, princípios, glossário, escopo |
| 2 | [Arquitetura](architecture.md) | Projetos, a porta do provedor e os adaptadores, o ciclo de sincronização, falhas, a segurança do leitor de email, decisões |
| 3 | [Modelo de dados](data-model.md) | `com001`–`com006`, e o cache de conteúdo em números reais |
| 4 | [Referência da API](api-reference.md) | Endpoints, payloads, erros, o evento |
| 5 | [Status de implementação](implementation-status.md) | O que está construído, as verificações com contas reais, decisões, pontos em aberto |
| 6 | [Plano de Produto](product-plan.md) | O que falta: fases futuras |

🇺🇸 Todo documento também existe em inglês, em [`en/`](../README.md).

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Communications.*` (7 projetos). IMAP pelo MailKit, Gmail
  pela API REST (sem o SDK do Google), corpo das mensagens lido com MimeKit.
- **Schema:** schema PostgreSQL `communications`, tabelas `com001_mailbox`…`com006_mail_rule`,
  migrations em `migrations/migrations/communications/`.
- **Sincronização:** um job em segundo plano roda um ciclo por caixa a cada 2 minutos (etiquetas,
  mudanças, lotes da carga inicial dentro de um orçamento de 5 minutos); "Sincronizar agora" roda um na
  hora. O `MailMessageReceived`
  sai pelo outbox.
- **Base da API:** `/api/v1/communications` — caixas, etiquetas, mensagens, anexos.
- **Credenciais:** no Integrations. Gmail é conta OAuth; Titan/IMAP é conta `password`. Cada caixa
  guarda o id da conta em que roda.
- **Armazenamento:** cabeçalhos, etiquetas e snippet são espelhados; corpo e anexos são buscados sob
  demanda, com cache.
- **Frontend:** `mail`, `mail/accounts` e `mail/rules` em `client-web/src/modules/communications`.
  O HTML das mensagens é limpo pelo `lib/prepareMailHtml` e mostrado num iframe em sandbox com
  CSP; imagens externas ficam bloqueadas até "Mostrar imagens".
