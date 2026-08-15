# Fase 03 — Frontend: sidebar em árvore + editor CodeMirror + autosave

> Bloco no plano: MVP §7.1.4 · Depende de: 01, 02 · Camada: frontend

## Objetivo

Primeira UI utilizável do módulo em `client-web/src/modules/notes`: navegar a árvore de
pages, editar markdown em CodeMirror 6 com live preview e autosave, e subir imagem inline.

## Escopo

### Sidebar (árvore)

- Renderiza a árvore vinda de `GET /api/notes/pages` (query de árvore da Fase 01).
- Expandir/colapsar, drag-and-drop para reordenar e reparentar (chama os endpoints de
  mover/reordenar).
- Ações por page: criar filha, favoritar, arquivar, deletar.

### Editor (CodeMirror 6 — Obsidian-like)

- Markdown cru com live preview (`#`, `-`, `**`, ` ``` `, etc.).
- **Upload inline**: colar (Ctrl+V) ou arrastar arquivo → `POST /api/notes/attachments`
  → insere `![](/api/notes/attachments/{id})`.
- **Autosave** com debounce via mutation do TanStack Query.

### Infra frontend

- Rotas/entrada do módulo em `client-web/src/modules/notes`.
- i18n EN + PT-BR das strings novas.

## Verify

- Criar page, escrever markdown, colar uma imagem, **recarregar** e tudo persistir.
- Reparentar via drag-and-drop reflete no backend.
- Imagem embedada renderiza servida pelo endpoint autenticado da Fase 02.

## Fora de escopo

- Wikilinks/backlinks (Fase 04), busca (Fase 05), grafo e blocos ricos (v2).

## Correção posterior — anexos que o browser não alcança (2026-08-15)

Testando o módulo apareceram dois bugs com a mesma raiz: o endpoint de anexo é **autenticado**, e o
token mora no localStorage, não em cookie — então nem uma navegação do browser nem um `<img src>`
chegam nele sozinhos. Os dois caminhos que dependiam disso tratavam a questão pela metade.

- **Link de anexo não baixava.** O markdown guarda um caminho relativo, então clicar na âncora
  navegava a aba atual para `/api/...` **na origem do frontend** (que em dev é o Vite, sem proxy — daí
  a página em branco; em produção seria o nginx e um 401). Agora o clique é interceptado no mesmo
  handler que já cuidava de wikilink e tag: busca o blob pelo `apiClient` e entrega a um `<a download>`
  descartável. Sem aba nova, sem navegação. O nome do arquivo vem do rótulo do markdown, que é o nome
  original — é isso que o editor escreve ao embedar.
- **Imagem sumia do preview.** O object URL era escrito direto no nó do DOM (`img.src = ...`), fora do
  modelo do React. Qualquer re-render que **não mudasse o markdown** reescrevia o HTML e restaurava o
  caminho original, e o efeito que buscaria de novo dependia só de `[html]`, que não tinha mudado —
  imagem quebrada até a próxima tecla digitada. Trocar de tema, trocar de idioma e qualquer refetch
  que recriasse `pageIndex`/`tagIndex` (autosave, foco na janela) faziam isso.
  - Agora `useAttachmentUrls` resolve os anexos **antes** de renderizar e a URL faz parte do `html`.
    O `combine` do `useQueries` memoiza o mapa, então a identidade só muda quando os resultados mudam.
  - A substituição acontece **depois** do DOMPurify: a allow-list de URI dele não cobre `blob:`, e um
    object URL embutido antes seria removido.
  - De brinde, cada anexo é baixado **uma vez por sessão** em vez de a cada tecla digitada — o efeito
    antigo rebuscava todas as imagens a cada mudança do markdown.
  - Ninguém revoga o object URL: um id endereça bytes imutáveis, então existe no máximo um por anexo
    por sessão, e o reload que encerra a sessão os libera.

O teste de regressão é o que reproduz o bug: renderiza, espera o `blob:`, e re-renderiza com
`pageIndex`/`tagIndex` novos — exatamente o que um refetch produz.
