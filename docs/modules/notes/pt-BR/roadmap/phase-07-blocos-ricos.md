# Fase 07 — Slash commands, callouts, tabelas

> Bloco no plano: v2 §7.2.8 · Depende de: 03 · Camada: frontend

## Objetivo

Enriquecer a edição com blocos ricos sobre o editor CodeMirror, mantendo markdown como
fonte da verdade.

## Escopo

- **Slash commands** (`/`) no editor para inserir blocos.
- **Callouts** (blocos de destaque estilo Obsidian/Notion).
- **Tabelas** — inserção e edição assistida.

> Escopo aberto no plano original (§7.2.8 não detalha). Refinar os blocos exatos numa
> sessão de brainstorming antes de implementar, garantindo que tudo serialize para
> markdown padrão (fonte da verdade). **Refinado e fechado** — ver as notas no fim.

## Verify

- Slash command insere o bloco escolhido; o resultado persiste como markdown válido.
- Callouts e tabelas sobrevivem a reload (round-trip markdown sem perda).

## Fora de escopo

- Histórico de versões, templates de page, colaboração/share (marcados como "Futuro"
  no plano — §3).

## Notas de implementação (fase concluída)

Fase 100% frontend: **o backend não foi tocado**. Tudo que os blocos produzem é markdown
comum, que o `contentMarkdown` já guardava — o round-trip do *verify* é estrutural, não
depende de nada novo do lado do servidor.

### Escopo decidido (o que o plano deixou aberto)

- **Callouts**: sintaxe Obsidian, **6 tipos** — `note`, `tip`, `info`, `warning`, `danger`,
  `quote`. Colapsáveis (`> [!note]-`) ficaram **de fora**.
- **Tabelas**: inserir pelo `/` + **Tab/Shift+Tab** entre células com realinhamento das
  barras. Editor visual de tabela (widget WYSIWYG) ficou de fora — trocaria o markdown por
  um widget como texto que se edita, e o ponto da fase é o contrário.

### Callouts

- `lib/callouts.ts` é uma **extensão do `marked`** (tokenizer + renderer de nível bloco),
  não um pré-processamento de string como os wikilinks: assim o corpo do callout continua
  passando pelo lexer e `**negrito**` dentro dele funciona.
- Tipo desconhecido (`> [!frobnicate]`) o tokenizer **recusa**, e a coisa cai no blockquote
  normal do `marked`. É de propósito: a sintaxe é feita para degradar em blockquote em
  qualquer outro renderer markdown, e é isso que mantém o arquivo portável.
- Callout **sem título** cai no nome do tipo traduzido, então o parser é um `Marked` próprio
  (não o `marked` global) memoizado por `i18n.language` no `MarkdownPreview`. Mutação global
  via `marked.use()` foi evitada de propósito.
- O ícone é emoji embutido no HTML; a cor vem de um `--notes-callout-accent` por tipo, que
  serve de borda e, via `color-mix`, de fundo. Um token só por tipo.

### Slash commands

- `slashTriggerAt` só abre o menu com a `/` no início da linha ou depois de espaço — assim
  `src/lib` no meio de um texto não dispara nada — e fecha ao primeiro espaço digitado.
- A filtragem é nossa (`filterCommands`, casa por id **e** por label traduzido), e o
  `CompletionResult` vai com `filter: false`: com o filtro do CodeMirror ligado, a opção que
  casou pelo label em PT-BR seria descartada por não casar com o `label` (`/table`).
- O `t` entra por **ref**, não por dependência do efeito: trocar de idioma não reconstrói o
  editor (o que perderia o histórico de undo).
- Tooltip pendurada em `document.body` (`tooltips({ parent })`) — o editor mora dentro de um
  `Card` com `overflow: hidden`, que cortaria o menu perto do rodapé do painel.

### Tabelas

- `lib/markdownTables.ts` é tudo função pura sobre as linhas do documento; `editorCommands.ts`
  é a única parte que conhece o `EditorView`. Foi o que permitiu testar Tab de verdade contra
  um editor montado, em vez de só testar a matemática.
- Tab **reformata a tabela inteira** antes de mover, então as barras se realinham enquanto se
  digita. `formatTable` é idempotente (tem teste) — Tab repetido não fica alargando coluna.
- A largura mínima de coluna é 3 (o `---`), e os `:` de alinhamento que o usuário escreveu são
  preservados, esticando só os traços entre eles.
- Tab **recusa** (deixa o Tab valer o que valia) fora de tabela, com seleção aberta, e numa
  linha de pipes solta que ainda não é tabela — reformatar uma linha que a pessoa ainda está
  escrevendo seria pior que não fazer nada. Shift+Tab na primeira célula também recusa.
- Tab na última célula **acrescenta uma linha**; a linha separadora é pulada nos dois sentidos.
- Nenhum atalho de "remover linha/coluna": é markdown, apaga-se a linha.

### Registrado (não feito)

- **Não há preview ao vivo dos blocos dentro do CodeMirror** — o editor continua markdown cru,
  o resultado se vê no painel de preview (ou no modo split). Widgets no editor seriam outra
  fase inteira.
- **Callout colapsável** e **alinhamento de coluna pela UI** ficaram fora; se entrarem, o lugar
  é `callouts.ts` (variante `-`/`+`) e `markdownTables.ts` (mexer só na linha separadora).
