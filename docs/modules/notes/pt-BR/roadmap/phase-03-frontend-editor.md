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
