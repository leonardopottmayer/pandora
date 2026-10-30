# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Referência da API](api-reference.md)

---

## 1. Organização dos projetos

O módulo segue os outros módulos do Pandora, em `backend/src/Modules/Communications/`:

```
Pottmayer.Pandora.Modules.Communications.
  Abstractions      → CommunicationsModule (nome, chave, schema), CommunicationsOptions
  Contracts         → IntegrationEvents: MailMessageReceived
  Domain            → Entidades (Mailbox, MailLabel, MailMessage, MailContent), ValueObjects
                      (MailProvider, MailboxStatus, LabelKind, LabelRole, ThreadKey),
                      Portas (IMailboxProvider, repositórios), MailErrors
  Application       → Commands (ConnectMailbox, UpdateMailbox, DeleteMailbox, SyncMailbox,
                      SyncMailboxNow, OpenMessage), Queries (GetMailboxes, GetLabels, ListMessages,
                      GetAttachment), MimeReader, subscribers, a fonte de uso da conta
  Infrastructure    → Adaptadores Gmail/ e Imap/, Jobs/MailboxSyncBackgroundService
  Persistence       → EntityConfigs, Repositórios, DbContext (com o outbox)
  Presentation      → CommunicationsController, requests
```

As telas ficam em `client-web/src/modules/communications`.

## 2. A porta do provedor

O `IMailboxProvider` (Domain/Ports) tem um adaptador por tipo de provedor, registrado como serviço keyed em
cada valor de `MailProvider` que atende, então o motor pede o provedor da caixa pela chave:

| Operação | O que faz |
|---|---|
| `Capabilities` | `MultipleLabels`, `ServerFilters`, `NativeThreads` |
| `TestConnectionAsync` | Usada antes de criar uma caixa ou de mudar a conexão dela |
| `ListLabelsAsync` | Todas as etiquetas ou pastas |
| `StartBackfillAsync` | Pega o cursor incremental **agora**, mais o cursor da carga inicial e o total |
| `FetchBackfillBatchAsync` | O próximo lote, do mais novo para o mais antigo |
| `FetchChangesAsync` | O que mudou desde o cursor: upserts, ids removidos, snapshots, cursor novo |
| `GetRawMessageAsync` | A mensagem RFC 822 inteira, sem marcar como lida |

Pegar o cursor incremental antes do primeiro lote é o que evita perder email: o que chega ou muda durante a
carga inicial aparece no próximo `FetchChangesAsync`.

### Adaptadores

| | `GmailMailboxProvider` (`gmail`) | `ImapMailboxProvider` (`titan`, `imap`) |
|---|---|---|
| **Cliente** | REST com `HttpClient`, sem o SDK do Google | MailKit, uma conexão por chamada |
| **Credencial** | `GetAccessTokenAsync`; o token precisa ter `gmail.modify` ou `mail.google.com` | `GetPasswordAsync` |
| **Cursor de sincronização** | `historyId` | JSON por pasta: UIDVALIDITY, UIDNEXT, MODSEQ |
| **Cursor da carga inicial** | `pageToken` | JSON por pasta: o menor UID lido; uma pasta por lote, INBOX primeiro |
| **Novas e alteradas** | `history.list`; toda mensagem tocada é lida de novo por inteiro | UIDs a partir do UIDNEXT; flags pelo CONDSTORE, ou os últimos 200 UIDs sem ele |
| **Removidas** | `messagesDeleted`, ou um 404 ao reler | Um snapshot dos UIDs de cada pasta (`SEARCH ALL`) a cada ciclo |
| **Id da mensagem** | O id do Gmail | `{uidvalidity}:{uid}:{caminho da pasta}` |
| **Cursor expirado** | O histórico responde 404 | UIDVALIDITY mudou |
| **Lida / estrela** | Etiquetas `UNREAD` / `STARRED`, viram flags | `\Seen` / `\Flagged` |
| **Data de envio** | `internalDate` | Cabeçalho `Date`, ou a data interna |

O adaptador do Gmail faz dois `messages.get` por vez e repete `429`, `5xx` e `403` de limite de taxa depois de
1, 2, 4, 8 e 16 s (um `Retry-After` tem prioridade). Quatro por vez geraram `403` frequentes numa carga real de
64 mil mensagens.

## 3. O ciclo de sincronização

`SyncMailboxCommand`, uma caixa, sob uma trava em processo por caixa:

```
1. Etiquetas     ListLabels → cria, atualiza, apaga as etiquetas locais
2. Mudanças      sem cursor ainda    → StartBackfill
                 cursor expirado     → apaga as mensagens da caixa, StartBackfill
                 senão               → aplica FetchChanges, guarda o cursor novo
3. Carga inicial lotes seguidos até acabar ou o BackfillBudget se esgotar (ao menos um)
4. Sucesso       RecordSuccess
```

- **As chamadas ao provedor rodam fora de transação.** Cada resultado é aplicado na própria unidade de trabalho,
  então um servidor lento nunca segura uma transação aberta, e uma falha mantém o que já foi aplicado. O lote e o
  cursor seguinte são salvos juntos, então um lote que falhou é simplesmente lido de novo.
- **O `MailMessageReceived`** vem só do caminho incremental, na mesma transação da mensagem, e só quando a caixa
  não tem linha com aquele Message-ID. A carga inicial não publica nada, e uma mensagem movida no webmail não
  conta como chegada.
- **Uma etiqueta que o provedor parou de listar** é apagada. No IMAP as mensagens vão junto; no Gmail elas só
  perdem a etiqueta.
- **Todas as datas são salvas em UTC.** O IMAP entrega o fuso de quem enviou e o Npgsql só grava UTC em
  `timestamptz`, então o `MailMessage` converte ao criar e ao atualizar.

### O job

O `MailboxSyncBackgroundService` roda a cada `SyncInterval`: um ciclo por caixa **em sequência**, pulando
`needs-reconnect`, e depois a retenção do cache de conteúdo. O `PeriodicTimer` pula os ticks que uma volta longa
cobre. "Sincronizar agora" (`POST mailboxes/{id}/sync`) roda um ciclo dentro da requisição, sob a mesma trava.

### Falhas

| Falha | Resultado |
|---|---|
| Senha IMAP recusada | `needs-reconnect` e um aviso, só na mudança (`communications.mailbox-auth-failed`, pelo Channels) |
| Falha de autenticação no Gmail ou escopos faltando | `needs-reconnect` sem aviso: o Integrations já avisa sobre autorização revogada, e a tela mostra o resto |
| `ExternalAccountRevoked` / `ExternalAccountDisconnected` | As caixas daquela conta vão para `needs-reconnect`; os dados ficam |
| Erro do provedor (rede, 5xx, cota) | `RecordFailure`, repete no próximo ciclo; `error` depois de 5 seguidas, e um sucesso limpa |
| Cursor expirado | A caixa é lida de novo do início, sem eventos |
| Uma exceção que escapa do ciclo (uma escrita no banco que falhou, por exemplo) | Registrada no log pelo job, **não** na caixa; veja [Pontos em aberto](implementation-status.md#pontos-em-aberto) |

## 4. Abrir uma mensagem

`OpenMessageCommand`: um corpo em cache é devolvido e o `last_accessed_at` atualizado. Senão, o adaptador busca a
mensagem crua, o `MimeReader` (MimeKit) a interpreta e o corpo vai para o cache. No IMAP isso também preenche a
prévia, que o IMAP não fornece.

**Anexos** vêm de interpretar a mensagem crua de novo; o id do anexo é a posição dele entre as partes. O endpoint
envia `nosniff`, e só PNG, JPEG, GIF, WebP e BMP vão como `inline`. Qualquer outro tipo (HTML, SVG, PDF) é baixado,
porque a parte vem de quem enviou o email.

## 5. Frontend

- **`/mail` (`MailPage`):** `MailSidebar` (caixas, etiquetas, não lidas), `MessageList` (páginas de 50 com "Carregar
  mais"), `MessageReader` (a conversa).
- **`/mail/accounts` (`MailAccountsPage`):** status, progresso da carga inicial, "Sincronizar agora",
  `ConnectMailboxModal`, `FixConnectionModal`. A lista consulta de novo a cada 15 s enquanto há carga inicial ou
  ciclo rodando.
- **Conectar o Gmail:** consentimento no Google com `gmail.modify` somado aos escopos padrão, volta em
  `?connect=gmail`, onde o modal reabre com a conta escolhida e o usuário confirma.
- **Reconectar:** o Gmail passa pelo Google e volta em `?sync={id}`; Titan e IMAP abrem "Editar conexão". Nos dois
  casos, o `sync` roda em seguida.

### HTML do email, a fronteira de segurança

O `lib/prepareMailHtml` trata o que a API devolve como recebido:

1. O DOMPurify remove scripts e handlers de evento.
2. `src`, `srcset`, `background` e `url()` de CSS externos viram um placeholder até "Mostrar imagens".
3. Imagens `cid:` viram URLs `data:`. O endpoint do anexo exige o token Bearer, e uma URL `blob:` pertence a uma
   origem que o sandbox não compartilha.
4. Links abrem em nova aba.

O resultado é mostrado em `<iframe sandbox="allow-popups allow-popups-to-escape-sandbox">` com CSP:
`default-src 'none'; style-src 'unsafe-inline'; img-src data:`, ampliada para `data: https: http:` depois de "Mostrar
imagens".

## 6. Principais decisões de design

| # | Decisão | Motivo (alternativa rejeitada) |
|---|---|---|
| **D1** | Cursores são opacos, inclusive o estado IMAP por pasta. | O motor é o mesmo para os dois provedores; colunas por pasta na `com002` levariam o IMAP para dentro dele. |
| **D2** | Um caminho de mensagem crua e um parser para os dois provedores. | Ler as partes em JSON do Gmail e a estrutura do IMAP seriam dois parsers. O custo: um download busca a mensagem inteira de novo. |
| **D3** | Cliente REST do Gmail, não `Google.Apis.Gmail.v1`. | O `GoogleOAuthProvider` do Integrations já funciona assim; sem Newtonsoft, e dá para testar com um handler HTTP falso. |
| **D4** | Remoções no IMAP por snapshot, não QRESYNC. | Funciona em qualquer servidor. O custo: um `SEARCH ALL` por pasta a cada ciclo. |
| **D5** | Cursor expirado lê a caixa inteira de novo. | É raro; um diff de estado seria um segundo caminho de sincronização. |
| **D6** | O cache de conteúdo é limitado por idade, não por tamanho. | Números reais: veja [Modelo de dados](data-model.md#cache-de-conteúdo). |
| **D7** | Gmail pela API, não IMAP com senha de app. | O IMAP mostraria uma linha por etiqueta do Gmail para cada mensagem e não alcançaria os filtros (M4). |

## 7. Regras transversais

- **Restrito ao usuário.** Toda consulta passa pelas caixas de quem chama; caixa, mensagem ou conta de outro usuário
  responde 404, igual a uma que não existe.
- **`TimeProvider` em tudo** que lê o tempo.
- **O outbox** é do próprio módulo (`AddTarsOutbox`, um relay), porque ele publica um evento.
