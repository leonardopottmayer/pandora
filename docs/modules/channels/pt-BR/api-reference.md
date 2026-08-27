# Referência de API

[← Voltar ao índice](README.md) · Relacionados: [Entrada e Vínculo](inbound-and-linking.md)

Caminho base: **`/api/v{version}/channels`**. Todos os endpoints são autenticados e escopo do usuário
do token. O tráfego de entrada do Telegram **não** chega aqui — é ingerido pelo serviço de background de
long-polling, não por um endpoint HTTP. Erros vêm de falhas tipadas `Result`.

---

## Endpoints

| Método | Caminho | Propósito |
|---|---|---|
| GET | `/` | Os endereços do usuário (vinculados ou desabilitados), para a tela de configurações. |
| POST | `/{channel}/link` | Inicia o handshake de vínculo; devolve o deep link que o usuário toca. |
| DELETE | `/{channel}/link` | Esquece o endereço neste canal. |
| POST | `/{channel}/test` | Enfileira uma mensagem de teste ao próprio endereço do usuário neste canal. |
| GET | `/notifications` | Histórico de entrega, mais recentes primeiro — filtrável por status, canal, categoria, data; paginado. |
| GET | `/preferences` | As escolhas de canal do usuário por categoria. |
| PUT | `/preferences/{category}` | Define os canais em que uma categoria sai (lista vazia silencia; canais desconhecidos são rejeitados). |

### GET `/`

Devolve as linhas `chn001` do usuário: canal, endereço, flags verificado/habilitado, metadados — a
lista de configurações.

### POST `/{channel}/link`

Para o Telegram, devolve `{ deepLink, token }`: o link `https://t.me/<bot>?start=<token>` que o usuário
toca. O chat id chega depois, do Telegram, carregando o token que este emitiu.

### DELETE `/{channel}/link`

Remove o endereço `chn001` do canal.

### POST `/{channel}/test`

Enfileira uma notificação de teste ao próprio endereço verificado do usuário no canal — o teste "o
vínculo funcionou?".

### GET `/notifications?status=&channel=&category=&from=&to=&skip=&take=`

A leitura do histórico de entrega (`GetDeliveryHistory`), apoiada por `ix_chn006_user_created_at`.
Responde "meu lembrete realmente saiu?".

### GET `/preferences`

Devolve as preferências `chn005` do usuário por categoria.

### PUT `/preferences/{category}`

```json
{ "channels": ["telegram", "email"] }
```

Define a lista ordenada de canais de uma categoria. Uma lista vazia a silencia; canais desconhecidos são
rejeitados. Categorias `identity.*` são obrigatórias e não configuráveis.

---

## Contratos (eventos in-process)

Não são HTTP, mas a superfície pública do módulo no bus:

| Direção | Evento | Significado |
|---|---|---|
| in | `NotifyUserRequested` | Notificação com botões, de responsabilidade do chamador (agenda.*, assistant.*). |
| in | `SendNotificationRequested` | Envio endereçado ad-hoc. |
| in | Fatos do Identity (`PasswordResetRequested`, `AccountActivationRequested`, `MfaEnabled`, …) | Mapeados para templates pelos subscribers deste módulo. |
| out | `InboundInteractionReceived` | Um toque de botão inline, roteado ao módulo dono. |
| out | `InboundMessageReceived` | Uma mensagem de texto/voz, para o Assistant. |
| out | `UserChannelDisabled` | Um canal desabilitado após falha permanente. |
