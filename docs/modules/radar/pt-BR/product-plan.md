# Módulo Radar — Plano de Produto

> **Status:** design registrado (2026-11-07); nada construído ainda. A entrega segue as etapas R1–R5 (§9).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Communications](../../communications/pt-BR/README.md) (newsletters) ·
> [Channels](../../channels/README.md) (o digest) · [Assistant](../../assistant/README.md) (o cliente de
> IA) · [Admin](../../admin/pt-BR/README.md) (flags e permissões)

---

## 1. O que o módulo faz

O **Radar** mantém o usuário atualizado em tecnologia sem curadoria manual. Ele acompanha as fontes que
o usuário escolhe (canais do YouTube, Medium, Reddit, Hacker News, blogs, releases do GitHub, newsletters
que chegam por e-mail), pergunta a um modelo de IA o quanto cada item novo combina com os interesses do
usuário e entrega os melhores como um **digest** no Telegram ou por e-mail e numa tela de feed. Os 👍/👎
do usuário nos itens ensinam o que importa.

```
fontes ──► busca (polling) ──► itens novos ──► nota + resumo da IA ──► tela de feed
   ▲                                                  │
   │                                                  └──► digest (diário/semanal) ──► Channels ──► Telegram / e-mail
   └────────────────── feedback 👍 / 👎 ◄────────────────────────────────────────────────────┘
```

### O que ele não é

- **Não é um leitor de RSS completo.** Os itens ficam guardados para a nota e o digest, não como
  arquivo: são apagados depois de um tempo, a não ser que estejam salvos (§6.4).
- **Não é um cliente de e-mail.** As newsletters ficam no [Communications](../../communications/pt-BR/README.md);
  o Radar só lê as que estão num marcador escolhido pelo usuário.
- **Não é raspador personalizado.** As fontes são conectores tipados que o Pandora oferece (§4). Uma
  fonte definida pelo usuário (página qualquer, seletor, API) é uma ideia para depois.

---

## 2. Nomes e coordenadas

| Item | Valor |
|---|---|
| Nome do módulo | `Radar` (UI: "Radar") |
| Projetos do backend | `Pottmayer.Pandora.Modules.Radar.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `radar` |
| Prefixo de tabela | `rdrXXX_`, PK `uuid_generate_v7()` |
| Seção de options | `Pandora:Radar` |
| Base da API | `/api/v{version}/radar`, JWT |
| Frontend | `client-web/src/modules/radar`, rotas `radar` (feed), `radar/sources`, `radar/settings` |
| Migrations | `migrations/migrations/radar/` |
| Categoria no Channels | `radar.digest` |

---

## 3. Princípios

1. **Fontes são conectores tipados.** Cada tipo de fonte é uma classe atrás de `ISourceConnector` (§4).
   A maioria é RSS/Atom por baixo, então uma nova costuma ser um modelo de URL sobre o conector RSS.
2. **Barato antes de esperto.** Duplicados, palavras bloqueadas e itens antigos saem antes de a IA vê-los.
   A IA dá nota em lotes, com um teto diário, para o custo ficar previsível.
3. **A IA se explica.** Toda nota vem com um motivo de uma linha e um resumo curto no idioma do usuário,
   para ele entender por que um item foi escolhido.
4. **O feedback é a outra metade do perfil.** O perfil de interesse escrito diz o que o usuário quer; os
   👍/👎 recentes mostram. Os dois entram no prompt da nota.
5. **A entrega é do Channels.** O Radar pede ao Channels para notificar o usuário (`NotifyUserRequested`);
   nunca fala com Telegram ou SMTP direto.
6. **Do usuário.** Fontes, itens e feedback são por usuário, como em todos os módulos.

---

## 4. Fontes

### 4.1 O contrato

`ISourceConnector`, registrado no DI e resolvido pelo `kind` da fonte:

| Membro | O que faz |
|---|---|
| `Kind` | O tipo gravado, com hífen (`youtube-channel`) |
| `ResolveAsync(input)` | Transforma o que o usuário colou (uma URL, um handle, um subreddit) na config da fonte e num nome de exibição, ou num erro de validação |
| `FetchAsync(source, state)` | Devolve os itens publicados desde a última busca e o novo estado da busca (ETag, `Last-Modified`, id do último item) |

Um item buscado traz: id externo, URL canônica, título, autor, data de publicação, resumo ou descrição
(texto puro, cortado) e dados extras opcionais por tipo (duração do vídeo, pontos, número de comentários).

### 4.2 Conectores das primeiras etapas

| Tipo | O usuário informa | Por baixo | Notas |
|---|---|---|---|
| `rss` | URL do feed, ou URL do site com `<link rel="alternate">` | RSS/Atom | Cobre blogs e qualquer coisa com feed |
| `youtube-channel` | URL do canal ou `@handle` | `youtube.com/feeds/videos.xml?channel_id=` | O id do canal é lido da página do canal uma vez, ao resolver |
| `medium` | Perfil, publicação ou tag | `medium.com/feed/...` | |
| `reddit` | Subreddit, nota mínima opcional | `reddit.com/r/<nome>/top.rss?t=day` | |
| `github-releases` | `owner/repo` | `github.com/<owner>/<repo>/releases.atom` | |
| `hacker-news` | Pontos mínimos | API pública do HN (top stories) | Não é RSS; conector próprio |
| `newsletter` | Um marcador do Communications | `MailMessageReceived` + leitura do corpo pelo Communications | Empurrado pelo evento, sem polling (§6.5) |

Planejados para depois: YouTube pela Data API (via Integrations) se a leitura da página parar de
funcionar, podcasts, X/Bluesky, dev.to, fontes definidas pelo usuário.

### 4.3 Busca

- Um job em background busca cada fonte ativa no seu intervalo (padrão 60 minutos, mínimo 15), com
  `If-None-Match`/`If-Modified-Since`, então um feed sem mudança custa um 304.
- Os itens são deduplicados por usuário pelo hash da URL canônica (sem parâmetros de rastreamento), então
  o mesmo artigo vindo do Reddit e do HN recebe nota uma vez só.
- Itens mais antigos que a primeira busca da fonte são ignorados, então seguir uma fonte nova não enche o
  feed com o arquivo dela.
- Uma fonte que falha 5 vezes seguidas é pausada e marcada na tela de fontes com o último erro.

---

## 5. Nota

### 5.1 Entradas

- **Perfil de interesse:** texto livre escrito pelo usuário ("backend .NET, arquitetura, PostgreSQL, IA
  aplicada; não me interessa frontend nem cripto"), mais palavras-chave opcionais **obrigatórias** e
  **bloqueadas**.
- **Feedback recente:** os títulos dos últimos 20 itens 👍 e 20 👎, como exemplos.
- **Os itens:** título, fonte, autor e resumo, em lotes de até 20 por chamada.

### 5.2 Saída por item

| Campo | O quê |
|---|---|
| `score` | 0–100, o quanto o item combina com o perfil |
| `summary` | Duas ou três frases no idioma do usuário |
| `reason` | Uma linha: por que essa nota |
| `tags` | Até 5 assuntos, para os filtros do feed |

O modelo responde em JSON estruturado. Um item em que o modelo falha fica com `score = null` e é tentado
de novo uma vez na próxima rodada.

### 5.3 O cliente de IA e o custo

- O Radar usa o mesmo cliente de chat de IA do Tars que o Assistant usa (`IAiChatCompletionClientFactory`,
  Gemini), com a própria configuração de modelo em `Pandora:Radar:Ai`.
- Antes da IA: palavras bloqueadas derrubam um item (`score = 0`, motivo "palavra bloqueada"); palavras
  obrigatórias o marcam para o digest independentemente da nota.
- Tetos nas options: itens com nota por usuário por dia, e itens por lote. Passando do teto, os itens
  esperam o dia seguinte.

### 5.4 Feedback

O usuário marca um item com 👍 ou 👎 no feed ou pelos botões do digest no Telegram. O feedback não muda
nenhuma nota já dada; ele entra nos próximos prompts (§5.1). Etapas futuras podem testar embeddings e
semelhança com os itens 👍 (§7).

---

## 6. Pandora: o módulo Radar

### 6.1 Modelo de dados

| Tabela | O que guarda | Colunas principais |
|---|---|---|
| `rdr001_source` | Uma fonte seguida | `id`, `user_id`, `kind`, `name`, `config jsonb`, `interval_minutes`, `status` (`active`/`paused`/`failing`), `fetch_state jsonb`, `last_fetched_at`, `last_error`, `failure_count`, timestamps |
| `rdr002_item` | Um item buscado | `id`, `user_id`, `source_id`, `external_id`, `url`, `url_hash`, `title`, `author`, `summary_source`, `published_at`, `extra jsonb`, `score`, `ai_summary`, `ai_reason`, `tags text[]`, `scored_at`, `feedback` (`up`/`down`/null), `saved`, `read_at`, `digested_at`, timestamps |
| `rdr003_preferences` | As configurações do Radar do usuário | `user_id` (PK), `interest_profile`, `must_have text[]`, `blocked text[]`, `digest_frequency` (`daily`/`weekly`/`off`), `digest_time`, `digest_weekday`, `digest_min_score`, `digest_max_items`, timestamps |
| `rdr004_digest` | Um digest enviado | `id`, `user_id`, `sent_at`, `item_ids uuid[]`, `correlation_id` |

Único em `rdr002 (user_id, url_hash)`. Índice `rdr002 (user_id, published_at desc)` para o feed e
`rdr002 (user_id, scored_at) where score is null` para a fila de notas.

### 6.2 Jobs

| Job | Roda | Faz |
|---|---|---|
| Busca | A cada minuto, pega as fontes vencidas | `FetchAsync`, deduplica, insere os itens novos |
| Nota | A cada poucos minutos | Agrupa os itens sem nota por usuário, dentro do teto diário |
| Digest | A cada poucos minutos | Para os usuários cuja hora do digest chegou (no fuso deles), pega os itens não enviados com `score >= digest_min_score` (e os obrigatórios), mais novos primeiro, até `digest_max_items`, e pede o envio ao Channels |
| Limpeza | Diário | Apaga itens mais velhos que `RetentionDays` que não estão salvos e não têm feedback |

### 6.3 Digest

- Enviado pelo Channels com `NotifyUserRequested`, categoria `radar.digest`, template `radar-digest`; as
  preferências de canal do usuário decidem Telegram, e-mail ou os dois. O horário de silêncio vale.
- Cada item: número, título (link), fonte, o resumo da IA. No Telegram, uma linha de botões por item
  (`👍 3` / `👎 3`) cujo callback volta ao Radar como feedback.
- Nada acima do limite: nenhuma mensagem (sem o ruído de "nada hoje").

### 6.4 Options

```json
"Pandora": {
  "Radar": {
    "Ai": { "Model": "gemini-2.5-flash" },
    "MaxScoredItemsPerDay": 300,
    "BatchSize": 20,
    "MaxSourcesPerUser": 100,
    "MinIntervalMinutes": 15,
    "RetentionDays": 90,
    "FailuresBeforePause": 5
  }
}
```

### 6.5 Newsletters do Communications

- O usuário escolhe um marcador do Communications (por exemplo `newsletters`). Uma fonte `newsletter`
  assina `MailMessageReceived` e fica com as mensagens que têm esse marcador.
- O evento não traz o corpo, então o Communications precisa de uma pequena porta em
  `Communications.Abstractions` (`IMailMessageReader.GetTextAsync(userId, messageId)`) que devolve o
  texto puro de uma mensagem. O Radar divide uma newsletter nos links dela quando ela lista vários (um
  item por link com o parágrafo dele), ou a mantém como um item só.
- Construir a porta faz parte da R4, no Communications.

### 6.6 Flags e permissões

Declaradas em `Radar.Abstractions` como constantes (`RadarFeatures`, `RadarPermissions`):

| Tipo | Chave | Protege |
|---|---|---|
| Feature | `radar` | O módulo inteiro |
| Feature | `radar.ai` | A nota (desligada: os itens chegam sem nota, o digest manda os mais novos) |
| Feature | `radar.digest` | O digest |
| Permissão | `radar.sources.{read,create,update,delete}` | Fontes |
| Permissão | `radar.items.{read,update}` | O feed; feedback, salvo e lido |
| Permissão | `radar.preferences.{read,update}` | Perfil e configurações do digest |

O papel padrão `user` recebe todas.

### 6.7 API (esboço)

| Método | Rota | O quê |
|---|---|---|
| `POST` | `/radar/sources/resolve` | O que o usuário colou → tipo, config e nome, antes de salvar |
| `GET/POST` | `/radar/sources` | Listar, seguir uma fonte |
| `PUT/DELETE` | `/radar/sources/{id}` | Alterar (nome, intervalo, pausa), deixar de seguir |
| `POST` | `/radar/sources/{id}/fetch` | Buscar agora |
| `GET` | `/radar/items` | O feed: filtros por fonte, tag, nota mínima, não lidos, salvos; paginado |
| `PATCH` | `/radar/items/{id}` | Feedback, salvo, lido |
| `GET/PUT` | `/radar/preferences` | Perfil, palavras-chave, configurações do digest |
| `POST` | `/radar/digest/preview` | O digest que sairia agora, sem enviar |

### 6.8 Frontend

- **Feed:** cards ordenados por nota ou data, com ícone da fonte, título, resumo da IA, motivo no hover,
  tags, 👍/👎, salvar, abrir. Filtros no topo; "mostrar notas baixas" desligado por padrão.
- **Fontes:** uma tabela com tipo, nome, status, última busca e erro; "seguir" abre um diálogo que
  resolve primeiro a URL colada e mostra o que encontrou.
- **Configurações:** perfil de interesse (textarea), palavras obrigatórias e bloqueadas, frequência,
  horário, limite e tamanho do digest, e "prévia do digest".

---

## 7. Pontos de extensão

| Ponto | Como estende | Exemplo |
|---|---|---|
| `ISourceConnector` | Uma classe nova, muitas vezes um modelo de URL sobre `rss` | Podcasts, dev.to, Bluesky, uma fonte definida pelo usuário |
| Nota | Outro avaliador atrás da mesma saída (§5.2) | Embeddings e semelhança com os itens 👍, mais barato que um prompt |
| Enriquecimento do item | Um passo entre a busca e a nota | Texto completo do artigo, transcrições do YouTube |
| Digest | Outro template ou canal pelo Channels | Um "melhores da semana", um digest por assunto |
| Ferramentas do Assistant | Ferramentas sobre as queries da Application | "o que saiu de novo sobre .NET essa semana?" |
| Evento de integração | `RadarItemSaved`, quando houver um consumidor | Salvar um item como página no Notes |

---

## 8. Decisões

| # | Decisão | Escolhida | Em vez de |
|---|---|---|---|
| 1 | Fontes | Conectores tipados que o Pandora oferece, a maioria sobre RSS | Fontes definidas pelo usuário agora |
| 2 | Ordem | Depois do e-mail do Communications (feito na M1–M4); newsletters pelo evento dele | O Radar lendo as caixas de e-mail sozinho |
| 3 | Relevância | Prompt de IA com perfil + feedback recente, em lotes, com teto diário | Só regras de palavra-chave; embeddings primeiro |
| 4 | Conteúdo avaliado | Título e resumo do feed | Buscar todo artigo completo (depois, §7) |
| 5 | Itens | Por usuário, deduplicados pela URL canônica | Itens compartilhados entre usuários |
| 6 | Entrega | Digest pelo Channels num horário, mais a tela de feed | Uma notificação por item |
| 7 | Retenção | 90 dias, a não ser que salvo ou avaliado | Guardar tudo |
| 8 | Nome | `Radar` | — |

> ponytail: os itens são por usuário, então dois usuários seguindo o mesmo feed o buscam duas vezes;
> compartilhar itens entre usuários só se o Pandora um dia tiver muitos usuários nas mesmas fontes.

---

## 9. Entrega

| Etapa | Entrega | Pronto quando |
|---|---|---|
| **R1** | Fontes (`rss`, `youtube-channel`, `medium`, `reddit`, `github-releases`), resolver, job de busca, deduplicação, as telas de feed e fontes, flags e permissões | Seguir os cinco canais do YouTube de exemplo e um blog enche o feed só com itens novos |
| **R2** | Nota com o perfil de interesse, palavras-chave, feedback no feed, o teto diário | Os itens mostram nota, resumo e motivo; itens 👎 deixam de aparecer no topo depois de alguns dias |
| **R3** | O digest pelo Channels, botões do Telegram como feedback, prévia | Um digest diário chega no Telegram no horário escolhido e os botões dele registram feedback |
| **R4** | `newsletter` pelo Communications (a porta de leitura), `hacker-news` | Uma newsletter com o marcador vira itens no próximo digest |
| **R5+** | Texto completo do artigo, transcrições, embeddings, mais conectores, ferramentas do Assistant, fontes definidas pelo usuário | Uma de cada vez, cada uma com seu plano |
