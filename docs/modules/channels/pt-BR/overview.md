# Visão geral — Limite e Princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

**Channels** é dono de toda a conversa com o usuário, nas duas direções:

- **Saída.** Uma fila de notificações durável (`Pending → Sending → Sent`, backoff exponencial,
  `MaxAttempts`, dead-letter `Dead`) sobre **e-mail** e **Telegram**, com renderização por canal a
  partir de uma chave de template.
- **Endereçamento.** Sabe *para quem* é uma notificação — um usuário, não só um endereço — e resolve os
  canais utilizáveis desse usuário por conta própria.
- **Política de entrega.** Preferências por usuário e por categoria decidem em quais canais um tipo de
  notificação sai. Uma requisição faz fan-out em N linhas independentes.
- **Entrada.** Recebe updates do Telegram: vínculo de conta, callbacks de botão inline, mensagens de
  texto e notas de voz, registrados de forma idempotente e **roteados ao módulo dono**.

Foi renomeado de `Notifications` quando metade do que faz deixou de ser notificação — ele hospeda o
ingress, trata `/start`, guarda chat ids e recebe áudio.

## 2. O limite

> **Channels:** o Pandora fala *com* o usuário. **Integrations:** o Pandora chama um terceiro *como* o usuário.

Um chat id do Telegram é um **endereço** onde o Pandora alcança o usuário, e um bot token é uma
credencial de **deployment** — ambos pertencem aqui, não em
[Integrations](../../integrations/pt-BR/overview.md). O que **não** pertence aqui é o *processamento*
do que o usuário mandou — transcrever, interpretar, executar um comando — isso vive em
[Assistant](../../assistant/pt-BR/product-plan.md). Channels roteia o evento de entrada bruto;
Assistant dá sentido a ele.

### Um módulo, não dois

Separar "transporte" de "política de entrega" foi avaliado e **rejeitado**: fan-out é um join entre
preferências (`chn005`) e endereços verificados/habilitados (`chn001`) que precisa caber numa
transação; a porta de transporte tem um único chamador (o dispatcher); e botões de interação nascem de
notificações (`chn003` tem FK para a linha da fila). A costura é real, mas interna — vive em namespaces
(`Delivery` / `Ingress` / `Addressing`), não em `.csproj` separados.

## 3. Princípios centrais

1. **Channels envia agora; não agenda.** Sem `ScheduledFor`, sem API de cancelamento. Quem quer entrega
   às 14:00 chama às 14:00. *(C1)*
2. **Quem chama nomeia um usuário e uma intenção, não um endereço.** Resolução de endereço, seleção de
   canal e opt-outs são política de entrega, e política de entrega vive aqui. *(C2)*
3. **Uma requisição vira N notificações.** "E-mail e Telegram" são duas linhas compartilhando um
   `group_id` — retry independente, falha independente, status honesto. *(C3)*
4. **Canais são portas.** Adicionar WhatsApp é uma implementação de `IChannelTransport` mais uma
   variante de template. Sem `switch` no dispatcher. *(C4)*
5. **A entrada é classificada estruturalmente, nunca semanticamente.** O módulo resolve um id numa
   tabela e lê a coluna que o módulo dono escreveu; nunca interpreta o que uma ação significa. *(C5)*
6. **A renderização acontece no enfileiramento, não no envio.** O que saiu fica guardado; o retry
   reenvia byte a byte o mesmo conteúdo; mudar um template amanhã não reescreve o histórico. *(C6)*

## 4. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Canal** | Um meio de entrega: `email` ou `telegram`. |
| **Canal do usuário** (`chn001`) | Onde um usuário pode ser alcançado num canal — um endereço utilizável só quando **verificado** e **habilitado**. |
| **Token de vínculo** (`chn002`) | O handshake de uso único e curta duração que liga um chat do Telegram a uma conta. Um chat id *nunca* é aceito do cliente. |
| **Notificação** (`chn006`) | Uma linha durável da fila: uma mensagem endereçada e já renderizada, com seu próprio retry/status. |
| **Grupo** | As N linhas em que uma requisição faz fan-out (e-mail + Telegram), com um `group_id`, lidas como uma notificação. |
| **Categoria** | O tipo de uma notificação (`agenda.reminder`, `identity.security`, …) — a chave em que a política de entrega se baseia. |
| **Preferência** (`chn005`) | A lista ordenada de canais em que uma categoria sai para um usuário. Vazia ⇒ silenciada. `identity.*` é obrigatória e a ignora. |
| **Template** | Um arquivo por `(chave, canal, locale)`, validado no startup. Renderiza assunto/corpo (e-mail) ou payload estruturado (Telegram). |
| **Interação** (`chn003`) | Um botão inline registrado e sua rota de volta: `(usuário, owner_module, ação, payload)` atrás de um único id que cabe num callback de 64 bytes do Telegram. |
| **Update de entrada** (`chn004`) | Todo update que o bot recebeu, registrado antes do processamento; o `update_id` do provedor torna reprocessar inofensivo. |
| **Triagem / classificação** | A ordenação estrutural de um update de entrada em `Interaction \| Command \| Message \| Discarded`. |

## 5. Escopo

### No escopo (implementado — ver [Status de implementação](implementation-status.md))

O schema `channels` (`chn001`–`chn006`); transportes e-mail + Telegram; templates em arquivo por canal
com validação no startup; a fila durável com fan-out, dedup por `(correlation_id, channel)`, retry e
dead-letter; preferências por usuário/categoria; o handshake de vínculo do Telegram; entrada por long
polling com triagem; botões de interação roteados aos donos; leitura de mídia de entrada; histórico de
entrega; e a purga diária do payload bruto.

### Fora do escopo / futuro (ver [product-plan.md](product-plan.md))

| Recurso | Status |
|---|---|
| **Horário de silêncio (quiet hours)** | Não construído — `chn005` guarda só a lista de canais hoje. O fuso IANA do usuário que precisam já está disponível nas preferências do Identity, então estão desbloqueadas, não bloqueadas. |
| **Métricas** (profundidade de fila, latência de dispatch, taxa de falha) | Planejado; depende da fiação de OpenTelemetry no Host. |
| **Driver de webhook** | Adiado — long polling cobre o ingress em todo lugar; ganha lugar quando o homelab tiver HTTPS público. |
| **Retry manual de uma linha morta** | Não planejado enquanto dead-letters são raras e inspecionáveis. |
| **Categorias de notificação do Finances** | Eventos do Finances ainda não publicados; um follow-up pequeno quando ele optar. |
