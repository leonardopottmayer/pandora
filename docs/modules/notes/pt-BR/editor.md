# Editor e blocos ricos

[← Voltar ao índice](README.md) · Relacionados: [Pages e hierarquia](pages-and-hierarchy.md), [Anexos](attachments.md), [Tags](tags.md)

O editor é **100% frontend**. Tudo que ele produz é markdown comum, que o `content_markdown` já
guardava — nada aqui exigiu mudança de backend.

---

## 1. A superfície

`components/MarkdownEditor.tsx` é o CodeMirror 6 sobre o markdown cru; `components/MarkdownPreview.tsx`
renderiza com `marked` + DOMPurify. A `NotesPage` mostra os dois em um de três modos de visualização —
`edit`, `split` (o padrão) e `preview`.

O editor continua **markdown cru**: não há renderização de widget ao vivo dentro do CodeMirror. O
resultado renderizado é o que o painel de preview mostra. Ver
[Status de implementação](implementation-status.md).

## 2. Autosave

`hooks/useAutosave.ts` faz debounce de **800 ms** e salva por uma mutation do TanStack Query, expondo
um status que a página exibe. O rascunho (`PageDraft`) guarda título, ícone e corpo juntos, então os
três salvam pelo mesmo caminho — o ícone não é uma chamada à parte.

`hooks/useDebouncedValue.ts` (200 ms) é o outro debounce, usado pelo palette de busca: uma requisição
por pausa na digitação, não por tecla.

## 3. Upload inline

Colar (`Ctrl+V`) ou arrastar um arquivo faz upload para `POST /notes/attachments` e insere
`![nome](/api/v1/notes/attachments/{id})` no cursor. O rótulo é o nome original do arquivo, que é
também o que o handler de download usa depois para nomear o arquivo salvo. Ver [Anexos](attachments.md).

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
| `[[` | `wikilinkTriggerAt` em `lib/wikilinks.ts` | Abre depois de `[[` e de `![[`, que escreve o alvo igual. **Recusa** depois de `]` ou de `\|` — link fechado ou metade do alias não são mais o alvo sendo digitado. `filterPages` casa por **título e slug**, então `cafe` acha `Café com Pão`; teto de 10 opções. Ao aplicar, um `]]` já existente logo depois do cursor é **reaproveitado** em vez de duplicado — que é exatamente o estado que o comando `/wikilink` deixa. |
| `#` | `lib/tags.ts` | Completa **só tags existentes** — tag nova se digita, que é justamente o ponto de ela morar no texto. |
| `/` | `slashTriggerAt` em `lib/slashCommands.ts` | Só abre com a `/` no início da linha ou depois de espaço (então `src/lib` no meio do texto nunca dispara), e fecha ao primeiro espaço digitado. `filterCommands` casa por **id e label traduzido**. |

## 5. Slash commands

O `SLASH_COMMANDS` insere markdown puro — toda entrada é algo que o usuário poderia ter digitado à
mão, e é isso que mantém o documento round-trippable.

| Grupo | Entradas |
|---|---|
| Blocos | `h1`, `h2`, `h3`, lista com marcador, lista numerada, todo (`- [ ] `), citação, bloco de código, divisor, tabela, wikilink (`[[]]` com o cursor no meio) |
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

**Callouts colapsáveis** (`> [!note]-`) estão fora de escopo. Se entrarem, o lugar é o `callouts.ts`.

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
3. **URLs de anexo** são trocadas por object URLs **depois** do DOMPurify — a allow-list de URI dele
   não cobre `blob:`, então um object URL embutido antes seria removido. Ver
   [Anexos](attachments.md#4-a-consequência-o-browser-não-alcança-um-anexo-sozinho).

Cliques em wikilinks, chips de tag e links de anexo são todos interceptados pelo mesmo handler.
