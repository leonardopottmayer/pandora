# Editor e blocos ricos

[← Voltar ao índice](README.md) · Relacionados: [Pages e hierarquia](pages-and-hierarchy.md), [Biblioteca de mídia](media-library.md), [Tags](tags.md)

O editor é **100% frontend**. Tudo que ele produz é markdown comum, que o `content_markdown` já
guardava — nada aqui exigiu mudança de backend.

---

## 1. A superfície

Dois editores sobre o mesmo texto. `components/visual/VisualEditor.tsx` é o **editor visual** (TipTap),
em que se escreve na página já desenhada, estilo Notion — ver [§9](#9-editor-visual).
`components/MarkdownEditor.tsx` é o CodeMirror 6 sobre o markdown cru; `components/MarkdownPreview.tsx`
renderiza com `marked` + DOMPurify. A `NotesPage` tem quatro modos — `visual` (o padrão, e o do
celular ao tocar em Editar), `edit` (markdown), `split` (markdown e leitura lado a lado) e `preview`.

O que se salva é sempre o markdown: os dois editores leem e escrevem o mesmo `content_markdown`, e
trocar de modo leva junto o que ainda não foi salvo (os dois começam do rascunho, não da page salva).
As seções 4 a 7 descrevem o editor de markdown; o visual reaproveita as mesmas listas e filtros.

## 2. Autosave

`hooks/useAutosave.ts` faz debounce de **800 ms** e salva por uma mutation do TanStack Query, expondo
um status que a página exibe. O rascunho (`PageDraft`) guarda título, ícone e corpo juntos, então os
três salvam pelo mesmo caminho — o ícone não é uma chamada à parte.

`hooks/useDebouncedValue.ts` (200 ms) é o outro debounce, usado pelo palette de busca: uma requisição
por pausa na digitação, não por tecla.

## 3. Upload inline

Colar (`Ctrl+V`) ou arrastar um arquivo envia ele para a biblioteca de mídia, em "Não organizadas"
(`POST /notes/media`), e insere `![nome](/api/v1/notes/media/{id})` no cursor — um link `[nome](…)` para
um arquivo que não é imagem. O rótulo é o nome do item, que é também o que o handler de download usa
depois para nomear o arquivo salvo. `/imagem` e `/arquivo`, ou o botão de imagem da barra, inserem da
biblioteca — ver [Mídia dentro das notas](media-library.md#9-mídia-dentro-das-notas).

## 4. Três menus de autocomplete

Os três são registrados no `@codemirror/autocomplete` e compartilham as mesmas três mecânicas:

- **Filtragem nossa**, com `filter: false` no `CompletionResult`. Com o filtro do CodeMirror ligado,
  uma opção que casou por outra coisa que não o `label` — uma page que casou pelo slug, um comando que
  casou pelo label traduzido — seria descartada logo depois de a termos incluído.
- **Entradas por ref**, não por dependência de efeito. A lista de pages e a função `t` mudam com
  frequência; reconstruir o editor a cada mudança jogaria fora o histórico de undo.
- **Tooltip pendurada em `document.body`** (`tooltips({ parent })`), porque o editor mora dentro de um
  `Card` com `overflow: hidden` que cortaria o menu perto do rodapé do painel.

| Gatilho | Fonte | Regras |
|---|---|---|
| `[[` | `wikilinkTriggerAt` em `lib/wikilinks.ts` | Abre depois de `[[` e de `![[`, que escreve o alvo igual. **Recusa** depois de `]` ou de `\|` — link fechado ou metade do alias não são mais o alvo sendo digitado. `filterPages` casa por **título e slug**, então `cafe` acha `Café com Pão`; teto de 10 opções. Ao aplicar, um `]]` já existente logo depois do cursor é **reaproveitado** em vez de duplicado. |
| `#` | `lib/tags.ts` | Completa **só tags existentes** — tag nova se digita, que é justamente o ponto de ela morar no texto. |
| `/` | `slashTriggerAt` em `lib/slashCommands.ts` | Só abre com a `/` no início da linha ou depois de espaço (então `src/lib` no meio do texto nunca dispara), e fecha ao primeiro espaço digitado. `filterCommands` casa por **id e label traduzido**. |

## 5. Slash commands

O `SLASH_COMMANDS` insere markdown puro — toda entrada é algo que o usuário poderia ter digitado à
mão, e é isso que mantém o documento round-trippable.

| Grupo | Entradas |
|---|---|
| Blocos | `h1`, `h2`, `h3`, lista com marcador, lista numerada, todo (`- [ ] `), citação, bloco de código, divisor, recolher (`<details>` com o cursor no resumo, linhas em branco em volta do corpo para o markdown dentro continuar renderizando), tabela, imagem, arquivo, link para página (abre o `PagePickerModal`: a árvore de cada espaço com os ícones, e uma busca que mostra o caminho de cada página; escolhida, entra `[[Título]]`) |
| Callouts | um por tipo de callout — `note`, `tip`, `info`, `warning`, `danger`, `quote` |

Cada comando declara o texto que insere e o deslocamento do cursor dentro dele.

## 6. Callouts

Sintaxe Obsidian, seis tipos: `note`, `tip`, `info`, `warning`, `danger`, `quote`.

`lib/callouts.ts` é uma **extensão do `marked`** (tokenizer + renderer de nível bloco), não um
pré-processamento de string como os wikilinks — assim o corpo do callout continua passando pelo lexer
e `**negrito**` dentro dele funciona.

- Um tipo desconhecido (`> [!frobnicate]`) o tokenizer **recusa**, e a coisa cai no blockquote normal
  do `marked`. É de propósito: a sintaxe é feita para degradar em blockquote em qualquer outro
  renderer markdown, e é isso que mantém o arquivo portável.
- Callout sem título cai no nome do tipo **traduzido**, então o parser é um `Marked` próprio memoizado
  por `i18n.language` no `MarkdownPreview` — mutação global via `marked.use()` foi evitada de
  propósito.
- O ícone é emoji embutido no HTML; a cor vem de um `--notes-callout-accent` por tipo, que serve de
  borda e, via `color-mix`, de fundo. Um token só por tipo.

**Callouts recolhíveis** usam o sinal do Obsidian depois do marcador: `> [!tip]- Título` começa
fechado, `> [!tip]+ Título` começa aberto. Viram `<details>`/`<summary>`, então quem recolhe é o
navegador, sem script, e em qualquer outro lugar degradam para blockquote.

## 7. Tabelas markdown

`lib/markdownTables.ts` é função pura sobre as linhas do documento; `lib/editorCommands.ts` é a única
parte que conhece o `EditorView`. Foi essa separação que permitiu testar Tab contra um editor montado,
em vez de só testar a matemática.

- **Tab / Shift+Tab** movem entre células, **reformatando a tabela inteira antes**, então as barras se
  realinham enquanto se digita. `formatTable` é idempotente (tem teste) — Tab repetido não fica
  alargando coluna.
- A largura mínima de coluna é 3 (o `---`), e os `:` de alinhamento que o usuário escreveu são
  preservados, esticando só os traços entre eles.
- Tab **recusa** — deixando o Tab valer o que valia — fora de tabela, com seleção aberta, e numa linha
  de pipes solta que ainda não é tabela: reformatar uma linha que a pessoa ainda está escrevendo seria
  pior que não fazer nada. Shift+Tab na primeira célula também recusa.
- Tab na última célula **acrescenta uma linha**; a linha separadora é pulada nos dois sentidos.
- Não há atalho de "remover linha/coluna": é markdown, apaga-se a linha.

**Alinhamento de coluna pela UI** está fora de escopo; se entrar, o lugar é a linha separadora em
`markdownTables.ts`.

## 8. Preview

O `MarkdownPreview` renderiza com `marked`, sanitiza com DOMPurify, e então pós-processa:

1. **Wikilinks** são resolvidos contra a lista de pages usando `lib/wikilinks.ts` — o espelho do
   parser do backend no frontend, para o preview linkar o que o próximo save vai linkar. Um
   `[[alvo]]` não resolvido vira um link de "create on click". Embeds renderizam como links comuns.
2. **`#tags`** viram chips coloridos, pintados com a lista de tags do módulo. O chip define `color` e
   `border-color` **inline** em vez de custom property: o HTML passa pelo DOMPurify, e o filtro de CSS
   dele é motivo suficiente para não depender de nada exótico sobreviver.
3. **URLs de mídia** são trocadas por object URLs **depois** do DOMPurify — a allow-list de URI dele
   não cobre `blob:`, então um object URL embutido antes seria removido. Ver
   [Biblioteca de mídia](media-library.md#7-servir-os-bytes).

Cliques em wikilinks, chips de tag e links de arquivo são todos interceptados pelo mesmo handler.

**Vídeos e frames** (`lib/embeds.ts`). Um link do YouTube ou do Vimeo **sozinho numa linha** vira o
player (`youtube-nocookie.com` / `player.vimeo.com`, com o `?t=` virando o início); no meio de uma
frase continua link. Um `<iframe>` escrito à mão só sobrevive apontando para o caminho de embed do
YouTube, do Vimeo ou do Google Maps em https — qualquer outro é removido. Todo frame que sobrevive
ganha um `sandbox`, `allow`, `referrerpolicy` e `loading="lazy"` fixos, seja lá com o que foi escrito.
Esse hook fica numa instância do DOMPurify só do módulo, então não alcança os outros lugares que
sanitizam HTML (email).

## 9. Editor visual

TipTap 3 com o `@tiptap/markdown`, que lê e escreve markdown pelo mesmo `marked` do preview (uma
instância própria, para os tokenizers dele não vazarem para o preview). `lib/visualMarkdown.ts` define o
schema e as regras de markdown; o componente só acrescenta as views.

- **Blocos nativos**: parágrafo, títulos, listas (inclusive de tarefas e aninhadas), citação, bloco de
  código, divisor, tabela (com o alinhamento das colunas), negrito, itálico, tachado, código e link.
- **Nós nossos**: o **wikilink** é um nó só (`[[alvo|alias]]`, `![[alvo]]`), desenhado como no preview e
  clicável — abre a page ou cria a que falta. A **imagem** guarda largura, alinhamento e legenda no texto
  alternativo, como sempre; selecionada, mostra botões de alinhamento que reescrevem esse texto.
- **Vídeo**: um link do YouTube ou do Vimeo sozinho na linha mostra o player embaixo dele, com o mesmo
  sandbox do preview (`components/visual/videoPreview.ts`). O player é decoração: o link continua
  editável e é só ele que vai para o markdown.
- **Callouts e blocos recolhíveis**: `lib/visualBlocks.ts` define nós nativos com campo de título e
  corpo editado diretamente no mesmo editor (formatação, listas, tabelas, links, mídia e blocos
  aninhados). O título salva a cada alteração; Enter/Seta para baixo entra no corpo, Ctrl/Cmd+Enter
  sai do bloco, e desfazer/refazer inclui alterações do título. A seta recolhe os blocos que suportam
  isso e salva o estado aberto (`[!tipo]+`/`[!tipo]-` ou `<details open>`). `/recolher` e os comandos de
  callout focam o título novo. O corpo continua markdown; não há mudança de backend ou schema.
- **Bloco cru**: HTML de bloco não suportado (`<iframe>`, `<img>` escrito à mão), tipos desconhecidos
  de callout e `<details>` personalizados (atributos extras ou HTML no resumo) ficam **intocados**,
  desenhados pelo `MarkdownPreview`. Duplo clique ou o botão no canto abre o código-fonte; Esc,
  Ctrl+Enter ou sair do campo devolve. Resumos simples escapam caracteres HTML ao serializar.
- **Menus** `/`, `[[` e `#` são sugestões do TipTap desenhadas por um menu React só. O `[[` oferece
  também o título digitado quando nenhuma page tem esse nome (a page nasce no clique, como no preview).
- **Barra da seleção** (negrito, itálico, tachado, código, link), **barra da tabela** (linha, coluna,
  excluir) e **alça de arrastar** para mover blocos.
- **Colar** um arquivo envia para a biblioteca, como no outro editor; colar texto puro (de um `.md`, do
  editor de markdown) entra como os blocos que ele descreve.

**Ida e volta.** Abrir uma page no editor visual não muda nada: só uma edição dispara o save. Ao
salvar, o markdown é reescrito na forma do editor — `*` vira `-` nas listas, `_itálico_` vira
`*itálico*`, `<` e `&` no texto viram `&lt;` e `&amp;`, colchetes soltos ganham `\` — o que **renderiza
igual**. Um link cujo texto é o próprio endereço volta a ser URL pura (senão o link de vídeo deixaria de
virar player), e o parágrafo vazio que o editor mantém no fim não é escrito. `visualMarkdown.test.ts`
prende a ida e volta de cada construção que uma nota pode ter.

Sublinhado fica desligado: não há markdown para ele, e voltaria como `++texto++`, que nada renderiza.
