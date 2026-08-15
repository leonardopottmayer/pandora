# Visão geral — Produto e princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

O módulo **Notes** é uma base de conhecimento pessoal dentro do monolito modular Pandora. Ele permite
a um único usuário:

- Escrever **pages** em markdown, organizadas em **árvore** (pai/filho) exibida numa sidebar.
- Reordenar e reparentar por drag-and-drop; **favoritar**, **arquivar** e **deletar** (soft).
- Dar à page um **ícone emoji**, editado ao lado do título.
- **Subir anexos** — colar uma imagem, arrastar um zip ou um PDF — servidos de volta por um endpoint
  autenticado e embedados inline no markdown.
- Conectar pages com `[[wikilinks]]` e `![[embeds]]`, e ler os **backlinks** ("linked mentions") da
  page aberta.
- Rotular pages com `#tags` escritas **no texto**, e então filtrar a sidebar, a busca e o grafo por
  elas (pintando-as com uma cor de quebra).
- Achar qualquer coisa por título ou corpo com **busca full-text**, por `Ctrl+K` ou por um botão.
- Ver a rede inteira como um **grafo**, ou só a vizinhança da page aberta.
- Editar com slash commands, callouts e **tabelas** markdown assistidas, mais autocomplete de `[[`,
  `#` e `/`.

## 2. Princípios centrais

1. **Markdown é a fonte da verdade.** Wikilinks e tags são digitados no corpo, não escolhidos num
   formulário. Um `.md` exportado mantém seus links e rótulos; um `.md` escrito em outro lugar e
   colado aqui funciona igual. *(Decisão de design D1.)*
2. **Dado derivado é reconstruído, nunca editado.** As arestas `PageLink` e as linhas `PageTag` são
   recalculadas do corpo a cada save e reconciliadas por diff — salvar o mesmo texto duas vezes não
   muda nada. *(D2.)*
3. **Hierarquia ≠ grafo.** A árvore (`parent_id`) é o sistema de arquivo e proíbe ciclos; o grafo
   (`PageLink`) é a rede de significado e os espera. Dois sistemas independentes sobre uma `Page`.
   *(D3.)*
4. **Nada é realmente apagado.** Deletar é soft delete da page e de toda a sua subárvore; uma page
   soft-deletada mantém a linha e as arestas que apontam para ela, que as leituras filtram.
5. **O banco só guarda o que o texto não guarda.** A cor de uma tag, o vetor de busca, as arestas
   materializadas — todo o resto o markdown já diz.

## 3. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Page** | Um documento markdown do usuário, e o aggregate root do módulo. Tem título, ícone emoji opcional, corpo, pai e posição entre as irmãs. |
| **Árvore / hierarquia** | A estrutura pai/filho da sidebar, sustentada só por `parent_id`. Ciclos são rejeitados no reparent. |
| **Slug** | Derivação do título amigável a link, única por usuário entre as pages vivas. Fixado na criação, então um link sobrevive a um rename. |
| **Wikilink** | Uma referência `[[Alvo]]` no corpo. `[[Alvo\|apelido]]` exibe o apelido; `![[Alvo]]` é um **embed**. |
| **PageLink (aresta)** | O fato materializado de que uma page referencia outra, com `kind` `wikilink` ou `embed`. Derivado do corpo da source. |
| **Backlink / linked mention** | A leitura inversa da aresta: as pages que referenciam a que está aberta. |
| **Link quebrado** | Um `[[Alvo]]` que não casa com page nenhuma. Existe só no texto — nenhuma aresta é criada. Clicar oferece **create-on-click**. |
| **Tag** | Um `#rótulo` escrito no corpo. O **slug** é a identidade por usuário (`#Café` e `#cafe` são uma tag só); o **nome** é como foi escrita primeiro; a **cor** é a única coisa que a linha acrescenta ao texto. |
| **PageTag (aresta)** | O fato materializado de que uma page carrega uma tag. Derivado do corpo, exatamente como uma aresta. |
| **Varredura de órfãs** | A passada no fim do save que apaga as tags que nenhuma page menciona mais — a menos que tenham cor. |
| **Anexo** | Os metadados de um arquivo subido mais o par `(storage_backend, storage_key)` que localiza os bytes. Write-once. |
| **File blob** | Os bytes em si; no MVP uma linha `bytea` em `nte003_file_blob` — o único backend real de `IFileStorage`. |
| **Grafo local** | A vizinhança da page aberta dentro de N saltos, com as arestas percorridas nos dois sentidos. |
| **Grau (degree)** | Quantas arestas tocam um nó *dentro do grafo devolvido* — é o que dimensiona o nó no desenho. |
| **Callout** | Um bloco de destaque em sintaxe Obsidian (`> [!note] Título`). Degrada para blockquote comum em qualquer outro renderer markdown. |

## 4. Escopo

### Dentro (implementado — ver [Status de implementação](implementation-status.md))

CRUD de pages com árvore, slugs, mover/reordenar, favoritar, arquivar e soft delete; anexos com a
abstração `IFileStorage` e backend em banco; o editor CodeMirror com autosave, live preview, upload
inline, slash commands, callouts e edição de tabelas; wikilinks, embeds e backlinks; tags com cores e
filtros por interseção em três superfícies; busca full-text com command palette; grafo global e local.

### Fora / futuro

| Capacidade | Status |
|---|---|
| **Ranking e realce da busca** | Planejado (v2). Os resultados vêm ordenados por título e o excerpt é uma fatia crua — sem `ts_rank`, sem `ts_headline`. |
| **Preview ao vivo dos blocos dentro do CodeMirror** | Não implementado. O editor continua markdown cru; a renderização acontece no painel de preview. Seria uma fase inteira (widget decorations). |
| **Renderização inline de `![[embeds]]`** | Não implementado. O tipo de aresta existe e é desenhado tracejado no grafo, mas o preview renderiza embed como link comum. |
| **Filtros na URL** | Não implementado. Os filtros de tag são estado de componente, então não existe link compartilhável de "as pages com #x". |
| **Esconder arquivadas no grafo** | Não implementado. O toggle de arquivadas só afeta a sidebar; o grafo sempre inclui as arquivadas (com a flag). |
| **Callout colapsável** (`> [!note]-`) e **alinhamento de coluna pela UI** | Deliberadamente fora. |
| **Rename global de tag** | Deliberadamente fora — exigiria reescrever o markdown de toda page que a menciona. |
| **Rollup de tag aninhada** (`#projeto` incluindo `#projeto/pandora`) | Fora. A barra é mantida como parte do texto da tag, nada além disso. |
| **Backend S3/MinIO** | Previsto, não implementado. `IFileStorage` + as colunas auto-descritivas `storage_backend`/`storage_key` fazem com que ligá-lo não exija migration. |
| **Histórico de versões, templates de page, share/colaboração** | Futuro. |
| **Event log de auditoria** | Não faz parte deste módulo. Pages e tags têm `created_by/at` + `updated_by/at`; não existe um event log como o `fin016` do Finances. |
