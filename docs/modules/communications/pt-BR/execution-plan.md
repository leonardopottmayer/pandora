# Communications — Plano de Execução, M1 (Conectar e ler)

> **Status:** Plano de execução da fase **M1** do [product-plan](product-plan.md). Nada construído ainda.
> O passo 1 foi feito pela [fase I5](../../integrations/pt-BR/product-plan.md) do Integrations
> (várias contas): o tipo `password`, o `GetPasswordAsync`, as buscas por id da conta, a correção do
> `GetProviders` e as descrições `titan`/`imap`. As telas de caixa escolhem a conta com o
> `AccountSelect` da I5, e o Communications implementa o `IExternalAccountUsageSource`
> (caixa → "Email · <endereço>").
> 🇺🇸 [English version](../en/execution-plan.md)

Este doc é a lista de trabalho. O *porquê* de cada decisão está no [product-plan](product-plan.md); os
números de seção (§) apontam para lá.

**A M1 entrega:** conectar uma caixa Gmail e uma Titan, espelhar cabeçalhos e etiquetas (só
leitura), ler mensagens com o corpo sob demanda e publicar `MailMessageReceived`. Sem organizar (M2),
sem enviar (M3), sem regras (M4).

---

## O que já existe (não reconstruir)

| Peça | Onde | O que a M1 usa |
|---|---|---|
| Template do Communications | `backend/src/Modules/Communications/*` (7 projetos, ligados no `Program.cs`) | `CommunicationsModule` (nome/chave/schema), `CommunicationsOptions` (`Pandora:Communications`), um `CommunicationsDbContext` vazio |
| Credenciais | Integrations: `ExternalAccount`, `IExternalCredentialProvider`, `ISecretProtector` | access token do Gmail; o caminho novo de senha segue o `SaveApiKey` passo a passo |
| Consentimento incremental do Google | o `GoogleOAuthProvider` já manda `include_granted_scopes=true`; o `POST integrations/{provider}/connect` aceita `scopes` | os escopos do Gmail são pedidos **pela tela de email**, não entram nos padrões — quem só usa o Agenda nunca concede Gmail |
| Outbox | `Host/OutboxRegistration.cs`, `modelBuilder.AddTarsOutbox(schema)` | mais um relay + assembly de contracts |
| Notificações | `NotifyUserRequested` + `Channels.Infrastructure/Templates/TemplateCatalog.cs` | o aviso de falha de autenticação IMAP |
| Jobs em background | `Finances.Infrastructure/Jobs/*BackgroundService.cs` | o padrão do `MailboxSyncBackgroundService` |
| MIME | o MailKit já é usado via `Tars.Communication.Email.MailKit` (sender do sistema) | o Communications referencia o MailKit direto (credencial por caixa) |
| Front | `dompurify` instalado; o `Table` do antd v6 tem `virtual` | nenhuma dependência nova no front |

---

## Decisões fechadas

| Decisão | Valor | Motivo |
|---|---|---|
| Escopos do Gmail | pedidos no "Conectar Gmail" via `connect` com `scopes = [openid, email, gmail.modify, gmail.send, gmail.settings.basic]` | o consentimento incremental deixa os padrões intactos; os três escopos do Gmail já agora para M3/M4 não pedirem consentimento de novo |
| Credencial de senha | `AuthKind.Password` (`password`), `provider_account_id` = usuário, segredo em `access_token_enc` | §5; sem coluna nova |
| Várias caixas por provedor | contas de senha são buscadas **pelo id da conta**, nunca por (usuário, provedor) | duas caixas Titan em dois domínios é o caso realista |
| Uma conta Google por usuário | mantido (o callback OAuth faz upsert por usuário + provedor) | limite conhecido, fora da M1 — ver [Limites conhecidos](#limites-conhecidos) |
| Corpo e anexos | o adaptador busca a mensagem **RFC 822 crua** (Gmail `format=raw`, IMAP `BODY.PEEK[]`) e o MimeKit faz o parse — um parser só para os dois provedores | baixar um anexo busca a mensagem crua de novo (teto `ponytail:`: mensagens enormes; evolui para `attachments.get` do Gmail / `BODY[part]` do IMAP) |
| Cache de conteúdo | por idade: linhas não acessadas há `ContentCacheRetentionDays` (padrão 30) são apagadas pelo job de sincronização | fecha a questão em aberto 2 do product-plan com a regra mais simples |
| Chave de mensagem IMAP | `provider_message_id = "{label_id}:{uid}"` (label = a linha `com002` da pasta) | UIDs são por pasta; uma troca de `UIDVALIDITY` ressincroniza a pasta e descarta as linhas dela |
| Prévia no IMAP | vazia até a mensagem ser aberta, depois preenchida a partir do texto já parseado | IMAP não tem prévia; um fetch parcial de corpo por mensagem não compensa na M1 |
| Pastas IMAP | pastas assinadas (`LIST SUBSCRIBED`); todas as selecionáveis se nenhuma estiver assinada | §7 |
| Laço de sincronização | um `BackgroundService`, caixas em sequência, a cada `SyncInterval` (padrão 2 min); cada ciclo = **incremental, depois um lote da carga inicial** | escala pessoal; `ponytail:` laço sequencial, paralelizar por caixa se ficar para trás |
| "Sincronizar agora" | roda um ciclo dentro da requisição, sob a trava da caixa | sem sinalização entre requisição e job |
| Busca | `ILIKE` em remetente/assunto/prévia, dentro das caixas do usuário | `ponytail:` adicionar `pg_trgm` quando ficar lento |
| Paginação | keyset em `(sent_at, id)` decrescente | estável enquanto chegam emails novos |

---

## Passos

Cada passo termina verde (build + os testes dele) antes do próximo. Pontos de commit sugeridos, no
espelho de F1a/F1b/F1c do Files: **M1a** = passos 1–2, **M1b** = passos 3–7, **M1c** = passos 8–9.
Commit só quando pedido.

### Passo 1 — Integrations: a credencial `password`

- `AuthKind.Password = new("password")` + `FromValue`.
- `ExternalAccount.ConnectPassword(userId, provider, username, passwordEnc, tp)` (nome de exibição =
  o usuário) e `ReplacePassword(passwordEnc, tp)`.
- `CREATE TABLE` do `int001`: o `chk_int001_auth_kind` ganha `'password'` (editado no lugar, pré-1.0).
- `IExternalAccountRepository.FindAsync(userId, provider, providerAccountId)`.
- `PasswordProviderDescriptor(Name, DisplayName)` + o registry dele; `AddIntegrationsPasswordProviders()`
  registra `titan` ("Titan Email") e `imap` ("IMAP").
- `SavePasswordCommand` — upsert por (usuário, provedor, usuário da caixa), protege, event log
  `connected`/`reconnected`; devolve o id da conta. Endpoint `PUT integrations/{provider}/password`,
  corpo `{ username, password }`, resposta `{ externalAccountId }`.
- `IExternalCredentialProvider.GetPasswordAsync(userId, externalAccountId)` →
  `Result<PasswordCredential(Username, Password)>`; falha quando a conta não é do usuário ou não é
  `password`.
- `GetProvidersQueryHandler`: o `ToDictionary(a => a.Provider)` estoura com duas contas no mesmo
  provedor — trocar por um lookup ("conectado" = qualquer conta). O catálogo lista os provedores de
  senha com `authKind = password`.
- `ConnectionsPage`: uma conta `password` aparece como uma api-key (status + desconectar); sem
  formulário ali — o Titan é adicionado pela tela de email.
- **Testes:** domínio (connect/replace password); integração `PasswordTests` no espelho de
  `ApiKeyTests` (salvar → listado, sem texto puro no DTO da conta, upsert por usuário, dois usuários →
  duas contas, provedor desconhecido → 422, senha vazia → 422).

### Passo 2 — Communications: schema, persistência, outbox

- Migrations em `migrations/migrations/communications/` (uma pasta por arquivo, `.up.sql` +
  `.down.sql`, mesmo padrão de nomes do `integrations/`): `create-schema-communications`,
  `com001_mailbox`, `com002_mail_label`, `com003_mail_message`, `com004_mail_message_label`,
  `com005_mail_content`, `tars-outbox-message`. O `com006_mail_rule` espera a M4.
  - Índices: `uq (mailbox_id, provider_message_id)`; `(mailbox_id, sent_at desc, id desc)`;
    `(mailbox_id, rfc822_message_id)`; `uq (mailbox_id, provider_label_id)`; `com004 (label_id, message_id)`;
    `com005 (last_accessed_at)`.
  - Checks em toda coluna de enum, com valores hifenizados (`needs-reconnect`).
  - O `com001` acrescenta `message_total_estimate` (para a barra de progresso da carga inicial) às
    colunas do product-plan.
- Entidades + configurações EF + repositórios (`IStandardRepository` do Tars);
  `modelBuilder.AddTarsOutbox(schema: CommunicationsModule.Schema)`.
- Ligações: `Tars:Data:Connections:communications` no `appsettings.json`, no `docker-compose.yml` e na
  factory de testes (conexão + `communications` no `SchemasToInclude` do Respawn);
  `OutboxRegistration`: `AddTarsOutboxRelay(CommunicationsModule.DatabaseKey)` e o assembly
  Communications.Contracts no registry de tipos de evento.
- **Testes:** a suíte de integração sobe com o schema novo (os testes existentes continuam verdes).

### Passo 3 — Domínio e a porta do provedor

- Valor `MailProvider` (`gmail`/`titan`/`imap`; o `titan` carrega os hosts predefinidos
  `imap.titan.email:993`, `smtp.titan.email:465`, SSL).
- Agregado `Mailbox`: `Connect…`, `StartBackfill(total)`, `AdvanceBackfill(cursor)`,
  `CompleteBackfill`, `SetSyncCursor`, `RecordSuccess`, `RecordFailure(error)` (→ `error` com 5
  seguidas), `MarkNeedsReconnect`. `MailLabel`, `MailMessage` (+ ligações com etiquetas),
  `MailContent`.
- `IMailboxProvider` (Domain/Ports), subconjunto da M1: `GetCapabilities`, `TestConnection`,
  `ListLabels`, `StartBackfill` (devolve o cursor incremental *do momento da conexão* + o total),
  `FetchBackfillBatch`, `FetchChanges(cursor)`, `GetRawMessage`. Os resultados são records neutros
  (`RemoteLabel`, `RemoteMessage` com o conjunto completo de etiquetas e flags, `RemoteChanges` =
  upserts + ids removidos + novo cursor, ou `CursorExpired`).
- `IMailboxProviderResolver` escolhe o adaptador pelo `MailProvider` (DI com chave; `titan` e `imap` →
  IMAP).
- `ThreadKey.From(messageId, references)` para o IMAP.
- **Testes:** transições de estado da caixa (contador de falhas, needs-reconnect), thread key,
  helpers de mapeamento de papel das etiquetas.

### Passo 4 — Adaptador IMAP (MailKit)

- Conecta com host/porta/segurança da caixa + `GetPasswordAsync`; `AuthenticationException` vira um
  erro tipado `MailboxAuthFailed` (que leva a `needs-reconnect`).
- Etiquetas a partir das pastas: flags SPECIAL-USE (`\Sent`, `\Trash`, `\Junk`, `\Drafts`,
  `\Archive`) → `role`; `INBOX` → `inbox`; todas `is_exclusive = true`.
- Por pasta, no início da carga inicial: guarda `UIDVALIDITY` e `UIDNEXT` (a fronteira).
  Incremental: `UID ≥ fronteira` = nova; `CHANGEDSINCE highest_modseq` quando o servidor anuncia
  CONDSTORE, senão relê as flags dos últimos N UIDs (`ponytail:` janela; diff completo de flags se
  perder mudanças); removidas por `VANISHED` (QRESYNC) ou diff do conjunto de UIDs. Carga inicial: UIDs
  abaixo da fronteira, decrescente, lotes de 500, buscando `ENVELOPE FLAGS BODYSTRUCTURE RFC822.SIZE`
  + `References`.
- `GetRawMessage` = `BODY.PEEK[]` (nunca marca como lida).
- **Testes:** integração contra o **GreenMail** (Testcontainers): semear mensagens por SMTP, conectar,
  carga inicial, entregar mais uma → vista como nova, marcar `\Seen` por outro cliente → vista como
  lida, apagar → removida. Cobre o caminho sem CONDSTORE.

### Passo 5 — Adaptador Gmail

- `Google.Apis.Gmail.v1`, `GoogleCredential.FromAccessToken(GetAccessTokenAsync(userId, "google"))`.
  Recusa uma conta Google cujos escopos não têm `gmail.modify` com `MissingScopes` (a tela pede
  reconexão).
- `ListLabels` = `labels.list` (marcadores do sistema mapeiam para papéis; `is_exclusive = false`).
- `StartBackfill` = `users.getProfile` → `historyId` (cursor) + `messagesTotal`. Carga inicial =
  páginas de `messages.list` (mais novas primeiro) + `messages.get(format=metadata)` com os cabeçalhos
  `From, To, Cc, Subject, Date, Message-ID, References`, em **batch request** de 100.
  `has_attachments` = `multipart/mixed` no nível de cima.
- Incremental = `history.list(startHistoryId)`; todo id tocado é relido com `format=metadata` (estado
  completo → upsert; 404 no get = removida). History 404 → `CursorExpired` → o motor faz uma
  ressincronização de estado (todos os ids, diff).
- `GetRawMessage` = `messages.get(format=raw)`.
- **Testes:** nenhum automatizado (não existe Gmail falso confiável) — manual contra a conta real, no
  passo 9.

### Passo 6 — Motor de sincronização

- `SyncMailboxCommand(mailboxId)` (Application), sob uma trava `SemaphoreSlim` por caixa (mesma
  observação do `ExternalCredentialProvider`): diff de etiquetas → mudanças incrementais → um lote da
  carga inicial → retenção do cache de conteúdo. As chamadas ao provedor ficam fora de transação; cada
  aplicação é uma unit of work.
- `MailMessageReceived(EventId, OccurredAt, UserId, MailboxId, MessageId, From, Subject, Labels)` no
  Communications.Contracts, publicado **só pelo caminho incremental** e só quando a caixa não tem linha
  com o mesmo `rfc822_message_id` (questão em aberto 1 do product-plan).
- Falhas: `MailboxAuthFailed` → `needs-reconnect` e, no IMAP, `NotifyUserRequested`
  (`communications.mailbox`, template `communications.mailbox-auth-failed`, entrada nova no
  `TemplateCatalog`, en + pt-BR); passageira → `RecordFailure`.
- `MailboxSyncBackgroundService` (Infrastructure/Jobs): a cada `SyncInterval`, cada caixa
  `active`/`syncing`/`error` em sequência; pula `needs-reconnect`.
- Assinantes: `ExternalAccountRevoked` e `ExternalAccountDisconnected` → as caixas daquela conta vão
  para `needs-reconnect` (dados mantidos).
- `CommunicationsOptions`: `SyncInterval`, `BackfillBatchSize`, `ContentCacheRetentionDays`.
- **Testes:** integração com um `IMailboxProvider` falso registrado na factory: a carga inicial
  preenche linhas sem eventos; uma chegada incremental publica um evento; um `rfc822_message_id` que
  reaparece não publica nada; falha de autenticação → `needs-reconnect` + um `NotifyUserRequested`;
  evento de revogação → `needs-reconnect`.

### Passo 7 — API

`/api/v1/communications`, `[Authorize]`, toda consulta restrita às caixas de quem chama:

- `GET mailboxes` (status, último erro, progresso da carga inicial = linhas / `message_total_estimate`).
- `POST mailboxes` `{ provider, externalAccountId, address?, displayName?, imapHost?, imapPort?, smtpHost?, smtpPort?, security? }`
  — valida que a conta é de quem chama e do tipo certo, roda `TestConnection`, então cria a caixa e
  começa a carga inicial. Teste que falha → 422 com a mensagem do provedor.
- `PATCH mailboxes/{id}` (nome de exibição, servidor), `DELETE mailboxes/{id}` (só os dados locais; a
  credencial fica no Integrations).
- `POST mailboxes/{id}/sync` (um ciclo, devolve a caixa).
- `GET mailboxes/{id}/labels` (com contagem de não lidas).
- `GET messages?mailboxId&labelId&unread&q&threadKey&before&limit` (keyset).
- `GET messages/{id}` — metadados + conteúdo (buscado e posto em cache na primeira abertura; atualiza
  `last_accessed_at`).
- `GET messages/{id}/attachments/{attachmentId}` — em stream, `Content-Disposition` a partir da parte;
  também serve as imagens `cid:`.
- **Testes:** integração com o provedor falso: caixa/mensagem de outro usuário → 404; criar com
  teste de conexão falhando → 422; a lista pagina por data; abrir uma mensagem põe em cache uma vez só.

### Passo 8 — Web (`client-web/src/modules/communications`)

- `models.ts`, `services/communications.service.ts`, `hooks/` (TanStack Query, mesmo formato de
  integrations/files).
- **`MailPage`** (`/mail`): esquerda — caixas e etiquetas com contagem de não lidas; centro — `Table`
  do antd com `virtual`, "carregar mais" por keyset, filtro por conta, busca; direita — painel de
  leitura + conversa.
  - `prepareMailHtml(html)`: DOMPurify, depois troca todo `src` remoto (img, srcset, `url()` de CSS)
    por um placeholder bloqueado, a menos que "Mostrar imagens"; `cid:` → o endpoint de anexo; links
    ganham `target=_blank rel=noopener noreferrer`.
  - Renderizado em `<iframe sandbox="allow-popups allow-popups-to-escape-sandbox" srcdoc=…>` com um
    `<meta http-equiv="Content-Security-Policy">` que só permite `img-src` de `data:` e da origem da
    API até as imagens serem liberadas.
- **`MailAccountsPage`** (`/mail/accounts`): "Conectar Gmail" → `integrations/google/connect` com os
  escopos do Gmail e `redirectAfter=/mail/accounts?connected=google` → na volta, `POST mailboxes`;
  formulário "Adicionar Titan / IMAP" → `PUT integrations/{titan|imap}/password` → `POST mailboxes`;
  status, progresso, "Sincronizar agora", reconectar quando `needs-reconnect`.
- Router (`mail`, `mail/accounts`), `navigation.tsx` (grupo "Email"), i18n `mail.*` no `en.json` e
  no `pt-BR.json`.
- **Testes:** vitest do `prepareMailHtml` (imagem remota bloqueada, liberada depois do opt-in, script
  removido, `cid:` reescrito) — é a fronteira de segurança da tela.

### Passo 9 — Verificação real e docs

- Manual, contra as contas reais — o **pronto quando** da M1 no product-plan:
  1. Gmail e Titan conectados; cabeçalhos chegam dos mais novos para os mais antigos; a barra de
     progresso anda.
  2. Uma mensagem lida em cada webmail aparece como lida no Pandora em um ciclo.
  3. Abrir uma newsletter não dispara requisição remota (devtools do navegador) até "Mostrar imagens".
- Checklist do Titan (product-plan §14): CONDSTORE/QRESYNC (`Capabilities` do MailKit), se o SMTP salva
  em Enviados (resposta necessária até a M3), ManageSieve (porta 4190). Resultados registrados no
  product-plan.
- Docs: `modules/communications/README.md` + `pt-BR/README.md` e a linha no `docs/README.md`; status
  do product-plan → "M1 implementada".

---

## Limites conhecidos

- **Uma conta Google por usuário.** O callback OAuth faz upsert por (usuário, provedor), então uma
  segunda conta Google substitui a primeira. Tirar esse limite significa buscar contas OAuth pelo id da
  conta de ponta a ponta (callback, `GetAccessTokenAsync`); é uma mudança no Integrations, agendada
  quando um segundo Gmail for necessário.
- **Prévia no IMAP** fica vazia até a mensagem ser aberta.
- **Download de anexo** busca a mensagem crua inteira de novo.
