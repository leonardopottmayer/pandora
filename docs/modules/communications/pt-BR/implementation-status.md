# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído no código e do que está desenhado mas ainda não implementado. O que vem pela
frente está em [product-plan.md](product-plan.md).

---

## Implementado (M1 — conectar e ler; M2 — organizar)

| Área | Notas |
|---|---|
| **Estrutura do módulo** | Sete projetos em camadas; schema `communications`; `com001`–`com005`; o outbox e o relay do módulo. |
| **Domínio** | `Mailbox`, `MailLabel`, `MailMessage`, `MailContent`; `MailProvider`, `MailboxStatus`, `LabelKind`, `LabelRole`, `ThreadKey`; `IMailboxProvider`; `MailErrors`. |
| **Adaptador Gmail** | REST sem o SDK do Google; `history.list` para as mudanças; dois gets em paralelo; novas tentativas com espera crescente quando há limite de taxa. |
| **Adaptador IMAP** | MailKit, para o Titan e qualquer servidor; cursores por pasta; CONDSTORE quando oferecido, senão os últimos 200 UIDs; remoções por snapshot. |
| **Sincronização** | `SyncMailboxCommand` sob uma trava por caixa; `MailboxSyncBackgroundService` a cada 2 minutos; carga inicial em lotes, com orçamento de 5 minutos por ciclo; cursor expirado → lê de novo. |
| **Falhas** | `needs-reconnect` com um aviso no IMAP (`communications.mailbox-auth-failed`, email + Telegram, en + pt-BR); `error` depois de 5 falhas; subscribers de `ExternalAccountRevoked` e `ExternalAccountDisconnected`. |
| **Leitura** | `OpenMessage` com o cache de conteúdo; retenção de 30 dias; `MimeReader`; anexos sob demanda. |
| **Evento** | `MailMessageReceived` pelo outbox, sem duplicar mensagens movidas. Sem consumidor ainda. |
| **Organizar (M2)** | `ApplyMessageAction` (lida, estrela, mover, arquivar, lixeira, etiquetas), `CreateLabel`, `RenameLabel`, `DeleteLabel`; escritas nos dois adaptadores; sem exclusão definitiva. |
| **Sincronização, M2** | `MailboxGates` (duas por caixa), `MailboxChangesStep`, junção de mensagens movidas pelo Message-ID, etiquetas que sumiram removidas depois das mudanças. |
| **API** | `GET`/`POST /mailboxes`, `PATCH`/`DELETE /mailboxes/{id}`, `POST /mailboxes/{id}/sync`, `GET /mailboxes/{id}/labels`, `GET /messages`, `GET /messages/{id}`, `GET /messages/{id}/attachments/{attachmentId}`, `POST /messages/actions`, `POST /mailboxes/{id}/labels`, `PATCH`/`DELETE /labels/{id}`. |
| **Integrations** | `CommunicationsAccountUsageSource`: a tela de conexões mostra "Email · endereço" como uso da conta. |
| **Frontend** | `/mail` e `/mail/accounts`; `prepareMailHtml` + iframe em sandbox + CSP; `MailActions`, seleção na lista, menu de etiquetas na barra lateral. |
| **Testes** | Unitários: 56 (`MailboxTests`, `ThreadKeyTests`, `LabelRoleTests`, `GmailMailboxProviderTests`). Integração: 48 (`SyncEngineTests`, `CommunicationsApiTests`, `MailActionsApiTests`, `ImapMailboxProviderTests` contra o GreenMail, `PersistenceTests`, `ProviderRegistrationTests`). Front: `prepareMailHtml.test.ts`, `MailPage.test.tsx`. |

### Verificação real (2026-10-30)

Contra um Gmail real (cerca de 64 mil mensagens) e uma caixa Titan real (cerca de 1.600):

| Verificação | Resultado |
|---|---|
| As duas caixas conectadas, carga inicial concluída, sem erros | ✅ |
| Lida no webmail → lida no Pandora | ✅ nas duas. **No Gmail levou mais de um ciclo**, alguns minutos; o plano pedia um. O momento da leitura não foi anotado, então a causa da demora é desconhecida |
| Uma newsletter não dispara requisição externa até "Mostrar imagens" | ✅ |
| Cabeçalhos chegam do mais novo para o mais antigo | Coberto por testes; não conferido à parte nas contas reais |
| Titan: CONDSTORE | ✅ Oferecido: o cursor tem `highestModSeq`, gravado só quando o servidor oferece |
| Titan: QRESYNC | Não verificado; também não é usado, porque as remoções vão por snapshot |
| Titan: ManageSieve | ❌ Não oferecido (portas 4190 e 2000 fechadas), então as regras do Titan rodam no motor do Pandora |
| Titan: o SMTP salva em Enviados? | Não verificado; o Pandora ainda não envia. Necessário no início da M3 |

As primeiras execuções reais acharam dois bugs que os testes não tinham pegado, ambos corrigidos: os `403` de limite de
taxa do Gmail derrubavam lotes inteiros, e um cabeçalho `Date` com fuso não podia ser salvo. O GreenMail normaliza as
datas, então o teste dele não pegaria o segundo; agora o `SyncEngineTests` cobre isso com um fuso pelo provedor falso.

### Verificação real da M2 (2026-10-31)

Nas mesmas contas, com mensagens descartáveis, à mão; o outbox e as etiquetas conferidos no banco:

| Verificação | Resultado |
|---|---|
| Uma etiqueta criada no Pandora aparece no Gmail e no Titan | ✅ |
| Abrir, estrela, etiquetas, mover, arquivar e lixeira chegam ao provedor | ✅ |
| Uma mudança no webmail volta para o Pandora | ✅ |
| Uma pasta renomeada no webmail do Titan aparece renomeada, com as mensagens dentro | ✅ |
| Um mover no webmail do Titan não conta como chegada | ✅ Nenhum evento veio de mover ou renomear |
| Uma pasta com mensagem não pode ser apagada; vazia, pode | ✅ no Titan |
| Lixeira no Titan | ✅ depois da correção abaixo |
| Titan: `UIDPLUS` e `MOVE` | Não registrado: o que eles mudam, um mover aparecendo um ciclo depois, não foi relatado |

A verificação achou um bug que os testes não tinham pegado: assinar uma pasta criada no Pandora virava a regra de pastas assinadas da M1, então Trash, Sent e as
outras saíram do Pandora no Titan, embora nada tenha mudado no provedor. Agora toda pasta selecionável está no escopo (decisão D8 na
[Arquitetura](architecture.md)). O retorno das pastas publicou 25 `MailMessageReceived` falsos (ponto em aberto 6 abaixo), sem efeito, porque nada os consome.

### Desvios relevantes em relação ao plano original

- **Sem `Google.Apis.Gmail.v1`.** REST com `HttpClient`, como faz o `GoogleOAuthProvider`.
- **Sem o endpoint de batch do Gmail.** Um `messages.get` por mensagem, dois por vez.
- **Só `gmail.modify` é pedido.** O plano pedia também `gmail.send` e `gmail.settings.basic`; a M3 e a M4 vão precisar de
  um novo consentimento.
- **Um ciclo lê lotes da carga inicial até o orçamento acabar**, não um lote por ciclo: um lote a cada 2 minutos levava
  mais de 3 horas em 50 mil mensagens.
- **`POST /mailboxes` não começa a carga inicial.** O primeiro ciclo começa.
- **Sem colunas por pasta na `com002`.** O estado do IMAP fica no `sync_cursor` opaco.
- **O cache de conteúdo não tem teto de tamanho**, só de idade. Veja [Modelo de dados](data-model.md#cache-de-conteúdo).
- **A lista de mensagens é uma lista simples** com "Carregar mais", não uma `Table` virtual.
- **Imagens `cid:` são URLs `data:`**, não links para a API.

## Ainda não implementado (desenhado / planejado)

| Área | Status | Fase |
|---|---|---|
| **Escrever, responder, encaminhar, anexos** | Planejado | M3 |
| **Regras, filtros do Gmail nos dois sentidos, `com006`** | Planejado | M4 |
| **Push do Gmail, Radar, mais provedores** | Futuro | Depois |

## Pontos em aberto

1. **O job é sequencial**, então a carga inicial de uma caixa grande atrasa as outras em até 5 minutos. Serve para um
   usuário. A primeira coisa a mudar com vários: uma fila separada de cargas iniciais, com concorrência limitada.
2. **Instância única.** A trava é em processo; várias instâncias precisariam de uma concessão no banco.
3. **Modo de teste no Google.** O refresh token do Gmail expira em 7 dias, então a caixa pede reconexão cerca de uma vez
   por semana. Passar de 100 usuários exigiria a verificação do Google para um escopo restrito.
4. **A busca** usa `ILIKE` sem índice e não lê o corpo. Serve no volume de uma pessoa.
5. **O download de anexo** busca a mensagem crua inteira de novo.
6. **Uma pasta que entra no escopo com mensagens** chega como email novo, com um `MailMessageReceived` falso por mensagem. Precisa ser corrigido antes de o Radar
   consumir o evento.
7. **Um mover que atravessa a varredura IMAP**, de uma pasta ainda não lida para uma já lida, ocupa dois ciclos, então não é juntado e pode disparar um evento falso.
8. **Depois de uma ação no Gmail durante uma carga inicial**, um lote antigo pode mostrar o estado anterior por até um ciclo.
9. **Só a página carregada pode ser selecionada**, e não há desfazer; a lixeira pode ser restaurada no provedor.
