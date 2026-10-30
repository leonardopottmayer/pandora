# Communications — Plano de Execução, M1 (Conectar e ler)

> **Status:** A fase **M1** está pronta (2026-10-30). Este é o histórico de como foi construída; o que existe hoje está
> nos documentos de tópicos, a começar pelo [Status de implementação](implementation-status.md).
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
| Várias contas Google | cada caixa Gmail guarda o id da sua conta Google e pede o token por esse id | a I5 do Integrations tirou o limite de uma conta |
| Corpo e anexos | o adaptador busca a mensagem **RFC 822 crua** (Gmail `format=raw`, IMAP `BODY.PEEK[]`) e o MimeKit faz o parse — um parser só para os dois provedores | baixar um anexo busca a mensagem crua de novo (teto `ponytail:`: mensagens enormes; evolui para `attachments.get` do Gmail / `BODY[part]` do IMAP) |
| Cache de conteúdo | por idade: linhas não acessadas há `ContentCacheRetentionDays` (padrão 30) são apagadas pelo job de sincronização | a regra mais simples; os números reais a confirmaram ([Modelo de dados](data-model.md#cache-de-conteúdo)) |
| Chave de mensagem IMAP | `provider_message_id` montado pelo adaptador a partir do caminho da pasta, do `UIDVALIDITY` e do UID | UIDs são por pasta; uma troca de `UIDVALIDITY` ressincroniza a pasta e descarta as linhas dela |
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

### Passo 2 — Communications: schema, persistência, outbox ✅

> **Construído (2026-10-27).** Além da lista abaixo: o `com001` também tem `backfill_completed_at` e
> `uq (user_id, external_account_id)` (uma caixa por credencial); o `com003` separa o remetente em `from_name`/`from_address` e indexa
> `(mailbox_id, thread_key)` para as conversas. O assembly Communications.Contracts entra no registry
> de tipos de evento no passo 6, com o primeiro evento. Teste: `PersistenceTests` (ida e volta pelos
> repositórios).


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

### Passo 3 — Domínio e a porta do provedor ✅

> **Construído (2026-10-27).** Mudanças em relação à lista abaixo:
> - **Cursores opacos, IMAP incluído.** O estado por pasta do IMAP (UIDVALIDITY, UIDNEXT, MODSEQ) fica
>   dentro do `sync_cursor` da caixa, que só o adaptador IMAP lê, então o motor de sync é o mesmo para
>   os dois provedores. O `com002` perdeu `uid_validity`/`highest_modseq`.
> - **Sem coluna `security`.** A porta decide (993/465 TLS, 143/587 STARTTLS, o
>   `SecureSocketOptions.Auto` do MailKit): um valor só para os dois servidores errava o par comum
>   993 + 587.
> - **Sem resolver.** Os adaptadores são serviços com chave, uma por valor de provedor (o IMAP em
>   `titan` e `imap`); o motor pede pela chave.
> - **Remoções sem QRESYNC** vêm como `RemoteLabelSnapshot` (todas as mensagens que uma etiqueta tem
>   agora), não como diff por prefixo de id. Um histórico que o provedor não conhece mais é o erro
>   `SyncCursorExpired`.
> - `Mailbox.StartBackfill(syncCursor, backfillCursor, total, now)` também recomeça;
>   `AdvanceBackfill(null)` conclui (sem `CompleteBackfill` separado); `Reconnect()` sai de
>   `needs-reconnect`.
> - Testes: novo `Pottmayer.Pandora.Modules.Communications.Tests` (35: caixa, thread key, papéis de
>   etiqueta).


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

### Passo 4 — Adaptador IMAP (MailKit) ✅

> **Construído (2026-10-27)** — `Infrastructure/Imap/ImapMailboxProvider` (com chave em `titan` e
> `imap`), MailKit 4.18.2. Mudanças em relação à lista abaixo:
> - **Id da mensagem** = `"{uidvalidity}:{uid}:{caminho da pasta}"` (o caminho por último, porque pode
>   ter `:`).
> - **Remoções sempre por retrato:** todo ciclo manda o conjunto de UIDs de cada pasta
>   (`UID SEARCH ALL`); ainda sem caminho QRESYNC/`VANISHED` (`ponytail:` acrescentar se pastas grandes
>   deixarem a busca lenta).
> - **Um UIDVALIDITY trocado** falha com `SyncCursorExpired` em vez de ressincronizar só aquela pasta.
>   O passo 6 trata apagando as mensagens da caixa e recomeçando a carga inicial (evento raro).
> - **Uma pasta que aparece depois da carga inicial** (criada ou assinada) chega inteira como upsert;
>   uma mensagem movida para ela mantém o Message-ID, então a checagem de duplicata do passo 6 não
>   publica evento para ela.
> - A carga inicial lê uma pasta por lote, a INBOX primeiro. O ramo CONDSTORE está escrito mas sem
>   cobertura (o GreenMail não tem CONDSTORE); o passo 9 confere no Titan.
> - Testes: `ImapMailboxProviderTests` (2) contra `greenmail/standalone:2.1.0`: senha errada → falha
>   de autenticação; carga inicial em lotes da mais nova para a mais antiga, thread key, uma chegada
>   nova, lida no webmail, busca crua sem marcar como lida, movida para uma pasta nova, apagada.


- Conecta com host/porta da caixa + `GetPasswordAsync`; `AuthenticationException` vira um
  erro tipado `MailboxAuthFailed` (que leva a `needs-reconnect`).
- Etiquetas a partir das pastas: flags SPECIAL-USE (`\Sent`, `\Trash`, `\Junk`, `\Drafts`,
  `\Archive`) → `role`; `INBOX` → `inbox`; todas `is_exclusive = true`.
- Por pasta, no início da carga inicial: guarda `UIDVALIDITY` e `UIDNEXT` (a fronteira) no cursor de sync.
  Incremental: `UID ≥ fronteira` = nova; `CHANGEDSINCE highest_modseq` quando o servidor anuncia
  CONDSTORE, senão relê as flags dos últimos N UIDs (`ponytail:` janela; diff completo de flags se
  perder mudanças); removidas por `VANISHED` (QRESYNC) ou diff do conjunto de UIDs. Carga inicial: UIDs
  abaixo da fronteira, decrescente, lotes de 500, buscando `ENVELOPE FLAGS BODYSTRUCTURE RFC822.SIZE`
  + `References`.
- `GetRawMessage` = `BODY.PEEK[]` (nunca marca como lida).
- **Testes:** integração contra o **GreenMail** (Testcontainers): semear mensagens por SMTP, conectar,
  carga inicial, entregar mais uma → vista como nova, marcar `\Seen` por outro cliente → vista como
  lida, apagar → removida. Cobre o caminho sem CONDSTORE.

### Passo 5 — Adaptador Gmail ✅

> **Construído (2026-10-27)** — `Infrastructure/Gmail/GmailMailboxProvider` (com chave em `gmail`).
> Mudanças em relação à lista abaixo:
> - **Sem SDK.** A API REST do Gmail com `HttpClient` + `System.Text.Json`, como o `GoogleOAuthProvider`
>   do Integrations já faz: sem `Google.Apis.*` + Newtonsoft, e o adaptador fica testável com um handler
>   HTTP falso.
> - **Sem batch request.** Um `messages.get` por mensagem, 4 por vez, dentro da cota por usuário
>   (`ponytail:` endpoint de batch do Gmail se a carga inicial ficar lenta).
> - `UNREAD`/`STARRED` são flags, não etiquetas; todo outro id de etiqueta (sistema, `CATEGORY_*`,
>   usuário) é etiqueta. A carga inicial inclui spam e lixeira. O `sent_at` é o `internalDate` do Gmail.
> - **Escopos:** o token precisa ter `gmail.modify` (ou `https://mail.google.com/`), checado antes de
>   qualquer chamada; um 403 de escopo também é `MissingScopes`; um 401 é `MailboxAuthFailed`.
> - Testes: 11 unitários sobre respostas prontas do Gmail (`GmailMailboxProviderTests`) +
>   `ProviderRegistrationTests` (todo valor de provedor resolve um adaptador). A conta real continua no
>   passo 9.


- `Google.Apis.Gmail.v1`, `GoogleCredential.FromAccessToken(GetAccessTokenAsync(userId, mailbox.ExternalAccountId))`.
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

> **Mudou (2026-10-29)** — a primeira carga inicial real (≈64 mil mensagens) falhava em todo lote por
> alguns `403` que o Gmail manda quando limita a taxa, e nada era salvo. Agora cada pedido ao Gmail é
> repetido depois de 1, 2, 4, 8 e 16 s (ou do `Retry-After` do servidor) em `429`, `5xx` e `403` com
> motivo `rateLimitExceeded`/`userRateLimitExceeded`; um erro que persiste leva o motivo do Gmail no
> último erro da caixa, e o `ParallelGets` passou de 4 para 2. Teste:
> `A_throttled_request_is_retried_and_a_lasting_one_names_the_reason`.

### Passo 6 — Motor de sincronização ✅

> **Construído (2026-10-27)** — `SyncMailboxCommand`, `MailboxSyncBackgroundService`, os dois
> assinantes de conta, `MailMessageReceived` (Contracts, no registry de tipos de evento) e
> `CommunicationsAccountUsageSource` ("Email · <endereço>" na tela de conexões). Detalhes e mudanças
> em relação à lista abaixo:
> - **`SyncCursorExpired`** (histórico do Gmail expirado, UIDVALIDITY do IMAP trocado) apaga as
>   mensagens da caixa e recomeça a carga inicial: sem eventos, e os corpos em cache vão junto.
> - **Quem é avisado.** Senha IMAP/Titan recusada → `needs-reconnect` e um aviso
>   (`communications.mailbox-auth-failed`, email + Telegram, en + pt-BR), só na mudança. Falha de
>   autenticação do Gmail e `MissingScopes` → `needs-reconnect` sem aviso (o Integrations já avisa
>   quando o acesso é revogado; a tela mostra os escopos que faltam).
> - **Uma etiqueta que o provedor deixou de listar** é apagada; no IMAP (etiqueta exclusiva, ou seja,
>   uma pasta) as mensagens vão junto, no Gmail elas só perdem a etiqueta.
> - **A retenção do cache de conteúdo** roda uma vez por ciclo do job para todas as caixas (um delete
>   indexado), não dentro do comando de cada caixa.
> - Mensagens que ficam sem etiqueta por um retrato são apagadas em bloco: o EF não consegue apagar uma
>   entidade rastreada cujas coleções JSON (`to_recipients`) ele rastreia.
> - `Mailbox.Reconnect()` é chamado pelo `sync` e pelo `PATCH` do passo 7 quando o provedor aceita a
>   credencial de novo.
> - Testes: `SyncEngineTests` (10) com um `FakeMailProvider` roteirizado em `titan`: carga inicial sem
>   eventos, um evento por chegada, flags e remoções, remoção por retrato, mesmo Message-ID → sem
>   evento, cursor expirado → releitura sem eventos, senha recusada → needs-reconnect + um aviso, falha
>   transitória contada e depois limpa, conta revogada → needs-reconnect, uso na API de contas.


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
- `CommunicationsOptions`: `SyncInterval`, `BackfillBatchSize`, `BackfillBudget`, `ContentCacheRetentionDays`.
- **Testes:** integração com um `IMailboxProvider` falso registrado na factory: a carga inicial
  preenche linhas sem eventos; uma chegada incremental publica um evento; um `rfc822_message_id` que
  reaparece não publica nada; falha de autenticação → `needs-reconnect` + um `NotifyUserRequested`;
  evento de revogação → `needs-reconnect`.

> **Mudou (2026-10-29)** — um ciclo não lê mais um lote só da carga inicial: lê lotes seguidos até a
> carga terminar ou o `BackfillBudget` acabar (sempre ao menos um lote). O padrão passou de 1 para 5
> minutos depois do teste real com o Gmail: maior que o intervalo de 2 minutos, a carga inicial roda
> quase o tempo todo em vez de metade. Com um lote
> por ciclo de 2 minutos, uma caixa de 50 mil mensagens levava mais de 3 horas. Teste:
> `One_cycle_reads_backfill_batches_until_the_backfill_ends` (lotes de 2, cinco mensagens, um ciclo).

### Passo 7 — API ✅

`/api/v1/communications`, `[Authorize]`, toda consulta restrita às caixas de quem chama:

- `GET mailboxes` (status, último erro, progresso da carga inicial = linhas / `message_total_estimate`).
- `POST mailboxes` `{ provider, externalAccountId, address?, displayName?, imapHost?, imapPort?, smtpHost?, smtpPort? }`
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

> **Construído (2026-10-28)** — `Presentation/Controllers/CommunicationsController`, comandos
> `ConnectMailbox`, `UpdateMailbox`, `DeleteMailbox`, `SyncMailboxNow`, `OpenMessage`, consultas
> `GetMailboxes`, `GetLabels`, `ListMessages`, `GetAttachment`. Mudanças em relação à lista acima:
> - **`POST mailboxes` não começa a carga inicial**: cria a caixa sem cursor, e o primeiro ciclo de
>   sincronização (o próximo tick do job, ou `POST mailboxes/{id}/sync`) começa. A tela chama `sync`
>   logo depois de conectar. Conta que já tem caixa → 409. A conta precisa ser de quem chama (senão
>   404) e do tipo do provedor (`gmail` ↔ `google`, senão 422); o endereço padrão é o email do Google
>   ou o usuário do IMAP.
> - **`POST mailboxes/{id}/sync` reconecta**: uma caixa em `needs-reconnect` é testada antes; teste
>   recusado → 422 e nada muda, teste aceito → `Reconnect()` e o ciclo roda. Um ciclo que falha não é
>   erro HTTP: a caixa volta com o status e o `lastError`.
> - **`PATCH`** troca o nome de exibição; servidor novo (os quatro campos, ou nenhum) é testado antes
>   e, se passar, também reconecta.
> - **Cursor da página**: `nextCursor` é `"{ticks utc do sent_at}.{id}"`, devolvido como `before`;
>   cursor inválido → 422. `limit` vai de 1 a 100 (padrão 50). `q` é um trecho, sem diferenciar
>   maiúsculas, do assunto, da prévia e do remetente (`ILIKE`, sem índice — basta no volume de uma
>   pessoa).
> - **Abrir uma mensagem** não marca como lida (a M1 só lê) e preenche a prévia do IMAP com o texto
>   (ou o HTML sem as tags). O corpo é lido com MimeKit no Application.
> - **Id do anexo** é a posição da parte entre as partes do corpo (estável para os mesmos bytes); as
>   partes são arquivos ou imagens inline com `Content-ID`. O download manda `nosniff`, e só
>   PNG/JPEG/GIF/WebP/BMP vão `inline` — o resto (HTML, SVG, PDF) vai `attachment`, porque a parte vem
>   de quem mandou o email.
> - Testes: `CommunicationsApiTests` (6): teste recusado → 422 e nenhuma linha; conta de outro
>   provedor → 422, segunda caixa na mesma conta → 409; paginação do mais novo + busca + etiquetas com
>   contagem + cursor inválido; abrir busca a mensagem crua uma vez só e serve o PDF como
>   `attachment`; caixa e mensagem de outro usuário → 404 em todas as rotas; sincronizar agora
>   para/reconecta e excluir apaga as linhas.

### Passo 8 — Web (`client-web/src/modules/communications`) ✅

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

> **Construído (2026-10-29)** — `client-web/src/modules/communications`: `MailPage`,
> `MailAccountsPage`, `ConnectMailboxModal`, `FixConnectionModal`, `MailSidebar`, `MessageList`,
> `MessageReader`, `lib/prepareMailHtml`. Mudanças em relação à lista acima:
> - **Imagens `cid:` viram URLs `data:`**, não a origem da API: o endpoint do anexo exige o token
>   Bearer, e uma URL `blob:` pertence à origem do app, que o iframe em sandbox não compartilha. Por
>   isso a CSP do iframe é `img-src data:` até "Mostrar imagens" (depois `data: https: http:`), mais
>   `default-src 'none'; style-src 'unsafe-inline'`.
> - **A lista é uma lista simples**, não uma `Table` virtual: páginas de 50 com "Carregar mais"
>   (`ponytail:` no `MessageList`, virtualizar se a rolagem longa ficar lenta).
> - **Na volta do Google** (`/mail/accounts?connect=gmail`) o modal de adicionar reabre no Gmail com a
>   conta já escolhida; o usuário confirma, e então `POST mailboxes` e `sync`. O `AccountSelect`
>   ganhou `excludeIds`, para não oferecer uma conta que já tem caixa.
> - **Reconectar**: no Gmail o navegador vai ao Google com o id da conta e volta em `?sync={id}`; no
>   Titan/IMAP abre "Editar conexão" (senha nova troca o segredo no Integrations, servidores IMAP novos
>   passam pelo `PATCH`), e então `sync`.
> - A lista de caixas consulta de novo a cada 15 s enquanto há carga inicial ou ciclo rodando, para a
>   barra de progresso andar. Anexos baixam pelo cliente da API (o `downloadAttachment` do notes);
>   imagens inline que o HTML mostra não aparecem de novo na lista de anexos.
> - Testes: `prepareMailHtml.test.ts` (6: imagens externas bloqueadas e liberadas, `url()` em blocos
>   de estilo, scripts e handlers removidos, `cid:` resolvido, links em nova aba, CSP) e
>   `MailPage.test.tsx` (a mensagem aberta fica no sandbox, sem a URL do rastreador até "Mostrar
>   imagens").

### Passo 9 — Verificação real e docs ✅

> **Pronto (2026-10-30).** Os resultados estão no [Status de implementação](implementation-status.md#verificação-real-2026-10-30). Além
> da lista abaixo, a verificação achou que um cabeçalho `Date` com fuso não podia ser salvo (o Npgsql só grava UTC); agora
> o `MailMessage` converte. Uma mensagem lida no Gmail levou mais de um ciclo para aparecer. A pergunta do SMTP passou para
> o início da M3, porque o Pandora ainda não envia.


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

- **Prévia no IMAP** fica vazia até a mensagem ser aberta.
- **Download de anexo** busca a mensagem crua inteira de novo.
