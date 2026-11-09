# Visão em grafo

[← Voltar ao índice](README.md) · Relacionados: [Links e backlinks](links-and-backlinks.md), [Tags](tags.md)

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
- **Pages arquivadas continuam no payload da API**, com a flag; a tela global aplica sua escolha de
  visibilidade localmente. O grafo local continua incluindo vizinhas arquivadas.
- **O `Degree` vem calculado do backend**, contado **dentro do grafo devolvido** — é o que dimensiona o
  nó no desenho. A tela global recalcula esse valor após os filtros de visibilidade.
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
- Clicar num nó global abre os detalhes; o grafo local continua navegando diretamente para a página.

## 5. Filtros de visibilidade

O grafo global usa **Mostrar arquivadas** da sidebar no desktop; em telas pequenas, o mesmo controle
fica na barra do grafo. Começa **desligado** e agora afeta a sidebar e o grafo. **Ocultar notas
isoladas** fica na barra em todos os tamanhos de tela e começa **desligado**. As duas escolhas valem
durante a visita atual, independentemente da preferência salva de Mostrar grupos.

A visibilidade é aplicada após os filtros de espaço/tags da API e antes das famílias e recolhimentos.
`lib/graphVisibility.ts` remove arquivadas e seus links quando solicitado, recalcula os graus e,
opcionalmente, remove vértices sem ligações restantes. Uma nota ligada apenas a uma arquivada oculta
pode ficar isolada. Links da nota para ela mesma contam como conexão, com grau dois, igual ao backend.
Ocultar isoladas afeta o grafo, sem retirar páginas da sidebar.

O localizador por título, os detalhes, as contagens das famílias, os resumos e o enquadramento usam
esse mesmo grafo filtrado. Mudar qualquer switch limpa a seleção e fecha os detalhes. O resultado
vazio explica os filtros de visibilidade; os controles continuam disponíveis para restaurar a visão.
A busca textual compartilhada e o grafo local mantêm o comportamento anterior. É uma projeção local
imutável, sem parâmetro de API, alteração de backend ou mudança nas notas e links salvos.

## 6. Famílias por tags e regiões temáticas

No **grafo global**, abra **Configurar famílias** para criar grupos com nome, cor e uma ou mais tags
existentes. Qualquer tag selecionada serve. A primeira família compatível recebe a página; as setas
mudam a prioridade. Cada página aparece uma vez; páginas sem família ficam fora das regiões. O hover
mostra todas as famílias compatíveis, mesmo quando a prioridade define uma só região.

**Mostrar grupos** começa **desligado**. Ao ligar, aproxima as famílias em regiões com fundo suave,
contorno arredondado e rótulo, mantendo os links reais entre famílias. A legenda mostra contagens
visíveis; clicar numa família enquadra seus membros. Famílias vazias não desenham região. Os filtros
de espaço/tags estreitam o grafo antes do agrupamento. Em telas pequenas, a barra quebra linhas e
inclui o filtro de tags; rótulos ficam dentro da área visível.

A flag e a configuração ordenada persistem **por conta neste navegador**, na chave
`pandora.notes_graph.<userId>`, como o modo local de edição. Não sincronizam entre dispositivos.
Armazenamento bloqueado mantém a escolha durante a visita atual. A edição usa rascunho: Salvar aplica
e Cancelar descarta. Tags excluídas aparecem como indisponíveis e não correspondem a páginas.

O backend acrescenta `TagIds` com uma leitura em lote das tags das páginas retornadas. `lib/graphGroups.ts`
atribui famílias e fornece uma força de atração; `lib/graphRegions.ts` desenha atrás dos vértices/links
reais. Desligar o agrupamento restaura a força original dos links. Vértices e arrays de tags do cache
são copiados antes da simulação. Não há mudança de schema ou dependência nova. O grafo local da página
não usa famílias.

Isso implementa por tags a ideia registrada em 2026-11-02. Comunidades automáticas e regiões
sobrepostas continuam como ideias futuras.

## 7. Localizar uma nota

O seletor **Localizar nota no grafo** do grafo global busca títulos entre os vértices carregados,
ignorando maiúsculas e acentos. Os filtros de espaço e tags também limitam os resultados.
Selecionar um resultado centraliza a câmera nesse vértice no zoom 2 e destaca suas conexões
imediatas sem sair do mapa ou esconder outros vértices. O título selecionado permanece visível.
A seleção também abre o painel de detalhes. O hover destaca temporariamente outra vizinhança;
ao sair do vértice, volta o destaque da busca.
Limpar o seletor remove o destaque persistente e mantém a posição da câmera.
Mudar os filtros de espaço ou tags limpa a seleção e fecha os detalhes.
É uma navegação local sobre o payload do grafo, separada da busca textual de páginas.

## 8. Ideias de melhorias (2026-11-02)

São ideias, sem compromisso de implementação. Busca por título, detalhes, recolhimento de famílias, controles de navegação e filtros de visibilidade estão entregues.

- **Painel de detalhes (implementado):** ver o comportamento de inspeção abaixo.
- **Recolher famílias (implementado):** ver a projeção visual abaixo.
- **Controles de navegação (implementado):** ver os controles da câmera abaixo.
- **Filtros de visibilidade (implementado):** ver a escolha compartilhada de arquivadas e o filtro de isoladas acima.
- **Ligações entre famílias:** resumir pontes e contagens de links entre regiões temáticas.
- **Depois:** fixar posições dos vértices, lembrar o enquadramento e explorar comunidades automáticas.

Próxima prioridade sugerida: uma visão geral das pontes entre famílias.

## 9. Inspecionar uma nota sem sair do grafo

Clique num vértice global, selecione um resultado da busca ou escolha uma nota conectada no painel
para inspecionar essa nota e destacar sua vizinhança. O painel mostra título/ícone, estado de
arquivamento, tags, todas as famílias configuradas compatíveis e notas conectadas dentro dos filtros
atuais. A ordem das famílias continua definindo a região desenhada quando várias combinam. Os
metadados das famílias configuradas aparecem mesmo com Mostrar grupos desligado.

As conexões incluem links de entrada e saída, listando cada outra nota uma vez, independentemente
do tipo ou direção. **Abrir nota** é a ação explícita que navega para o editor. A sidebar continua
abrindo páginas diretamente. Fechar os detalhes mantém a seleção e o destaque; limpar a busca
ou mudar os filtros de espaço/tags fecha os detalhes e limpa a seleção.

No desktop, o painel fica ao lado do canvas, permitindo selecionar outro vértice imediatamente.
Em telas pequenas, usa uma gaveta com rolagem. Os metadados vêm dos payloads existentes do grafo e
das tags; não há consulta do conteúdo da página, alteração de backend ou persistência nova. O grafo
local da página mantém o comportamento original de abrir a nota ao clicar.

## 10. Recolher e expandir famílias

Com **Mostrar grupos** ligado, cada família não vazia na legenda tem um botão **Recolher/Expandir**.
Recolher substitui seus membros visíveis por um vértice colorido com o rótulo `Família · quantidade`.
Clicar nesse resumo expande a família; o nome na legenda continua enquadrando seus membros.
As famílias começam expandidas. Páginas sem família continuam visíveis e a prioridade mantém cada
página em uma única família.

A projeção visual oculta os links internos de uma família recolhida e direciona seus links externos
ao resumo. Links paralelos são combinados por origem, destino e tipo visíveis; direção e tracejado
dos embeds são preservados. O hover de uma ligação agregada mostra a quantidade real de links.
Contagens e membros respeitam os filtros atuais de espaço/tags; famílias vazias não geram resumo
nem entrada na legenda.

Buscar uma página escondida ou selecioná-la nas conexões do painel expande sua família antes de
localizá-la. Recolher a família da página selecionada limpa a seleção e fecha os detalhes. Vértices
de resumo expandem a família em vez de abrir detalhes de página. Busca e detalhes usam o grafo original.

O estado de recolhimento vale durante a visita atual e não é persistido. Desligar os grupos restaura
todas as páginas e links reais; ligar novamente reaplica os recolhimentos da visita. Salvar a
configuração expande todas as famílias. `lib/collapsedGraph.ts` cria dados novos para a simulação;
nenhuma API, schema, nota ou link real é alterado. O grafo local permanece igual.

## 11. Controles da câmera

Os grafos global e local têm botões **Aumentar zoom**, **Diminuir zoom** e **Enquadrar todo o grafo**
no canto superior direito. Os botões têm rótulos acessíveis, ativação pelo teclado e área de toque
de 40 px, separada da legenda das famílias. Ficam desabilitados quando não há vértices disponíveis.

O zoom altera a escala atual por um fator de 1,25, com animação curta e os limites existentes do
canvas. Enquadrar mostra todos os vértices desenhados, respeitando os filtros de espaço/tags e as
famílias recolhidas. Nenhuma dessas ações limpa a seleção, fecha os detalhes, altera as famílias
ou grava preferências. A navegação por roda do mouse/gestos continua disponível.

Uma ação manual da câmera, incluindo enquadrar uma família, tem prioridade sobre o enquadramento
automático ao terminar a simulação. Selecionar outra nota ou mudar o grafo/layout retoma o
posicionamento automático. Os controles usam a API do canvas existente, sem backend ou dependência nova.
