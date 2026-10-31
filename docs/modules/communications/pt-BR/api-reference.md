# Referência da API

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

Caminho base: **`/api/v{version}/communications`**. Todo endpoint é autenticado e restrito às caixas de quem chama:
caixa, mensagem ou conta de outro usuário responde 404, igual a uma que não existe. Os erros vêm de falhas tipadas
de `Result`, pelo mapeador de erros HTTP compartilhado.

Senhas são salvas antes pelo Integrations, com `PUT /api/v1/integrations/{provider}/password`, que devolve o id da
conta que a caixa precisa. Uma conta Google vem de `POST /api/v1/integrations/google/connect`.

---

## Endpoints

| Método | Caminho | Finalidade |
|---|---|---|
| GET | `/mailboxes` | As caixas de quem chama, com status e progresso da carga inicial |
| POST | `/mailboxes` | Testa a conexão e cria uma caixa |
| PATCH | `/mailboxes/{id}` | Muda o nome de exibição e, no IMAP, os servidores |
| DELETE | `/mailboxes/{id}` | Apaga a cópia local; nada muda no provedor, e a credencial fica |
| POST | `/mailboxes/{id}/sync` | Roda um ciclo agora e devolve a caixa |
| GET | `/mailboxes/{id}/labels` | As etiquetas, com total e não lidas |
| POST | `/mailboxes/{id}/labels` | Cria uma etiqueta |
| PATCH | `/labels/{id}` | Renomeia uma etiqueta |
| DELETE | `/labels/{id}` | Apaga uma etiqueta |
| GET | `/messages` | Lista mensagens, da mais nova para a mais antiga |
| GET | `/messages/{id}` | Uma mensagem com o corpo, buscado e guardado em cache na primeira abertura |
| GET | `/messages/{id}/attachments/{attachmentId}` | Os bytes do anexo |
| POST | `/messages/actions` | Uma ação em até 100 mensagens de uma caixa |
| POST | `/messages/send` | Envia uma mensagem |
| GET | `/messages/{id}/compose` | O formulário preenchido para responder ou encaminhar |

### GET `/mailboxes`

`MailboxDto`: `id`, `provider`, `externalAccountId`, `address`, `displayName`, `imapHost`, `imapPort`, `smtpHost`,
`smtpPort`, `status`, `lastError`, `lastSyncedAt`, `messageCount`, `messageTotalEstimate`, `backfillCompletedAt`. O
progresso da carga inicial é `messageCount / messageTotalEstimate`.

### POST `/mailboxes`

```json
{
  "provider": "titan",
  "externalAccountId": "…",
  "address": null,
  "displayName": "Trabalho",
  "imapHost": null, "imapPort": null, "smtpHost": null, "smtpPort": null
}
```

- `provider`: `gmail`, `titan` ou `imap`. A conta precisa ser desse tipo (`gmail` exige uma conta `google`).
- `address` é opcional: o padrão é o email do Google ou o usuário IMAP.
- Os quatro campos de servidor andam juntos. Nenhum usa os valores prontos, que só o Titan tem; `imap` os exige.
- A conexão é testada **antes** de criar qualquer coisa.
- **Não** começa a carga inicial: o próximo ciclo começa. A tela chama `sync` logo depois.

| Status | Quando |
|---|---|
| 404 | A conta não existe ou é de outro usuário (`AccountNotFound`) |
| 409 | A conta já tem uma caixa (`AlreadyConnected`) |
| 422 | Provedor desconhecido, tipo de conta errado, endereço ou servidores inválidos, ou teste recusado (`MailboxAuthFailed`, `MissingScopes`, `ProviderFailed`) |

### PATCH `/mailboxes/{id}`

`{ displayName, imapHost, imapPort, smtpHost, smtpPort }`. O nome de exibição é substituído. Servidores novos (os
quatro, ou nenhum para manter os atuais) são testados antes e, se passarem, também reconectam a caixa.

### POST `/mailboxes/{id}/sync`

Uma caixa em `needs-reconnect` é testada antes: um teste recusado é um 422 e nada muda; um que passa a reconecta e o
ciclo roda. **Um ciclo que falha não é erro HTTP**: a caixa volta com o `status` e o `lastError`.

### GET `/mailboxes/{id}/labels`

`LabelDto`: `id`, `name`, `kind`, `role`, `isExclusive`, `total`, `unread`.

### POST `/mailboxes/{id}/labels`, PATCH `/labels/{id}`, DELETE `/labels/{id}`

`{ name }`, aparado, de 1 a 100 caracteres. Cada um responde com as etiquetas da caixa como ficam depois da operação. Só uma etiqueta do usuário pode ser
renomeada ou apagada. No Gmail, apagar uma etiqueta só a tira das mensagens; no IMAP só uma pasta vazia e sem subpastas pode ser apagada.

| Status | Quando |
|---|---|
| 404 | A caixa ou a etiqueta não existe ou é de outro usuário |
| 409 | A pasta tem mensagens ou subpastas (`LabelNotEmpty`) |
| 422 | Etiqueta do sistema (`SystemLabel`), nome inválido (`LabelNameInvalid`), ou recusa do provedor |

### POST `/messages/actions`

```json
{ "mailboxId": "…", "messageIds": ["…"], "action": "move", "labelId": "…" }
```

`action`: `mark-read`, `mark-unread`, `star`, `unstar`, `move`, `archive`, `trash`, `add-label`, `remove-label`; `labelId` acompanha `move`, `add-label` e
`remove-label`. O provedor vai primeiro: uma falha não muda nada. Responde com as mensagens como estão agora. Não há como apagar uma mensagem de vez.

| Status | Quando |
|---|---|
| 404 | A caixa, uma mensagem ou a etiqueta não existe ou é de outro usuário; uma mensagem de outra caixa interrompe a ação inteira, antes do provedor |
| 422 | Fora de 1–100 ids (`MessageCountInvalid`), ação desconhecida ou etiquetas no IMAP (`ActionNotSupported`), etiqueta que não pode ser usada (`SystemLabel`), pasta que não existe (`NoTrashFolder`, `NoArchiveFolder`), ou recusa do provedor |

### GET `/messages`

| Query | Notas |
|---|---|
| `mailboxId` | Opcional; sem ele, todas as caixas de quem chama |
| `labelId` | |
| `unread` | |
| `q` | Trecho do assunto, da prévia e do remetente, sem diferenciar maiúsculas (`ILIKE`). No IMAP a prévia fica vazia até a mensagem ser aberta, e o corpo nunca entra na busca |
| `threadKey` | Uma conversa |
| `before` | O `nextCursor` da página anterior; um inválido é 422 |
| `limit` | 1–100, padrão 50 |

Devolve `{ items: MessageSummaryDto[], nextCursor }`, com `nextCursor` nulo na última página. O cursor é um keyset
sobre `(sent_at, id)`, estável enquanto chega email novo.

`MessageSummaryDto`: `id`, `mailboxId`, `threadKey`, `from`, `to`, `cc` (`{ name, address }`), `subject`, `snippet`,
`sentAt`, `isUnread`, `isStarred`, `hasAttachments`, `labelIds`.

### GET `/messages/{id}`

`MessageDto`: `message` (o resumo), `rfc822MessageId`, `textBody`, `htmlBody`, `attachments`
(`{ id, fileName, contentType, sizeBytes, contentId }`).

- **O `htmlBody` vem como recebido.** O cliente precisa limpá-lo antes de mostrar.
- **Este endpoint não marca a mensagem como lida.** A tela envia `mark-read` depois de abrir.
- A primeira abertura chama o provedor, então pode falhar com os erros dele.

### GET `/messages/{id}/attachments/{attachmentId}`

Os bytes, com `X-Content-Type-Options: nosniff`. O `Content-Disposition` é `inline` só para PNG, JPEG, GIF, WebP e BMP,
e `attachment` para o resto. Também serve as imagens `cid:`.

### POST `/messages/send`

`multipart/form-data`: `mailboxId`, `sendKey` (um uuid gerado ao abrir o formulário), `to`, `cc`, `bcc` (só endereços, um campo por endereço), `subject`, `textBody`, `htmlBody`, `replyToId?` ou `forwardOfId?`, `keepAttachmentIds` (quais arquivos da mensagem encaminhada vão junto), `files`. Responde `{ copySaved }`. **Isso não tem volta.**

| Status | Quando |
|---|---|
| 200 | Enviada. `copySaved: false` significa que saiu, mas não foi possível salvar a cópia |
| 404 | A caixa ou a original não existe ou é de outro usuário, ou um arquivo mantido não existe |
| 409 | Esta chave já foi tratada (`AlreadySent`) |
| 422 | Destinatários inválidos, ausentes ou acima de 100 (`RecipientsInvalid`); anexos acima de 25 MB (`MessageTooLarge`); recusa do provedor (a chave é liberada); `SendUncertain`: **pode ter saído**, e a chave continua presa |

### GET `/messages/{id}/compose?mode=reply|reply-all|forward`

O formulário, calculado a partir da mensagem crua: `to`, `cc`, `subject`, `inReplyTo`, `references`, `originalFrom`, `originalDate`, `originalText` (como escrito; a tela cita), `attachments` (os arquivos de um encaminhamento, sem imagens inline).

---

## Evento

**`MailMessageReceived`** (Contracts), pelo outbox: `EventId`, `OccurredAt`, `UserId`, `MailboxId`, `MessageId`, `From`,
`Subject`, `Labels`. Publicado para uma mensagem que chega depois da carga inicial, e só quando a caixa não tem linha
com o mesmo Message-ID.
