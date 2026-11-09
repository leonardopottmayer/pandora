# Blocos e editor

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Links e backlinks](links-and-backlinks.md), [Biblioteca de mídia](media-library.md)

---

## 1. O documento

`nte001_page.content` guarda um documento por página:

```json
{
  "version": 1,
  "blocks": [
    { "id": "b8f3k2", "type": "tags", "props": { "tags": ["dotnet", "mensageria"] } },
    { "id": "c1x9qa", "type": "heading", "props": { "level": 1 }, "content": [{ "text": "Por que um outbox" }] },
    { "id": "d72mpe", "type": "paragraph", "content": [
        { "text": "Relacionado: " },
        { "mention": { "page": "0198f6a2-…" } },
        { "text": " e mais", "marks": [{ "type": "bold" }] }
    ] },
    { "id": "e4a0zz", "type": "bulleted", "content": [{ "text": "Consumidores são idempotentes." }], "children": [
        { "id": "f0q1rr", "type": "bulleted", "content": [{ "text": "Guardar os ids processados." }] }
    ] }
  ]
}
```

- **Bloco:** `id` (único na página), `type` (`[a-z][a-z0-9-]*`), `props` opcionais, `content`
  (inlines) e `children` (blocos).
- **O backend confere só a forma** (`PageContent`): JSON válido, uma lista `blocks`, id e tipo válidos
  em todo bloco, ids únicos, `content` como lista de objetos, no máximo 2 MB, 10.000 blocos e 24
  níveis. O que as props de cada tipo significam é com o frontend.
- **Um tipo desconhecido sobrevive.** O editor mantém um bloco que não conhece do jeito que veio e o
  salva de volta intacto.

## 2. Inlines

| Inline | Guardado como | Notas |
|---|---|---|
| Texto | `{ text, marks? }` | Marcas: `bold`, `italic`, `underline`, `strike`, `code`, `link` (`href`), `color` e `highlight` (`attrs.color`), `comment` (`attrs.thread`) |
| Menção a página | `{ mention: { page } }` | Digitada com `@` ou `[[`; desenhada em caixa com o título atual da página |
| Data | `{ date: { value, reminderId? } }` | `YYYY-MM-DD` ou `YYYY-MM-DDTHH:mm` (local); pode carregar um lembrete do Agenda |
| Status | `{ status: { label, color? } }` | Uma pílula |
| Item de outro módulo | `{ item: { module, kind, id, label } }` | Tarefas e lembretes do Agenda, contas e cartões do Finances; o rótulo fica como estava ao ligar |
| Fórmula inline | `{ math: { latex } }` | KaTeX; `$x^2$` digitado no texto vira uma |

As oito cores, para texto, fundos, status e destaques: `gray`, `orange`, `yellow`, `green`, `blue`,
`purple`, `magenta`, `red`.

O texto de busca lê cada inline como suas palavras: uma data como o valor, um status ou item como o
rótulo, uma fórmula como o LaTeX.

## 3. Catálogo de blocos

### Básicos

| Bloco | Guardado como |
|---|---|
| Parágrafo | `paragraph` |
| Título 1–3 | `heading`, `props.level`; `props.toggle: true` o torna recolhível, com `props.open` e a seção em `children` |
| Item de lista com marcador / numerada | `bulleted`, `numbered` (aninhamento em `children`) |
| Tarefa | `todo`, `props.checked` |
| Lista de alternância | `toggle`, `props.open`, corpo em `children` |
| Citação | `quote` |
| Divisor | `divider` |
| Callout | `callout`, `props.tone` (`note`, `info`, `success`, `warning`, `error`), título e dobra (`+`/`-`) opcionais, corpo em `children` |
| Colunas | `columns` com 2 a 4 filhos `column`; uma tela estreita empilha |

Parágrafos, títulos, itens de lista, citações, código, callouts e listas de alternância aceitam
`props.color`, `props.background` e `props.align` (`center`, `right`).

### Mídia

| Bloco | Guardado como | Aparece como |
|---|---|---|
| Imagem | `image`, `props.mediaId` (ou `src`), `width`, `align`; legenda em `content` | A imagem; um clique abre em tela cheia |
| Galeria | `gallery`, `props.items[]` de `{ mediaId, caption? }` | Uma grade de imagens da biblioteca |
| Vídeo | `video`, `props.url` | Player do YouTube, Vimeo ou Loom, ou o do navegador para um arquivo de vídeo |
| Áudio | `audio`, `props.mediaId` (+ `name`) ou `props.url` | O player do navegador |
| Arquivo | `file`, `props.mediaId`, `name` | Um cartão com ícone, nome e tamanho; um clique baixa |
| PDF | `pdf`, `props.mediaId` (+ `name`) ou `props.url` | O leitor de PDF do navegador, na página |

### Avançados

| Bloco | Guardado como | O que faz |
|---|---|---|
| Código | `code`, `props.language` | Um bloco de código |
| Tabela | `table`, `props.header`, `props.rows` (linhas de células, cada uma uma lista de inlines) | Tabela editável com linha de cabeçalho |
| Mermaid | `mermaid`, `props.code` | O diagrama (segurança estrita, SVG sanitizado); o código é editado embaixo |
| Fórmula | `math`, `props.latex` | Uma fórmula KaTeX (`trust: false`) |
| Bloco sincronizado | `synced` com `children`; as cópias são `synced-ref`, `props.pageId`, `props.blockId` | Ver [Links e backlinks §4](links-and-backlinks.md#4-blocos-sincronizados) |
| Botão de modelo | `template-button`, `props.label`, o modelo em `children` | Um clique insere uma cópia abaixo, com ids novos |
| Decisão | `decision`, `props.status` (`decided`, `rejected`, ou nenhum), texto em `content` | Uma linha com o status como pílula |
| Trilha | `breadcrumb` | O espaço, as pastas e as páginas-pai da página |
| Lista de subpáginas | `subpages` | As subpáginas da página, na ordem |
| Páginas por tag | `tag-pages`, `props.tags` | As páginas que têm essas tags |
| Propriedades da página | `properties`, `props.rows[]` de `{ key, value }`, cada um uma lista de inlines | Uma tabela de duas colunas; lida pela visão de tabela da pasta |
| Bloco de IA | `ai`, `props.prompt`, `props.output`, `props.generatedAt` | Ver [IA §5](ai.md#5-bloco-de-ia) |
| Tags | `tags`, `props.tags` (nomes) | Editado no cabeçalho da página, salvo como o primeiro bloco; ver [Tags](tags.md) |

### Dados (blocos ao vivo)

Guardam só a configuração. Os números ao vivo nunca entram no documento, no autosave, na busca, nas
versões nem nos modelos, e uma página pública mostra "Conteúdo privado" sem consultar nada.

| Bloco | Configuração | Mostra |
|---|---|---|
| Agenda | `agenda-list`: `{ mode: "today" }`, `{ mode: "tasks", listId }` ou `{ mode: "events", days: 7 \| 30, calendarId? }` | Eventos, tarefas e lembretes de hoje; as tarefas abertas de uma lista; ou os próximos eventos, somente leitura, com "Abrir no Agenda" e Atualizar |
| Finances | `finance-summary`: `{ mode: "account", accountId }`, `{ mode: "card", cardId }` ou `{ mode: "expenses", month? }` | O saldo efetivado e projetado de uma conta; o limite total e disponível de um cartão; ou as despesas efetivadas de um mês por categoria, por moeda |

Usam os endpoints, hooks e query keys do próprio outro módulo, e exigem a feature e as permissões de
leitura dele. Uma origem que sumiu, foi arquivada ou é de outro usuário aparece "indisponível"; nada
cai para outra origem. As exportações em Markdown e HTML escrevem só `[Agenda]` ou `[Finances]`.

### Embeds

| Bloco | Guardado como | Aparece como |
|---|---|---|
| Link de página | `page-link`, `props.pageId`, `props.look` (`preview`, ou cartão quando ausente) | Um cartão, ou as primeiras linhas da página; nunca a página inteira |
| Bookmark | `bookmark`, `props.url`, `title`, `description`, `siteName`, `imageUrl`, `iconUrl` | Um cartão; o que o site disse é salvo quando o bloco é criado |
| Embed | `embed`, `props.url` | O frame do site para YouTube, Vimeo, Loom, Figma, Google Maps, Drive e Docs, Miro, CodePen e gists; o HTML do site para X, Bluesky e qualquer site com oEmbed; senão um cartão |

`GET /notes/links/preview` lê uma URL no servidor, recusando endereços privados (SSRF), para preencher
um bookmark ou um embed. Todo frame fica em sandbox; o HTML de um oEmbed ou de um gist roda num frame
sem a origem do Pandora, que só informa a própria altura.

## 4. O editor

`components/visual/VisualEditor.tsx`, TipTap 3. `lib/blocks.ts` converte entre os blocos e o JSON do
TipTap, e `lib/visualSchema.ts` dá um `blockId` a todo nó de bloco.

- **Autosave** 800 ms depois da última mudança, com "Salvando…" / "Salvo" no cabeçalho.
- **Menu `/`** em grupos: Básicos, Mídia, Avançados, Dados, Inline, Embed. Digitar também acha cores
  (`/vermelho`, `/fundo azul`; `/red`, `/blue background`) e comandos "transformar em", que agem no
  bloco onde a barra foi digitada.
- **`@`** lista páginas, depois datas (`@hoje`, `@amanhã 14h`, `@25/12`, `@2026-12-25`) e, quando algo
  é digitado, itens de outros módulos. `@lembrar amanhã 9:00` (`@remind …`) cria a data com o lembrete
  no Agenda. Tirar a data do texto não apaga o lembrete.
- **`:`** mais uma palavra oferece emoji (`:fogo`, `:fire`); o painel do sistema tem o resto.
- **Menu do bloco** na alça de arrastar: transformar em, cor, alinhamento, copiar link do bloco,
  duplicar, mover para página, comentar, datas do bloco, excluir.
- **Vários blocos:** Ctrl/Cmd-arrastar, Shift+↑/↓ ou Ctrl/Cmd+A selecionam blocos inteiros; as mesmas
  ações valem para todos, e a alça arrasta todos juntos. Mover para outra página acrescenta lá, com ids
  novos.
- **Link para um bloco:** `/notes/<página>#block-<id>` rola até o bloco e o marca por um momento.
- **Barra de seleção:** marcas, link, cor, comentário e as ações de IA.
- **Colar** um arquivo manda para a biblioteca (imagem vira bloco de imagem, áudio bloco de áudio, PDF
  bloco de PDF, outros arquivos cartão de arquivo). Um link sozinho numa linha pergunta: link,
  bookmark ou embed.
- **Somente leitura** quando a página está travada, numa página pública, e no celular até "Editar".
- **Sumário** (títulos 1–3) ao lado da página numa tela larga, recolhido acima dela nas outras,
  marcando a seção que está sendo lida.

Mermaid e KaTeX carregam na primeira vez que uma página mostra um deles (chunks separados).
