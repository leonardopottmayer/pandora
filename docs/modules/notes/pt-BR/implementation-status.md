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
| **Ícone emoji** | Picker ao lado do título, reduzido ao primeiro grafema via `Intl.Segmenter`, salvo pelo autosave da própria page. |
| **Anexos** | `IFileStorage` + `DatabaseFileStorage`, tabela de blob, upload/download autenticados, colar/arrastar inline no editor, resolução por object URL no preview (`nte002`, `nte003`). |
| **Editor** | CodeMirror 6, markdown cru + preview + modo split, autosave de 800 ms, sanitização com DOMPurify. |
| **Blocos ricos** | Slash commands (11 blocos + 6 callouts), callouts Obsidian como extensão do `marked`, tabelas markdown assistidas com Tab/Shift+Tab e reformatação idempotente. |
| **Autocomplete** | Três menus — `[[` (pages por título e slug), `#` (tags existentes), `/` (comandos) — compartilhando `filter: false`, entradas por ref e tooltip pendurada no body. |
| **Wikilinks e backlinks** | `WikilinkParser`, resolução título-depois-slug, tipos `wikilink`/`embed`, reconstrução por diff no create e no update, endpoint e painel de backlinks, create-on-click (`nte004`). |
| **Busca** | Coluna gerada `tsvector` + índice GIN, configuração `simple`, tradução para `tsquery` com pontuação descartada, excerpt de 160 chars, command palette no Ctrl+K **e** botão na sidebar. |
| **Grafo** | Grafo global e local a partir de uma query, BFS não-direcionada em memória com profundidade clampada em 1..5, grau calculado no backend, arestas quebradas filtradas, canvas `react-force-graph-2d` com destaque no hover e rótulos por zoom. |
| **Tags** | `TagParser` removendo código, normalização por `TagName`, criação de tag a partir do texto, arestas por diff, varredura de órfãs preservando tags coloridas, validação hex da cor, filtros por interseção na sidebar / busca / grafo, chips no preview (`nte005`, `nte006`). |
| **Frontend** | Módulo React (`client-web/src/modules/notes`) cobrindo tudo acima, i18n EN + PT-BR, testes unitários em todo módulo de `lib/`. |
| **Testes** | Unitários de domínio (`Modules.Notes.Tests`: page, hierarquia, grafo, busca, slugger, tag name, tag parser, wikilink parser) + integração (`IntegrationTests/Modules/Notes`: pages, anexos, backlinks, busca, grafo, tags). |

## Ainda não implementado (previsto / planejado)

| Área | Status |
|---|---|
| **Ranking e realce da busca** | Previsto como v2 no plano de produto. Os resultados são ordenados por título, não por `ts_rank`, e o `PageSearchResultDto.Excerpt` é uma fatia crua — sem `ts_headline`. |
| **Preview ao vivo dos blocos dentro do CodeMirror** | Não implementado. O editor continua markdown cru; o resultado se vê no painel de preview. Widget decorations dentro do editor seriam uma fase inteira. |
| **Renderização inline de `![[embeds]]`** | Não implementado. O tipo de aresta `embed` existe de ponta a ponta (guardado, tracejado no grafo, listado nos backlinks), mas o preview renderiza embed como link comum. |
| **Filtros de tag na URL** | Não implementado. Os filtros são estado de componente, então "as pages com #x" não é um link compartilhável. O lugar seria um search param na `NotesPage`. |
| **Toggle de arquivadas no grafo** | Não implementado. O `includeArchived` da `NotesGraphPage` só afeta a sidebar; o grafo sempre inclui as arquivadas (com a flag). |
| **Callouts colapsáveis** (`> [!note]-`) | Deliberadamente fora do escopo de blocos ricos. Se entrar, o lugar é a variante `-`/`+` em `lib/callouts.ts`. |
| **Alinhamento de coluna pela UI** | Deliberadamente fora. O lugar seria a linha separadora em `lib/markdownTables.ts`. |
| **Editor visual de tabela** (widget WYSIWYG) | Rejeitado: trocaria o markdown por um widget renderizado como texto editável, o oposto da premissa do módulo. |
| **Backend S3/MinIO** | Só a abstração existe. `storage_backend` + `storage_key` em cada anexo fazem com que plugá-lo não exija migration; o gatilho natural são uploads grandes. |
| **Histórico de versões, templates de page, share/colaboração** | Futuro no plano de produto; nada iniciado. |

## Deliberadamente fora (não é dívida)

- **Rename global de tag** — exigiria reescrever o markdown de toda page que a menciona; é find &
  replace, não operação de banco.
- **Rollup de tag aninhada** — `#projeto/pandora` é aceito como texto da tag, mas `#projeto` não
  inclui as filhas.
- **Endpoints de criar/deletar tag** — o texto cria a tag, a varredura remove.
- **`ON DELETE CASCADE` na árvore de pages** — o comando de delete percorre a subárvore por conta
  própria para o soft delete manter a história.

## Pontos em aberto conhecidos

1. **Retenção dos blobs.** Os bytes dos anexos moram em `bytea`; revisitar mover para object storage
   se o caderno começar a carregar arquivos pesados — é também o gatilho do backend S3.
2. **Anexos órfãos.** Nada apaga o blob quando a page que o embedava é deletada, e o `page_id` não tem
   FK por design. Uma varredura precisaria de uma decisão sobre o que significa "sem referência" quando
   a única referência é uma string de markdown.
3. **Passeio do grafo em memória.** Carregar o conjunto inteiro de pages/arestas do usuário é o certo
   para um caderno pessoal; é a primeira coisa a revisitar se o grafo crescer.
4. **Parsers duplicados.** `lib/wikilinks.ts` e `lib/tags.ts` espelham o backend de propósito, para o
   preview resolver o que o save vai resolver. Qualquer mudança numa regra de parse tem que pousar nos
   dois lados — os testes unitários de cada lado são a proteção.
