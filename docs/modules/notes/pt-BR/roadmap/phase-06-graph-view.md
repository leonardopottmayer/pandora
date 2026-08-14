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

## Notas de implementação (fase concluída)

- **Filtro por tag não foi feito: pages não têm tags.** O plano adiou tags na Fase 01
  ("opcional no MVP") e nada as introduziu depois — não há o que filtrar. O filtro por
  profundidade está implementado. Quando/se tags entrarem no `Page`, o encaixe natural é um
  parâmetro a mais no `GetPageGraphInput`, cortando os nós antes da vizinhança.
- Backend: `GET /notes/pages/graph` (global) e `GET /notes/pages/{id}/graph?depth=N` (local),
  ambos servidos pelo mesmo `GetPageGraphQuery` — `RootPageId` nulo é o grafo inteiro.
  `depth` é clampado em 1..5; além disso o grafo local vira o global com passos extras.
- As pages do usuário e suas arestas são carregadas inteiras e a vizinhança é cortada **em
  memória** (`PageGraph.Neighborhood`, BFS não-direcionada com visited set). Um caderno pessoal
  é pequeno, e isso mantém o passeio por profundidade fora do SQL — sem CTE recursiva.
- A vizinhança ignora a direção da aresta: quem aponta para a page aberta é tão vizinho quanto
  quem ela aponta. É o que o grafo local do Obsidian mostra.
- Aresta cujo alvo não resolve mais (page deletada) é **descartada na leitura**, igual ao painel
  de backlinks — o payload nunca tem aresta apontando para nó que não existe.
- Pages arquivadas continuam no grafo, com a flag: arquivar tira da sidebar, não desliga o link.
- `Degree` (grau) vem calculado do backend, contado **dentro do grafo devolvido** — é o que
  dimensiona o nó no desenho, então tem que respeitar o mesmo corte do resto do payload.
- Frontend: `react-force-graph-2d` (canvas + d3-force), como o plano sugeriu. `GraphView` é o
  canvas puro; `LocalGraphPanel` é o Collapse ao lado do de backlinks (fechado por padrão — o
  canvas roda uma simulação, e não deve rodar embaixo de toda page que o usuário abre para ler);
  `NotesGraphPage` é a rota `/notes/graph`, declarada **antes** de `notes/:id` para não ser lida
  como id de page. Entrada: botão de grafo no topo da sidebar.
- `toForceData` copia os nós antes de entregar à simulação: ela escreve `x`/`y`/velocidades nos
  objetos que recebe, e esses objetos seriam o cache do react-query.
- Hover destaca a vizinhança imediata (o resto desbota) e o rótulo só aparece a partir de certo
  zoom — sem isso um grafo de qualquer tamanho vira borrão. Embed é desenhado tracejado.
- **Registrado (não feito): o grafo global não tem filtros próprios** (nem tag, nem "esconder
  arquivadas"). Com poucas pages não incomoda; se incomodar, o lugar é a `NotesGraphPage`.
