# Fase 06 — Graph view (global + local)

> Bloco no plano: v2 §7.2.7 · Depende de: 04 · Camada: frontend (+ query no backend)

## Objetivo

Visualizar a rede de pages: nós = pages, arestas = `PageLink` (já materializadas na
Fase 04). Como os dados já existem, o grafo é essencialmente uma **visualização**.

## Escopo

### Backend

- Endpoint que devolve nós + arestas para o grafo (global, e vizinhança de uma page para
  o local graph), com filtros por tag e profundidade.

### Frontend

- Lib candidata: `react-force-graph` (d3-force) — leve e suficiente para volume pessoal.
- Modos:
  - **Grafo global** — toda a rede.
  - **Local graph** — vizinhança da page atual (estilo Obsidian).
- Interação: clique navega; hover destaca a vizinhança.
- Filtros por tag e profundidade.

## Verify

- Nós/arestas batem com o conteúdo de `PageLink`.
- Clicar num nó navega para a page.
- Local graph mostra só a vizinhança da page atual, respeitando a profundidade.

## Fora de escopo

- Blocos ricos / slash commands (Fase 07).
