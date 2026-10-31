# Communications — Plano de Execução, M3 (Enviar)

> **Status:** Em andamento: decisões aprovadas, passos 1–6 construídos; falta a verificação real. É a fase **M3** do [product-plan](product-plan.md); o que existe hoje está no
> [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/execution-plan-m3.md)

Este documento é a lista de trabalho. Os números de seção (§) apontam para o [product-plan](product-plan.md).

**A M3 entrega:** escrever, responder, responder a todos e encaminhar, com anexos, de qualquer caixa conectada. Sem rascunhos no servidor, sem desfazer, sem
regras (M4).

**Pronto quando** (§15): uma resposta enviada pelo Pandora cai na mesma conversa do Gmail, e uma mensagem enviada pelo Titan aparece exatamente uma vez na pasta
Enviados.

**Enviar não tem volta,** ao contrário de tudo o que a M2 faz. Uma nova tentativa errada ou um clique duplo chega à caixa de outra pessoa. Isso molda as decisões abaixo.

---

## O que já existe (não reconstruir)

| Peça | Onde | O que a M3 usa |
|---|---|---|
| Escopo | `gmail.modify`, já autorizado | A página do Google para `messages.send` o aceita: **sem novo consentimento**, ao contrário do que os planos anteriores diziam |
| Servidores SMTP e senha | `com001`, `GetPasswordAsync` do Integrations | A mesma conta do IMAP |
| MIME | MimeKit, no Application (`MimeReader`) | Monta a mensagem de saída e lê a original numa resposta |
| Mensagem crua | `GetRawMessageAsync` | Responder e encaminhar leem `Reply-To`, `References` e anexos da original, que o espelho não guarda |
| Passo de etiquetas + mudanças | `MailboxChangesStep` | Traz a cópia enviada para o espelho logo depois do envio |
| Markdown | `marked` + `dompurify`, no `client-web` | Transforma o texto escrito na parte HTML |
| Upload de arquivo | `IFormFile`, como em Notes e Finances | Os anexos |

---

## Decisões fechadas

Aprovadas em 2026-10-31.

| Decisão | Valor | Motivo |
|---|---|---|
| Repetir um envio | **Só quando o provedor certamente recusou:** `429` e `403` de limite de taxa. Nunca em `5xx`, timeout ou conexão perdida | Esses são ambíguos: o email pode ter saído. O erro diz isso: "pode ter sido enviado, confira em Enviados" |
| Envio duplicado | **Uma chave de envio:** o formulário gera um Message-ID ao abrir, e o servidor recusa uma chave que já tratou (`409 AlreadySent`) | Cobre clique duplo, nova tentativa depois de timeout e duas abas. Rejeitado: só desabilitar o botão, que não cobre os dois últimos. `ponytail:` chaves na memória do processo por uma hora; reiniciar as esquece |
| Cópia em Enviados no IMAP | **Detectar:** depois que o SMTP aceita, procurar o Message-ID na pasta `sent`, duas vezes com uma pausa; `APPEND` só se não estiver lá | Funciona em qualquer servidor sem configuração, e responde sozinho à questão em aberto do Titan. Rejeitado: uma opção por provedor, que alguém precisa saber e manter certa. O risco: um servidor que salva depois da espera gera duplicata, e a verificação real no Titan procura isso |
| Cópia que falha | O envio **deu certo**: `200` com `copySaved: false`, e a tela avisa | O email já saiu. Um erro convidaria a enviar de novo |
| Editor | **Uma área de texto, escrita em Markdown**, com prévia. A mensagem vai como HTML + texto puro | Sem dependência nova, e texto puro simplesmente funciona. Rejeitado: o `MarkdownEditor` do Notes, preso a páginas, tags e anexos do Notes; um editor rico, dependência nova |
| Texto citado | **Texto puro**, `> ` por linha na resposta, um bloco de cabeçalho no encaminhamento | O HTML da original veio de outra pessoa, e reenviá-lo exige limpeza própria e as imagens `cid:`. `ponytail:` manter a formatação se o texto puro ficar pobre demais |
| Anexos ao encaminhar | Os arquivos da original vão junto, e cada um pode ser removido | É o que se espera de um encaminhamento. Lidos da mensagem crua |
| Limites | **25 MB** por mensagem com anexos, **100 destinatários** | O limite do Gmail como está no plano (§9, a confirmar no passo 4). Nosso teto fica abaixo dos 500 do Gmail |
| Para quem vai a resposta | O servidor calcula a partir da mensagem crua: `Reply-To`, senão `From`; responder a todos soma `To` e `Cc`, sem o endereço da própria caixa | O espelho não tem `Reply-To`, e errar isso manda email para a pessoa errada |
| Conversa | `In-Reply-To` e `References` da original; no Gmail também o `threadId`, e o assunto precisa bater com o da original (o prefixo `Re: ` não atrapalha) | As três condições do Google para entrar numa conversa |
| Rascunhos | No navegador, por caixa e por mensagem respondida; apagados ao enviar | §9. Nada vai para o provedor |
| Confirmação | Nenhuma para enviar, como em qualquer cliente de email. Um aviso para assunto vazio ou corpo sem texto | Confirmar todo envio ensina a clicar sem ler |
| Testes reais | **Só para os seus próprios endereços**, entre o seu Gmail e o seu Titan | Nada aqui chega a mais ninguém |

---

## Achados ao planejar

- **Email enviado publicava `MailMessageReceived`.** Uma mensagem nova em Enviados ou Rascunhos passava pela mesma checagem de chegada, e só uma cópia com o mesmo Message-ID na caixa a impedia. Reproduzido por um
  teste e corrigido no passo 1. Nada consumia o evento, mas a M3 tornaria isso rotina.
- **Cotas podem custar dinheiro.** A página do Google (atualizada em 2026-10-16) prevê cobrança "later in 2026", com 90 dias de aviso, ao passar de **80.000.000 de unidades por projeto por dia**.
  `messages.send` custa 100. Uma pessoa está longe disso: a carga de 64 mil mensagens custa cerca de 1,3 milhão. Importa para escala: esse teto equivale a umas 60 cargas assim por dia.

---

## Passos

Cada passo termina verde antes do próximo. Pontos de commit sugeridos: **M3a** = passo 1, **M3b** = passos 2–5, **M3c** = passos 6–7. Commit só quando pedido.

### Passo 1 — Email enviado não é chegada ✅

> **Construído (2026-10-31).** Como listado; o motor antigo publicou 2 eventos no teste novo. Um limite, marcado com `ponytail:` no código: email enviado para você mesmo não conta como chegada se a cópia de Enviados vier um
> ciclo antes da entrada, porque o Message-ID já está lá. Teste: `Mail_in_sent_or_drafts_is_not_an_arrival`.

- **Primeiro, um teste que falha:** uma mensagem nova numa etiqueta com papel `sent` ou `drafts` → nenhum `MailMessageReceived`.
- `MailboxChangesStep`: um upsert cujas etiquetas são só `sent` ou `drafts` não é chegada.

### Passo 2 — A porta e a mensagem ✅

> **Construído (2026-10-31).** `SendAsync` na porta, com os dois adaptadores respondendo `ActionNotSupported` até os passos 3 e 4; `MailErrors`; `MessageComposer`, com `Build` e `DraftFrom`. Além da lista abaixo:
> o rascunho traz o texto da original como está, e a tela o cita, no idioma do usuário. Responder à própria mensagem continua para as mesmas pessoas. Os arquivos de um encaminhamento deixam de fora imagens inline. Testes:
> `MessageComposerTests`.

- `IMailboxProvider.SendAsync(mailbox, rawMessage, threadKey?)` → `SentMessage(CopySaved)`. Bytes crus, para o MimeKit ficar fora do Domain; cada adaptador os carrega ou repassa.
- `MailErrors`: `SendUncertain` (pode ter saído), `AlreadySent` (409), `RecipientsInvalid`, `MessageTooLarge`.
- `Application/Mime/MessageComposer`: monta a `MimeMessage` a partir da entrada (de, para, cc, cco, assunto, texto, HTML, anexos, Message-ID, `In-Reply-To`, `References`); e, a partir de uma original crua,
  calcula destinatários, assunto, texto citado e anexos para responder, responder a todos e encaminhar.
- **Testes (unitários):** `Reply-To` vence `From`; responder a todos tira o endereço da própria caixa, em qualquer caixa alta ou baixa; `Re: ` não duplica; `References` cresce com o Message-ID da original; um cabeçalho com quebra de linha
  não injeta outro cabeçalho; o encaminhamento leva os arquivos.

### Passo 3 — IMAP: SMTP e a cópia em Enviados ✅

> **Construído (2026-10-31).** Mudanças em relação à lista abaixo:
> - **A pasta de Enviados** é a que tem o papel, senão uma chamada `Sent`, `Sent Items`, `Sent Messages` ou `Enviados`, para servidores sem SPECIAL-USE. Isso também permitiu testar a cópia no GreenMail, sem ponto de teste.
> - **Uma recusa do servidor é um erro comum; qualquer outra coisa depois de conectar é `SendUncertain`,** inclusive conexão perdida antes dos dados. Chamar isso de incerto custa uma olhada em Enviados; o contrário arrisca uma duplicata.
> - **A espera** é de 3 segundos, uma vez.
> - Testes: `ImapMailboxProviderTests` +4. **Não exercitado:** `SendUncertain`, porque o GreenMail não derruba a conexão no meio do envio, e senha SMTP errada, que ainda não tem teste; a verificação real cobre a senha.

- `ImapMailboxProvider.SendAsync`: `SmtpClient` do MailKit, `SecureSocketOptions.Auto`, a mesma credencial. Cco vai no envelope, não na mensagem, e fica na cópia em Enviados.
- Antes de o servidor aceitar, qualquer falha é um erro comum. Depois de enviar os dados, com a resposta perdida → `SendUncertain`. Destinatários recusados são um erro comum.
- Cópia como decidido: busca por `HEADER Message-ID` na pasta com papel `sent`, duas vezes com uma pausa, e então `APPEND` com `\Seen`. Sem pasta `sent` → `CopySaved: false`.
- **Testes (GreenMail):** recebida pelo destinatário; uma cópia numa pasta configurada como Enviados (o GreenMail não tem SPECIAL-USE, então achar a pasta precisa de um ponto de teste, ou fica para a verificação real); senha errada →
  `MailboxAuthFailed`. Uma cópia que já está lá não é adicionada de novo.

### Passo 4 — Envio no Gmail ✅

> **Construído (2026-10-31).** O upload multipart, confirmado no guia de upload do Google: `multipart/related`, primeiro os metadados, depois `message/rfc822`. O `PostMessageAsync` fica separado das outras escritas, para não herdar a nova tentativa
> delas. **As páginas do Google não informam limite de tamanho,** então os 25 MB não estão confirmados; a verificação real não chega lá, e um email acima do limite real volta como recusa, o que é seguro. Testes: `GmailMailboxProviderTests` +9, com respostas prontas.

- `messages.send` com upload multipart, levando `threadId` como metadado e a mensagem crua como `message/rfc822`. **Confirmar no guia de upload do Google** antes de contar com isso, junto com o limite de tamanho,
  que a página do método não informa.
- O `SendAsync` do adaptador ganha um parâmetro para o que pode ser repetido: aqui só `429` e `403` de limite de taxa. Todo o resto ambíguo → `SendUncertain`. O Gmail guarda a própria cópia: `CopySaved: true`.
- **Testes (respostas prontas):** a URL, as partes e o `threadId`; um `500` faz **exatamente uma** requisição e é `SendUncertain`; um `429` é repetido; um timeout é `SendUncertain`.

### Passo 5 — Application e API ✅

> **Construído (2026-10-31).** `SendMessageCommand`, `SendKeys`, `GetComposeDraftQuery`, e `POST messages/send` e `GET messages/{id}/compose`. Além da lista abaixo:
> - **Sem nome de remetente.** O nome de exibição da caixa é o rótulo do usuário ("Trabalho"), não quem escreve, então o From é só o endereço. Nome por caixa fica para o "depois" da §15.
> - **Destinatários são só endereços** no formulário, um campo por endereço; um nome no rascunho não é enviado.
> - **Só uma recusa certa libera a chave,** para o mesmo formulário tentar de novo. `SendUncertain` e uma exceção a mantêm.
> - **Tudo que pode ser recusado é checado antes:** a chave é pega logo antes de enviar, então um endereço inválido não a gasta.
> - **Depois do envio, nada parece falha de envio:** o passo de etiquetas + mudanças só vai para o log se falhar ou lançar exceção.
> - **O limite da requisição é de 40 MB**, acima dos 25 MB de anexos, para passar do limite responder 422 com mensagem.
> - Testes: `SendMessageApiTests` (13).

- `SendMessageCommand`: confere se a caixa é de quem chama, os endereços, os limites e a chave de envio; monta; envia **sem a trava de ação**, porque um SMTP lento não pode segurar as ações; depois roda o passo de etiquetas +
  mudanças, cuja falha só vai para o log.
- `GetComposeDraftQuery`: para responder, responder a todos ou encaminhar, o formulário preenchido.
- API:
  - `POST messages/send`, multipart: os campos, `sendKey`, `replyToId?` ou `forwardOfId?`, `files`. Responde `{ copySaved }`. `[RequestSizeLimit]` no limite.
  - `GET messages/{id}/compose?mode=reply|reply-all|forward`.
  - `GET messages/{id}/attachments/{attachmentId}` já serve o que o encaminhamento mostra.
- **Testes (provedor falso):** caixa ou original de outro usuário → 404 e nenhum envio; a mesma chave duas vezes → `409` e **um** envio; envios simultâneos com uma chave → um envio; endereço inválido → 422 e nenhum envio; grande demais → 422; uma
  cópia que falhou é `200`; `SendUncertain` guarda a chave, então uma nova tentativa é recusada.

### Passo 6 — Web ✅

> **Construído (2026-10-31).** `ComposeDrawer`, `lib/composeBody`. Além da lista abaixo:
> - **Depois de `SendUncertain`, "Enviar mesmo assim" é deliberado:** gera uma chave nova, depois de o usuário olhar em Enviados.
> - **Imagens saem da parte HTML,** para um rastreador numa citação não disparar na prévia. Estilos, formulários e iframes também.
> - **Um teste achou um clique duplo enviando duas vezes:** o formulário se liberava depois de um sucesso. Agora só uma falha o libera.
> - **A chave de envio não usa `crypto.randomUUID`,** que só existe em contexto seguro, e um homelab pode estar em http puro.
> - Testes: `ComposeDrawer.test.tsx`, `composeBody.test.ts`. Os botões da `MailPage` e do `MessageReader` que abrem o formulário não têm teste próprio, e o layout não foi visto no navegador; a verificação real cobre os dois.

- **`ComposeDrawer`:** de (qual caixa), para, cc, cco, assunto, Markdown com prévia, anexos; uma chave de envio nova a cada abertura. `marked` + `DOMPurify` para a parte HTML.
- "Nova mensagem" na `MailPage`; "Responder", "Responder a todos" e "Encaminhar" no `MessageReader`.
- **Falhas:** o formulário fica com o conteúdo. `SendUncertain` mostra uma mensagem própria e não oferece enviar de novo como se nada tivesse acontecido. `copySaved: false` é um aviso.
- **Rascunho:** `localStorage`, em `try/catch`, apagado ao enviar.
- i18n `mail.compose.*` em `en.json` e `pt-BR.json`.
- **Testes:** clique duplo envia uma vez; uma falha mantém o conteúdo; `SendUncertain` aparece como tal; a parte HTML não tem script; o rascunho volta depois de fechar e some depois de enviar.

### Passo 7 — Verificação real e docs

**Só para os seus próprios endereços.**

- **Do Gmail para o Titan e do Titan para o Gmail:** recebida uma vez, com texto, HTML e um anexo; aparece em Enviados.
- **Titan:** **exatamente uma** cópia em Enviados. Olhar no webmail, e de novo depois de alguns minutos, por causa de um servidor que salva tarde.
- **Responder no Gmail:** entra na mesma conversa, nos dois lados. **Responder no Titan:** o lado do Gmail agrupa.
- **Encaminhar com anexo**, e removendo um.
- **Responder a uma mensagem com `Reply-To`** vai para esse endereço.
- **Cco** não aparece para quem recebe.
- **Clique duplo** em enviar: uma mensagem.
- **Docs:** `overview`, `architecture`, `api-reference`, `implementation-status`, en + pt-BR; o product plan guarda só a M4; uma nota **Construído** em cada passo aqui.

---

## Limites conhecidos

- **Reiniciar esquece as chaves de envio,** então uma nova tentativa logo depois pode enviar duas vezes.
- **A checagem da cópia pode errar** com um servidor que salva depois da espera.
- **O texto citado perde a formatação** da original.
- **Imagens inline** na mensagem escrita não são suportadas; vão como anexo.
- **Sem desfazer envio, sem envio agendado, sem assinatura por caixa** (§15, depois).
