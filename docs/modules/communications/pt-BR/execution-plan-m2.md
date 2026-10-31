# Communications — Plano de Execução, M2 (Organizar)

> **Status:** Em andamento: passos 1–2 construídos. É a fase **M2** do [product-plan](product-plan.md); o que existe hoje está no
> [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/execution-plan-m2.md)

Este documento é a lista de trabalho. O *porquê* do modelo está no [product-plan](product-plan.md); os números de seção (§) apontam
para lá.

**A M2 entrega:** etiquetas nos dois sentidos (criar, renomear, apagar) e ações em lote nas mensagens (lida, estrela, mover, arquivar,
lixeira, e adicionar ou remover etiquetas no Gmail). **É a primeira fase que altera caixas reais.** Sem envio (M3), sem regras (M4).

**Pronto quando** (§15): uma etiqueta criada no Pandora aparece no Gmail e no Titan, uma pasta renomeada no webmail do Titan aparece
renomeada no Pandora, e um mover feito de qualquer lado reflete no outro.

---

## O que já existe (não reconstruir)

| Peça | Onde | O que a M2 usa |
|---|---|---|
| Sincronização de etiquetas | `SyncMailboxCommandHandler.ApplyLabelsAsync` | Todo ciclo relê as etiquetas, então uma mudança feita no webmail chega sozinha |
| Estado que se corrige sozinho | `history.list` do Gmail, CONDSTORE + snapshots no IMAP | Uma mudança no provedor, inclusive a nossa, volta como upsert no próximo ciclo |
| Escopo | `gmail.modify`, já autorizado | Cobre etiquetas, modify e lixeira: **sem novo consentimento** |
| Adaptadores | `GmailMailboxProvider`, `ImapMailboxProvider` | Mais um grupo de métodos; o `RunAsync` e o mapeamento de erros ficam |
| Capacidades | `MailboxCapabilities.MultipleLabels` | Decide o que as telas oferecem |
| Telas | `MailSidebar`, `MessageList`, `MessageReader` | Ganham menu de etiquetas, checkboxes e barra de ações |

---

## Decisões fechadas

| Decisão | Valor | Motivo |
|---|---|---|
| Mover no webmail | **Juntar pelo Message-ID:** uma mensagem que sumiu e reapareceu na mesma aplicação atualiza a mesma linha | Mantém o id da linha e o corpo em cache, e elimina o evento falso ([achado ao planejar](#o-bug-que-abre-o-passo-1)). Rejeitado: manter "removida + nova", que perde o cache a cada mover |
| Renomear no webmail | **A mesma junção**, sem detectar renomeação. Os cabeçalhos da pasta são lidos de novo | Um caminho só. Detectar renomeação por UIDVALIDITY é uma heurística que depende do servidor; só vale se uma pasta grande fizer a releitura pesar |
| Renomear no Pandora | **O mesmo caminho:** `RENAME` no provedor e, em seguida, o passo de etiquetas + mudanças | O caminho da pasta está em cada `provider_message_id` e no cursor opaco, então uma atualização direta mexeria nos dois |
| Sincronização e ações | **Duas travas por caixa.** A trava de ciclo impede dois ciclos ao mesmo tempo. A trava de ação cobre uma ação, ou o passo de etiquetas + mudanças do ciclo. A carga inicial roda fora dela | Um clique nunca espera uma carga inicial, que pode segurar o ciclo por 5 minutos. Um lote antigo que sobrescrever uma ação é corrigido no próximo ciclo, porque a ação é uma mudança no provedor. Pior caso: a tela fica errada por um ciclo. Rejeitado: uma trava só (até 5 minutos de espera); orçamento menor (a carga pode levar o dobro) |
| Ordem de escrita | **Provedor primeiro**, linha local depois que ele aceita (§3, C2) | Uma falha não muda nada localmente e aparece na tela |
| Exclusão definitiva | **Não existe na M2.** "Excluir" é lixeira: sem método na porta, sem endpoint | Nada que a M2 faz pode perder email de vez |
| Apagar pasta (IMAP) | **Só vazia**, e nunca uma etiqueta do sistema. Senão `409 LabelNotEmpty` | Apagar uma pasta IMAP apaga o que está dentro |
| Tamanho do lote | Até **100 ids por requisição, uma caixa por requisição** | O cliente agrupa uma seleção de várias caixas; não precisa de formato para falha parcial |
| Mover e etiquetas entre caixas | Só oferecidos quando a seleção é de uma caixa | Etiquetas pertencem a uma caixa. Lida, estrela, arquivar e lixeira funcionam entre caixas |
| Abrir uma mensagem | **Marca como lida**, pelo cliente, com a mesma ação | É o que se espera de um cliente de email. A M1 não marcava porque só lia. Uma falha aqui é silenciosa e não bloqueia a leitura |
| Lixeira no IMAP | Move para a pasta com papel `trash`; sem ela → `422 NoTrashFolder`. O mesmo para arquivar | O Pandora nunca inventa uma pasta |
| Testes reais | As contas reais, **só com material descartável**: uma etiqueta e uma pasta `pandora-test`, mensagens enviadas para você mesmo | O ambiente real, sem nada novo para configurar |

---

## O bug que abre o passo 1

Achado lendo o código ao planejar, depois **reproduzido por um teste e corrigido no passo 1**. No `ApplyMessagesAsync`, as remoções do snapshot rodam antes do
`ExistsWithRfc822IdAsync`. Então uma mensagem movida no webmail é apagada da pasta antiga, depois parece nova, e publica um
`MailMessageReceived` falso. O teste atual, `A_message_id_the_mailbox_already_has_is_not_an_arrival`, só cobre cópia, em que a original
continua lá. Nada consome o evento hoje, então ainda não houve dano.

O `ApplyLabelsAsync` tem um problema parecido: apaga as mensagens de uma pasta que sumiu **antes** de ler as mudanças, o que não deixa
nada para juntar numa renomeação.

---

## Passos

Cada passo termina verde (build + os testes dele) antes do próximo. Pontos de commit sugeridos: **M2a** = passo 1, **M2b** = passos 2–4,
**M2c** = passos 5–6. Commit só quando pedido.

### Passo 1 — Motor de sincronização: junção e travas ✅

> **Construído (2026-10-30).** `MailboxGates` (singleton), `MailboxChangesStep` (etiquetas + mudanças, sob a trava de ação; ele mesmo
> pega a trava, então o passo 4 o chama fora da sua própria posse), `MailMessage.Rebind` e `MailboxWrites.UpdateMailboxAsync`, compartilhado
> pelo passo e pelo handler. O motor antigo falhou nos testes novos, inclusive com um `MailMessageReceived` falso num mover. Testes:
> `SyncEngineTests` +6 (mover, renomear, pasta que sumiu de vez, duas cópias movidas juntas, sem Message-ID, as duas travas).

Sem funcionalidade nova, e não altera nenhuma caixa.

- **Primeiro, um teste que falha:** um mover de verdade pelo `FakeMailProvider` (`SnapshotLabel` + o mesmo Message-ID com id e etiqueta
  novos) → nenhum `MailMessageReceived`, o mesmo id de linha, o corpo em cache ainda lá.
- `MailMessage.Rebind(providerMessageId)`.
- `ApplyMessagesAsync` numa nova ordem:
  1. Descobrir o que está sumindo (`removedIds` + as linhas ausentes de cada snapshot que ficaram sem etiqueta).
  2. Para cada upsert com id de provedor desconhecido e com Message-ID, procurar uma linha sumindo com o mesmo Message-ID, uma para uma:
     `Rebind` + `ApplyRemote`, e tirar da lista de exclusão.
  3. Apagar o resto em bloco.
  4. Upsert como hoje; a checagem de chegada continua.
- O `ApplyLabelsAsync` vira dois: adicionar e atualizar antes das mudanças; **remover as etiquetas que sumiram depois** de aplicá-las. O
  adaptador já manda um snapshot vazio para uma pasta fora do escopo, e a etiqueta precisa ainda existir para ele contar. Sem cursor
  ainda (nada para ler), remove na hora, como hoje.
- `MailboxGates` substitui o dicionário estático do handler: `Cycle(id)` e `Action(id)`. O ciclo pega `Cycle` por toda a duração e
  `Action` só em etiquetas + mudanças.
- Extrair o passo de etiquetas + mudanças para algo que um comando possa chamar (o passo 4 o roda depois de uma operação de etiqueta).
- **Testes:** o mover acima; uma pasta renomeada (caminho antigo sumiu, caminho novo com os mesmos Message-IDs) mantém linhas e corpos e
  não dispara evento; uma cópia continua como duas linhas; uma mensagem sem Message-ID continua como exclusão + criação; uma ação não fica
  bloqueada enquanto uma carga inicial lenta falsa segura o `Cycle`.
- `ponytail:` um mover entre pastas que atravessa a varredura (de uma pasta ainda não lida para uma já lida) ocupa dois ciclos, então não é
  juntado e ainda pode disparar um evento falso. Uma janela de segundos, só na varredura IMAP.

### Passo 2 — Porta e escritas IMAP ✅

> **Construído (2026-10-30).** Mudanças em relação à lista abaixo:
> - **`TrashAsync` devolve os ids** como o `MoveAsync`, porque no IMAP é um mover.
> - **Renomear move a assinatura à mão**, subpastas incluídas: a assinatura pertence ao nome, então a pasta sairia do escopo.
> - **Uma pasta com subpastas** também é `LabelNotEmpty`, e a checagem usa `STATUS`.
> - **O adaptador do Gmail responde `ActionNotSupported`** a toda escrita até o passo 3. Nada chama as escritas ainda.
> - **Não coberto pelo GreenMail, portanto para o passo 6:** ele não tem SPECIAL-USE, então nenhuma pasta lá tem o papel `trash` (só a recusa
>   é testada); e ele derruba a conexão ao apagar uma pasta que já teve mensagem, então o teste apaga uma nunca usada. O comportamento do
>   MailKit sem `MOVE` também não foi exercitado: o GreenMail anuncia `MOVE` e `UIDPLUS`.
> - Testes: `ImapMailboxProviderTests` +4.

- O `IMailboxProvider` ganha:
  - `CreateLabelAsync(mailbox, name)` → `RemoteLabel`
  - `RenameLabelAsync(mailbox, providerLabelId, newName)`
  - `DeleteLabelAsync(mailbox, providerLabelId)`
  - `SetFlagsAsync(mailbox, ids, isUnread?, isStarred?)`
  - `ChangeLabelsAsync(mailbox, ids, add, remove)`: só onde há `MultipleLabels`
  - `MoveAsync(mailbox, ids, targetProviderLabelId)` → id antigo → id novo, ou nulo quando o provedor não informa
  - `TrashAsync(mailbox, ids)`
- `ImapMailboxProvider`:
  - Agrupar os ids por pasta; abrir em `ReadWrite`. Um UIDVALIDITY diferente do id → `SyncCursorExpired`, sem escrever.
  - Flags: `AddFlags`/`RemoveFlags` para `\Seen` e `\Flagged`.
  - Mover: `MoveToAsync`, lendo o UID novo. Sem `UIDPLUS` o id é nulo, e o próximo ciclo junta a linha. Verificar o que o MailKit faz num
    servidor sem `MOVE`, e registrar se o Titan tem `UIDPLUS` e `MOVE`.
  - Criar: **também assinar.** O `FoldersInScopeAsync` só lista pastas assinadas, então uma pasta nova não assinada nunca apareceria.
  - Renomear: `RenameAsync`. Apagar: recusar pasta com mensagens (`LabelNotEmpty`), checando no servidor, não só localmente.
  - Lixeira: resolver o papel `trash` no adaptador, e mover.
- **Testes (GreenMail):** lida e estrela vistas de outro cliente; um mover sai da origem e chega ao destino; uma pasta criada aparece na lista;
  uma renomeação; apagar uma pasta com mensagens é recusado e as mensagens continuam lá; a `INBOX` não pode ser apagada.

### Passo 3 — Escritas no Gmail

- `labels.create`, `labels.patch`, `labels.delete` (só etiquetas do usuário).
- `messages.batchModify` para flags (`UNREAD`, `STARRED`) e etiquetas. Mover = adicionar o destino, remover `INBOX` ou a exclusiva atual (§4.3);
  o id não muda.
- `messages.trash`, um por mensagem, `ParallelGets` por vez. `messages.delete` e `batchDelete` **nunca são chamados**.
- Confirmar os limites na documentação do Google antes de contar com eles; os números de cota que temos são de memória.
- A nova tentativa do `GetAsync` vira um caminho de envio único, compartilhado com `POST`/`PATCH`/`DELETE`. Essas escritas são idempotentes, então
  repetir depois de um limite de taxa é seguro.
- **Testes:** com respostas prontas, a URL e o corpo de cada chamada; um `403` de escopos → `MissingScopes`; uma escrita limitada é repetida.

### Passo 4 — Application e API

- Comandos sob a trava de ação: `CreateLabel`, `RenameLabel`, `DeleteLabel`, `ApplyMessageAction`.
- **Operações de etiqueta** chamam o provedor e depois rodam o passo de etiquetas + mudanças, para as renomeações seguirem o caminho único.
- **Ações em mensagens:** provedor primeiro, depois a linha local (flags, etiquetas, `Rebind` quando o mover devolveu id novo). Um id nulo deixa a
  linha para o próximo ciclo.
- `MailErrors`: `LabelNotEmpty` (409), `SystemLabel` (422), `LabelNameInvalid` (422), `NoTrashFolder` e `NoArchiveFolder` (422),
  `ActionNotSupported` (422, etiquetas no IMAP), `TooManyMessages` (422).
- Uma etiqueta do sistema não pode ser renomeada, apagada nem usada como destino onde o provedor proíbe (`SENT`, `DRAFT`, `CATEGORY_*`).
- API, `/api/v1/communications`:
  - `POST mailboxes/{id}/labels` `{ name }`, `PATCH labels/{id}` `{ name }`, `DELETE labels/{id}`
  - `POST messages/actions`:
    ```json
    { "mailboxId": "…", "messageIds": ["…"], "action": "move", "labelId": "…" }
    ```
    `action`: `mark-read`, `mark-unread`, `star`, `unstar`, `move`, `archive`, `trash`, `add-label`, `remove-label`. Devolve os resumos atualizados.
- **Testes** (provedor falso): ids de outro usuário → 404; uma mensagem de outra caixa na lista → 404 e nenhuma chamada ao provedor; falha no
  provedor → o erro e **nenhuma mudança local**; o resultado local de cada ação; mais de 100 ids → 422; uma etiqueta do sistema recusada; um id nulo
  de um mover deixa a linha até o próximo ciclo, que a junta.

### Passo 5 — Web

- **`MessageList`:** uma checkbox por linha, "selecionar a página carregada".
- **Barra de ações** com seleção: lida, não lida, estrela, arquivar, lixeira, mover para, etiqueta (só Gmail). Lê `mailbox.provider` para saber o que
  oferecer; mover e etiquetas somem quando a seleção abrange várias caixas. O cliente divide a seleção em uma requisição por caixa.
- **`MailSidebar`:** um menu nas etiquetas do usuário (renomear, apagar) e "nova etiqueta" por caixa, num modal pequeno. Uma pasta com mensagens mostra a
  mensagem do servidor.
- **`MessageReader`:** as mesmas ações para a mensagem aberta. Abrir uma mensagem não lida envia `mark-read`, sem bloquear o leitor.
- Invalidar mensagens e etiquetas depois de uma ação, sem atualização otimista: a linha muda quando o provedor aceitou. Uma falha é um `message.error`, e
  a seleção continua.
- i18n `mail.*` em `en.json` e `pt-BR.json`.
- **Testes:** uma seleção em duas caixas faz duas requisições e esconde "mover"; uma ação que falha deixa a linha como estava; abrir uma mensagem não lida
  envia `mark-read` uma vez.

### Passo 6 — Verificação real e docs

Só com material descartável: uma etiqueta e uma pasta `pandora-test`, e mensagens enviadas para você mesmo.

- **Gmail:** criar `pandora-test` no Pandora → aparece no Gmail. Etiquetar, mover e dar estrela → aparece no Gmail. Lixeira → está na lixeira do Gmail, e pode
  ser restaurada. Uma mudança no Gmail → aparece no Pandora.
- **Titan:** o mesmo, com uma pasta. Mover no webmail → mesma linha, corpo em cache mantido, nenhum `MailMessageReceived` no outbox. Renomear no webmail →
  renomeada no Pandora, corpos mantidos. Apagar uma pasta com mensagem → recusado. Esvaziar essa pasta e apagar → ela some.
  A lixeira encontra a pasta com o papel `trash`.
- **Tempo:** uma ação durante um ciclo do Gmail ou do Titan não espera por ele.
- **Titan:** registrar `UIDPLUS` e `MOVE`.
- **Docs:** `overview`, `architecture` (travas, junção), `data-model`, `api-reference`, `implementation-status`, en + pt-BR; o product plan guarda só M3–M4, e a
  C8 e a §13 descrevem as duas travas; uma nota **Construído** em cada passo aqui.

---

## Limites conhecidos

- **Renomear uma pasta relê os cabeçalhos dela.** Rápido numa pasta pequena; uma muito grande precisaria de detecção de renomeação.
- **Um mover que atravessa a varredura IMAP** ocupa dois ciclos (veja o passo 1).
- **Só a página carregada pode ser selecionada.** "Tudo nesta etiqueta" fica para depois.
- **Sem desfazer.** A lixeira pode ser restaurada no provedor.
- **Depois de uma ação no Gmail a tela fica certa na hora**, mas um lote antigo da carga inicial pode trazer o estado anterior de volta por até um ciclo.
