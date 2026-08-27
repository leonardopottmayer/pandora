# Saída e Templates

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. Dois caminhos de entrada

A regra de qual ponto de entrada um produtor usa é simples: **quem é dono dos botões é dono do
`NotifyUserRequested`.**

- **Sem botões (`identity.*`).** O produtor publica um *fato* (`PasswordResetRequested`,
  `AccountActivationRequested`, `MfaEnabled`, …) e um **subscriber neste módulo** o mapeia para uma
  `TemplateKey` + payload e categoria. É assim que o Identity funciona; continua assim. Subscribers:
  `AccountActivationRequestedHandler`, `AccountActivatedHandler`, `PasswordResetRequestedHandler`,
  `PasswordChangedHandler`, `MfaEnabledHandler`, `MfaDisabledHandler`.
- **Com botões (`agenda.*`, `assistant.*`).** Quem chama publica **`NotifyUserRequested`** diretamente,
  porque a ação e o payload de cada botão são seu domínio. Tratado por `NotifyUserRequestedHandler`.
- **Ad-hoc.** `SendNotificationRequested` é a saída de emergência — um envio endereçado simples, não
  atribuído a um usuário. Tratado por `SendNotificationRequestedHandler`.

## 2. A cadeia de renderização

A renderização acontece **no enfileiramento, não no envio** (princípio C6), então o que saiu fica
guardado e o retry reenvia bytes idênticos.

| Participante | Sabe | Não sabe |
|---|---|---|
| **Produtor** (Identity, Agenda, …) | O fato + (para botões) as ações e payloads. | Que e-mail, templates ou canais existem. |
| **Subscriber** (aqui) | fato → `TemplateKey` + payload plano + categoria; monta valores derivados (ex. a URL de reset). | Como o texto é escrito. |
| **Renderer** (aqui) | O arquivo, por `(chave, canal, locale)`. Substitui placeholders, nada mais. | De onde o payload veio. |
| **Fila** (aqui) | O conteúdo já renderizado. | Todo o resto. |

`NotificationEnqueuer` renderiza por canal resolvido e persiste uma `Notification` por canal.

## 3. Fan-out

`NotifyUserRequested` nomeia um usuário e uma categoria. O enqueuer:

1. Lê a **preferência** do usuário para a categoria (`chn005`) — a lista ordenada de canais; vazia ⇒
   silenciada (exceto `identity.*`, obrigatória, que pula isto).
2. Interseca com os canais **utilizáveis** do usuário (`chn001`, verificados **e** habilitados).
3. Renderiza e enfileira **uma linha por canal sobrevivente**, todas com um `group_id`.
4. Dedup é por canal via `uq_chn006_correlation_channel (correlation_id, channel)` — a mesma requisição
   chegando a e-mail e Telegram são duas linhas, não uma engolida como duplicata.

Cada linha faz retry e falha de forma independente, então o status do grupo é honesto por canal.

## 4. Templates

Templates são **arquivos no repositório** (recursos embutidos), não linhas de banco — conteúdo que
passa por code review e acompanha a versão do código que os preenche. O layout é uma árvore por
`chave / canal.locale`:

```
Templates/
├── password-reset/
│   ├── email.pt-BR.txt          (linha 1 = assunto, resto = corpo)
│   └── email.en.txt
└── agenda.reminder.due/
    ├── email.pt-BR.txt
    ├── telegram.pt-BR.txt
    └── telegram.en.txt
```

`FileNotificationTemplateRenderer` seleciona o arquivo e substitui `{{placeholder}}` — sem lógica.
`TemplateCatalog` + `TemplateCatalogValidator` enumeram chaves conhecidas × canais permitidos × locales
e **falham o startup** se uma variante estiver faltando, então um template do Telegram ausente é um erro
de boot, não uma surpresa em runtime.

## 5. Botões

Rótulo é conteúdo; ação é domínio.

- Quem **chama** declara as ações e o payload de cada uma.
- O **template** do canal carrega os rótulos por ação (rótulos têm locale):
  `buttons.task_done = ✓ Concluído`.
- O **merge** acontece no render. No enfileiramento, cada ação vira uma linha `Interaction` (`chn003`) e
  o botão renderizado do Telegram carrega o **id da interação** (não a ação) como `callback_data`.
  Canais que não suportam botões descartam a lista sem erro.

## 6. O dispatcher e o retry

`NotificationDispatcherBackgroundService` varre linhas devidas (`status = Pending`,
`next_attempt_at ≤ agora` via `ix_chn006_status_next_attempt_at`), envia pelo `IChannelTransport` do
canal (`EmailChannelTransport` / `TelegramChannelTransport`) e avança o agregado:

- **Sucesso** → `Sent`, registra `provider_message_id` (habilita respostas em thread).
- **Falha transitória** → volta a `Pending` com backoff exponencial (`attempt_count`,
  `next_attempt_at`), até `max_attempts` → `Dead`.
- **Falha permanente de transporte num canal** (ex. o usuário bloqueou o bot) → desabilita o
  `UserChannel` (`disabled_reason`) e publica `UserChannelDisabled`.
