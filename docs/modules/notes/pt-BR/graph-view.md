# Visão em grafo

[← Voltar ao índice](README.md) · Relacionados: [Wikilinks e backlinks](wikilinks-and-backlinks.md), [Tags](tags.md)

Nós são pages, arestas são `PageLink`. Como os dados já existem, o grafo é essencialmente uma
**visualização** — o trabalho de backend é uma query.

---

## 1. Os dois modos

| Modo | Rota | Comportamento |
|---|---|---|
| **Global** | `GET /notes/pages/graph` | A rede inteira. |
| **Local** | `GET /notes/pages/{id}/graph?depth=N` | A vizinhança de uma page, estilo Obsidian. |

Os dois são servidos pelo mesmo `GetPageGraphQuery`: `RootPageId` nulo *é* o grafo global. O `depth` é
clampado em **1..5** — além disso o grafo local vira o global com passos extras.

## 2. Como a vizinhança é calculada

As pages do usuário e as arestas delas são carregadas inteiras, e a vizinhança é cortada **em memória**
pelo `PageGraph.Neighborhood` — um passeio em largura com visited set. Um caderno pessoal é pequeno, e
isso mantém o passeio por profundidade fora do SQL, sem CTE recursiva.

O passeio **ignora a direção da aresta**: quem aponta para a page aberta é tão vizinho quanto quem ela
aponta. É o que o grafo local do Obsidian mostra.

## 3. Regras do payload

- Aresta cujo alvo não resolve mais (page deletada) é **descartada na leitura**, igual ao painel de
  backlinks. Toda ponta de aresta em `Edges` tem garantia de estar em `Nodes`, então o frontend nunca
  desenha aresta para o nada.
- **Pages arquivadas continuam no grafo**, com a flag: arquivar tira da sidebar, não desliga o link.
- **O `Degree` vem calculado do backend**, contado **dentro do grafo devolvido** — é o que dimensiona o
  nó no desenho, então tem que respeitar o mesmo corte do resto do payload.
- Uma page que linka e embeda outra rende duas arestas, uma por tipo.
- `tagIds` corta os nós **antes** do passeio pela vizinhança, então a profundidade é contada sobre o
  que sobrou.

## 4. No frontend

`react-force-graph-2d` (canvas + d3-force), dividido em três:

| Peça | Papel |
|---|---|
| `components/GraphView.tsx` | O canvas puro. |
| `components/LocalGraphPanel.tsx` | O Collapse ao lado do painel de backlinks — **fechado por padrão**, porque o canvas roda uma simulação e ela não deve rodar embaixo de toda page que o usuário abre para ler. |
| `pages/NotesGraphPage.tsx` | A rota `/notes/graph`, declarada **antes** de `notes/:id` para não ser lida como id de page. Entrada: o botão de grafo no topo da sidebar. |

- `lib/graphData.ts` (`toForceData`) **copia os nós** antes de entregá-los à simulação: ela escreve
  `x`/`y`/velocidades nos objetos que recebe, e esses objetos seriam o cache do react-query.
- Hover destaca a vizinhança imediata e desbota o resto; os rótulos só aparecem a partir de certo zoom
  — sem isso um grafo de qualquer tamanho vira borrão. Embed é desenhado tracejado.
- Clicar num nó navega para a page dele.

## 5. Não implementado

O grafo global **não tem toggle de arquivadas próprio**. O estado `includeArchived` da
`NotesGraphPage` só afeta a sidebar; a query do grafo nunca o recebe, então as arquivadas são sempre
desenhadas. Se começar a incomodar, o lugar é a `NotesGraphPage` mais um parâmetro em
`GetPageGraphInput`. Ver [Status de implementação](implementation-status.md).
