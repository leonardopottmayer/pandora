# Status de implementação

[← Voltar ao índice](README.md)

O que está construído, como foi verificado com contas reais e os pontos em aberto conhecidos. O que falta
está em [product-plan.md](product-plan.md).

---

## Implementado

As quatro fases estão construídas: M1 conectar e ler, M2 organizar, M3 enviar, M4 regras.

| Área | Notas |
|---|---|
| **Estrutura do módulo** | Sete projetos em camadas; schema `communications`; `com001`–`com006`; o outbox e o relay do módulo. |
| **Domínio** | `Mailbox`, `MailLabel`, `MailMessage`, `MailContent`, `MailRule`; `MailProvider`, `MailboxStatus`, `LabelKind`, `LabelRole`, `ThreadKey`; `IMailboxProvider`; `MailErrors`. |
| **Adaptador Gmail** | REST sem o SDK do Google; `history.list` para as mudanças; dois gets em paralelo; novas tentativas com espera crescente quando há limite de taxa. |
| **Adaptador IMAP** | MailKit, para o Titan e qualquer servidor; cursores por pasta; CONDSTORE quando oferecido, senão os últimos 200 UIDs; remoções por snapshot. |
| **Sincronização** | `SyncMailboxCommand` sob travas por caixa (`MailboxGates`, duas por caixa); `MailboxSyncBackgroundService` a cada 2 minutos; carga inicial em lotes, com orçamento de 5 minutos por ciclo; cursor expirado → lê de novo; mensagens movidas no webmail são juntadas pelo Message-ID. |
| **Falhas** | `needs-reconnect` com um aviso no IMAP (`communications.mailbox-auth-failed`, email + Telegram, en + pt-BR); `error` depois de 5 falhas; subscribers de `ExternalAccountRevoked` e `ExternalAccountDisconnected`. |
| **Leitura** | `OpenMessage` com o cache de conteúdo; retenção de 30 dias; `MimeReader`; anexos sob demanda. |
| **Evento** | `MailMessageReceived` pelo outbox, nunca para mensagem movida nem para cópia em Enviados/Rascunhos. O motor de regras do Pandora o consome. |
| **Organizar** | `ApplyMessageAction` (lida, estrela, mover, arquivar, lixeira, etiquetas), `CreateLabel`, `RenameLabel`, `DeleteLabel`; escritas nos dois adaptadores; sem exclusão definitiva. |
| **Envio** | `SendMessage`, `SendKeys`, `GetComposeDraft`, `MessageComposer`; SMTP com cópia em Enviados detectada; upload multipart do Gmail; sem repetir em caso de dúvida. |
| **Regras** | `com006`, `mail/rules`. No Gmail uma regra é um filtro, nos dois sentidos: todo ciclo lista os filtros e traz o que é novo, sumiu ou mudou. Nos outros, o `RunMailRulesHandler` as roda no `MailMessageReceived`. Um filtro que não cabe no modelo fica somente leitura, nunca reescrito. |
| **Salvar no Notes** | "Salvar no Notes" no leitor copia o texto da mensagem para uma página nova via `INoteImporter` (Notes.Abstractions). Texto puro tem preferência; email só em HTML vira texto pelo tokenizador do MimeKit. Anexos e formatação não são copiados, e a página não acompanha mudanças posteriores do email. |
| **API** | Ver [Referência da API](api-reference.md). |
| **Integrations** | `CommunicationsAccountUsageSource`: a tela de conexões mostra "Email · endereço" como uso da conta. |
| **Frontend** | `/mail`, `/mail/accounts` e `/mail/rules`; `prepareMailHtml` + iframe em sandbox + CSP; `MailActions`, seleção na lista, menu de etiquetas na barra lateral; `ComposeDrawer`; a mensagem aberta fica na URL (`/mail?message={id}`). |
| **Testes** | Unitários: `MailboxTests`, `MessageComposerTests`, `ThreadKeyTests`, `LabelRoleTests`, `GmailMailboxProviderTests` e os testes de regras. Integração: `SyncEngineTests`, `CommunicationsApiTests`, `MailActionsApiTests`, `SendMessageApiTests`, `ImapMailboxProviderTests` contra o GreenMail, `PersistenceTests`, `ProviderRegistrationTests`. Front: `prepareMailHtml.test.ts`, `MailPage.test.tsx`, `ComposeDrawer.test.tsx`, `composeBody.test.ts`. |

### Verificado com contas reais

Cada fase foi verificada à mão com um Gmail real (cerca de 64.000 mensagens) e uma caixa Titan real
(cerca de 1.600), entre 2026-10-30 e 2026-11-01. O que essas verificações estabeleceram e ainda importa:

- **Titan** oferece CONDSTORE mas não ManageSieve, então as regras do Titan rodam no motor do Pandora. O
  SMTP dele salva exatamente uma cópia em Enviados.
- **Gmail** pode levar mais de um ciclo para mostrar uma mudança feita no webmail. A edição de um filtro
  pode levar minutos para chegar ao `filters.list`.
- **As telas do próprio Gmail mantêm o id do filtro ao editá-lo**, o que a documentação dele não diz. Por
  isso a comparação das regras olha o que um filtro conhecido faz, não só o id.
- Filtros que usam "nunca enviar para spam" (`removeLabelIds: SPAM`) não cabem no modelo de regras e
  aparecem somente leitura.
- Nunca mostrado numa conta real: resposta do Gmail entrando na conversa, encaminhar com anexo, Cco e
  clique duplo em Enviar.

### Decisões diferentes do primeiro desenho

- **Sem `Google.Apis.Gmail.v1`.** REST com `HttpClient`, como o `GoogleOAuthProvider` faz.
- **Sem o endpoint de batch do Gmail.** Um `messages.get` por mensagem, dois por vez.
- **Só `gmail.modify` é pedido de início.** Enviar não precisa de mais. Escrever filtros precisa de
  `gmail.settings.basic`, pedido só quando uma regra é salva.
- **Um envio não traz a cópia de Enviados na hora.** Ela aparece no ciclo seguinte, em até 2 minutos.
- **Um ciclo lê lotes da carga inicial até acabar um orçamento**, não um lote por ciclo: um lote a cada 2
  minutos levou mais de 3 horas para 50.000 mensagens.
- **`POST /mailboxes` não inicia a carga inicial.** O primeiro ciclo inicia.
- **Sem colunas por pasta em `com002`.** O estado do IMAP fica no `sync_cursor` opaco.
- **O cache de conteúdo não tem limite de tamanho**, só de idade. Ver [Modelo de dados](data-model.md#cache-de-conteúdo).
- **A lista de mensagens é uma lista simples** com "Carregar mais", não uma `Table` virtual.
- **Imagens `cid:` viram URLs `data:`**, não links para a API.

## Ainda não implementado

| Área | Status |
|---|---|
| **Push do Gmail, Radar consumindo o evento, mais provedores** | Depois; ver [product-plan.md](product-plan.md) |

## Pontos em aberto conhecidos

1. **O job é sequencial**, então a carga inicial de uma caixa grande atrasa todas as outras em até o orçamento de 5 minutos.
   Serve para um usuário. A primeira coisa a mudar com vários: uma fila separada para a carga inicial, com concorrência limitada.
2. **Uma instância só.** A trava fica no processo; várias instâncias precisariam de um lease no banco.
3. **Modo de teste no Google.** O refresh token do Gmail expira em 7 dias, então a caixa pede reconexão mais ou menos
   uma vez por semana. Passar de 100 usuários exigiria a verificação do Google para um escopo restrito.
4. **A busca** usa `ILIKE` sem índice e não lê os corpos. Serve no volume de uma pessoa.
5. **O download de anexo** busca a mensagem bruta inteira de novo.
6. **Uma movimentação que atravessa a varredura do IMAP**, de uma pasta ainda não lida para uma já lida, cai em dois ciclos, então não é juntada e pode disparar um evento falso.
7. **Depois de uma ação no Gmail durante a carga inicial**, um lote velho pode mostrar o estado antigo por até um ciclo.
8. **Só a página carregada pode ser selecionada**, e não há desfazer; a lixeira pode ser restaurada no provedor.
9. **As chaves de envio ficam na memória do processo,** então uma nova tentativa logo depois de reiniciar pode enviar duas vezes.
10. **Um servidor que salva a cópia de Enviados mais de 3 segundos depois** fica com duas cópias. O Titan não.
11. **O limite de 25 MB não está confirmado:** as páginas da API do Google não dizem nenhum. Um email acima do limite real volta como recusa.
12. **Sem nome do remetente, sem imagens inline, citação em texto puro.**
13. **Os erros das regras estão em inglês,** como as outras mensagens do servidor, e uma regra cuja etiqueta sumiu aparece "Mover para ?" até ser editada.
14. **As regras leem condições e ações de volta como o Gmail as entrega,** então uma regra salva como arquivar + adicionar etiqueta volta como mover. Significam o mesmo.
