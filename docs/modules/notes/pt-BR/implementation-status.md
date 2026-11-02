# Status de implementação

[← Voltar ao índice](README.md)

Uma fotografia do que está construído no código versus o que está previsto mas ainda não
implementado. Serve para separar "documentado porque existe" de "documentado como plano".

O módulo foi construído em nove fases sequenciais (scaffold → pages → anexos → editor → wikilinks →
busca → grafo → blocos ricos → tags), todas **fechadas**. O que segue é o estado que elas deixaram,
não o log das fases.

---

## Implementado

| Área | Notas |
|---|---|
| **Scaffold do módulo** | Todos os projetos por camada, schema `notes`, registrado no Host. Sem jobs, sem eventos de integração, sem event log de auditoria. |
| **Pages** | Aggregate `Page`, árvore com rejeição de ciclo no reparent, slugs congelados com sufixo em colisão, mover/reordenar, favoritar, arquivar, soft delete da subárvore inteira (`nte001`). |
| **Espaços** | Aggregate `Space`, um padrão por usuário (criado na hora quando não há), pages num espaço, mover entre espaços levando a subárvore, wikilinks preferindo o próprio espaço, filtro de espaço na árvore / busca / grafo, tela de espaços e seletor na sidebar, `create_note` aceitando espaço (`nte007`). |
| **Ícone emoji** | Picker ao lado do título, reduzido ao primeiro grafema via `Intl.Segmenter`, salvo pelo autosave da própria page. |
| **Biblioteca de mídia** | Itens (imagem / arquivo; vídeo no schema, recusado no upload) em pastas virtuais, miniaturas WebP com ImageSharp e rotação EXIF, mover pastas sem ciclo, pasta excluída subindo o conteúdo, soft delete mantendo os bytes servidos, cache imutável, a tela da biblioteca, colar/arrastar no editor para "Não organizadas" (`nte002`, `nte003`, `nte008`, `nte009`). |
| **Mídia dentro das notas** | Seletor da biblioteca por `/imagem`, `/arquivo` e pela barra, largura, alinhamento e legenda pelo texto alternativo (`![texto|400|direita]`), `<img>` da biblioteca em HTML, zoom em tela cheia, cartões de arquivo com tamanho, quais notas usam cada item derivado no save (`nte010`), "Usado em" na biblioteca, lugar "Sem uso", aviso antes de excluir um item em uso. |
| **Editor** | Editor visual estilo Notion (TipTap 3, salvando markdown: menus `/` `[[` `#`, barra da seleção, barra da tabela, alça de arrastar, callouts e blocos recolhíveis nativos com corpo e título editáveis, player sob o link de vídeo; HTML não suportado preservado como bloco cru) como padrão; CodeMirror 6 com markdown cru, preview e modo split ao lado; autosave de 800 ms, sanitização com DOMPurify. |
| **Blocos ricos** | Slash commands (14 blocos + 6 callouts), callouts Obsidian como extensão do `marked` (recolhíveis com `-`/`+`), blocos recolhíveis (`<details>`), player de YouTube/Vimeo a partir do link sozinho e allowlist de iframe (YouTube, Vimeo, Google Maps) com sandbox fixo, tabelas markdown assistidas com Tab/Shift+Tab e reformatação idempotente. |
| **Autocomplete** | Três menus — `[[` (pages por título e slug), `#` (tags existentes), `/` (comandos) — compartilhando `filter: false`, entradas por ref e tooltip pendurada no body. |
| **Wikilinks e backlinks** | `WikilinkParser`, resolução título-depois-slug, tipos `wikilink`/`embed`, reconstrução por diff no create e no update, endpoint e painel de backlinks, create-on-click (`nte004`). |
| **Busca** | `tsvector` gerado + GIN, configuração `simple`, `tsquery` seguro por prefixos, prioridade de título e relevância por `ts_rank_cd` com desempate estável, filtros antes do limite de 20, trecho de 160 caracteres com destaque seguro no título/trecho, palette no Ctrl+K e botão na sidebar. |
| **Grafo** | Grafo global e local a partir de uma query, BFS não-direcionada em memória com profundidade clampada em 1..5, grau calculado no backend, arestas quebradas filtradas, canvas `react-force-graph-2d` com destaque no hover e rótulos por zoom. |
| **Tags** | `TagParser` removendo código, normalização por `TagName`, criação de tag a partir do texto, arestas por diff, varredura de órfãs preservando tags coloridas, validação hex da cor, filtros por interseção na sidebar / busca / grafo, chips no preview (`nte005`, `nte006`). |
| **Famílias no grafo** | Regiões com nome/cor por tags escolhidas, prioridade ordenada sem duplicar páginas, contagens e enquadramento por família. Flag global desligada por padrão; modo e configuração lembrados por conta/navegador. Ver [Visão em grafo](graph-view.md#6-famílias-por-tags-e-regiões-temáticas). |
| **Famílias recolhidas no grafo** | Controles por visita substituem os membros por um resumo e agregam links externos por direção/tipo. A busca revela páginas escondidas automaticamente e o grafo original é preservado. Ver [Recolher famílias](graph-view.md#10-recolher-e-expandir-famílias). |
| **Controles da câmera do grafo** | Botões acessíveis de aumentar/diminuir zoom e enquadrar tudo nos grafos global/local, inclusive no celular; enquadramento manual tem prioridade ao terminar a simulação. Ver [Controles da câmera](graph-view.md#11-controles-da-câmera). |
| **Visibilidade do grafo** | Mostrar arquivadas controla sidebar e grafo global (desligado por padrão); Ocultar isoladas remove vértices sem links restantes. Busca, detalhes, contagens e enquadramento usam a mesma projeção filtrada, com graus recalculados. Escolhas por visita; sem alteração de API. Ver [Filtros de visibilidade](graph-view.md#5-filtros-de-visibilidade). |
| **Inspeção no grafo** | Busca por título e seleção de vértices destacam a vizinhança e mostram um painel responsivo com tags, famílias e notas conectadas sem duplicação. A ação Abrir nota navega para o editor. Ver [Localizar uma nota](graph-view.md#7-localizar-uma-nota) e [Inspecionar uma nota](graph-view.md#9-inspecionar-uma-nota-sem-sair-do-grafo). |
| **Frontend** | Módulo React (`client-web/src/modules/notes`) cobrindo tudo acima, i18n EN + PT-BR, testes unitários em todo módulo de `lib/`. |
| **Testes** | Unitários de domínio (`Modules.Notes.Tests`: page, hierarquia, grafo, busca, slugger, tag name, tag parser, wikilink parser) + integração (`IntegrationTests/Modules/Notes`: pages, anexos, backlinks, busca, grafo, tags). |

## Ainda não implementado (previsto / planejado)

| Área | Status |
|---|---|
| **Filtros de tag na URL** | Não implementado. Os filtros são estado de componente, então "as pages com #x" não é um link compartilhável. O lugar seria um search param na `NotesPage`. |
| **Ideias de navegação no grafo** | Visão geral das pontes entre famílias e, depois, persistência do layout/comunidades automáticas. Ver [Ideias para o grafo](graph-view.md#8-ideias-de-melhorias-2026-11-02). |
| **Alinhamento de coluna pela UI** | Deliberadamente fora. O lugar seria a linha separadora em `lib/markdownTables.ts`. |
| **Cartão de arquivo no editor visual** | Planejado. Um arquivo da biblioteca aparece como link comum no visual; o cartão com nome e tamanho só aparece no modo leitura. |
| **HTML inline no editor visual** | Planejado. `<kbd>`, `<span style>` e afins viram texto puro quando o parágrafo é editado no visual (`<img>` inline já vira `![|largura](…)`). |
| **Espaço ocupado na biblioteca** | Planejado. O tamanho de cada item já é salvo (`size_bytes`) e aparece no cartão e nos detalhes; falta o total na tela da biblioteca. |
| **Backend de bucket e vídeo** | Previsto, sem data: não entra na fila atual. Só a abstração existe. `storage_backend` + `storage_key` em cada linha de mídia fazem com que plugar um bucket não exija migration; vídeo pede também download por pedaços e a versão de capa. Ver [Biblioteca de mídia](media-library.md#3-tipos-e-o-que-fica-pronto-para-vídeo). |
| **Histórico de versões** | Implementado — ver [Histórico de versões](version-history.md). |

## Deliberadamente fora (não é dívida)

- **Renderizar `![[embeds]]` inline** — decidido contra em 2026-11-02: uma page nunca é desenhada dentro
  de outra. `![[x]]` continua link; o tipo de aresta `embed` continua existindo (menções, tracejado no grafo).
- **Capa, sumário, templates de page e link público/compartilhamento** — fora por enquanto (decidido em
  2026-11-02). Saíram da lista da reforma; voltam só se o usuário pedir.
- **Preview ao vivo dentro do CodeMirror** — substituído pelo editor visual; o CodeMirror fica como modo
  Markdown cru.

- **Rename global de tag** — exigiria reescrever o markdown de toda page que a menciona; é find &
  replace, não operação de banco.
- **Rollup de tag aninhada** — `#projeto/pandora` é aceito como texto da tag, mas `#projeto` não
  inclui as filhas.
- **Endpoints de criar/deletar tag** — o texto cria a tag, a varredura remove.
- **`ON DELETE CASCADE` na árvore de pages** — o comando de delete percorre a subárvore por conta
  própria para o soft delete manter a história.

## Histórico de versões

Implementado: snapshots completos e imutáveis de cada save com alterações, sem expiração.
Agrupamento apenas visual, comparação de qualquer par (Markdown e metadados), prévia e restauração
reversível com validação de dono, hierarquia e concorrência. Páginas existentes recebem uma versão
inicial; soft delete preserva o histórico. Ver [Histórico de versões](version-history.md).

## Pontos em aberto conhecidos

1. **Retenção dos blobs.** Os bytes da mídia moram em `bytea`; revisitar mover para object storage
   se o caderno começar a carregar arquivos pesados — é também o gatilho do backend S3.
2. **Bytes sem uso.** Excluir um item mantém os bytes, porque notas podem ainda mostrá-lo; nada varre os
   bytes de um item excluído que nenhuma nota usa mais. Fica assim por decisão (2026-11-02): o lugar
   "Sem uso" da biblioteca já mostra esses itens, e quem decide apagar é o usuário.
3. **Passeio do grafo em memória.** Carregar o conjunto inteiro de pages/arestas do usuário é o certo
   para um caderno pessoal; é a primeira coisa a revisitar se o grafo crescer.
4. **Parsers duplicados.** `lib/wikilinks.ts` e `lib/tags.ts` espelham o backend de propósito, para o
   preview resolver o que o save vai resolver. Qualquer mudança numa regra de parse tem que pousar nos
   dois lados — os testes unitários de cada lado são a proteção.
