# Links e backlinks

[← Voltar ao índice](README.md) · Relacionados: [Grafo](graph-view.md), [Blocos e editor](blocks.md), [Modelo de dados](data-model.md)

Os links entre páginas são **derivados dos blocos**, não cadastrados à parte. São também o que o
[grafo](graph-view.md) desenha.

---

## 1. Três jeitos de ligar uma página

| No documento | Tipo da aresta | Aparece como |
|---|---|---|
| Uma **menção** inline: `{ mention: { page } }`, digitada com `@` ou `[[` | `wikilink` | O título da página, em caixa, dentro do texto |
| Um **bloco de link de página**: `page-link`, `props.pageId`, `props.look` | `embed` | Um cartão, ou uma prévia com as primeiras linhas da página |
| Uma **cópia sincronizada**: `synced-ref`, `props.pageId`, `props.blockId` | `embed` | O bloco original, somente leitura (§4) |

Uma menção pode virar cartão ou prévia, e um cartão ou prévia pode voltar a ser menção, pela seleção
ou pelo menu do bloco.

Todo link guarda o **id** do destino. Renomear nunca quebra um, e nada é resolvido pelo título. Uma
página inteira nunca é desenhada dentro de outra.

## 2. Reconstruir as arestas

O `PageContent` junta os ids das páginas mencionadas e ligadas na sua passada; o
`PageLinkSynchronizer` então compara com a `nte004_page_link`:

1. o conjunto que o documento pede, como pares `(targetId, kind)`, limitado às páginas vivas do dono;
2. as arestas guardadas que não são mais pedidas são removidas;
3. só as novas são inseridas.

Salvar um documento sem mudança não toca em nada, e a mesma linha nunca é apagada e reinserida numa
transação (o `uq_nte004_edge` recusaria).

## 3. "Mencionada em"

`GET /notes/pages/{id}/backlinks` devolve as páginas que referenciam esta:
`BacklinkDto(PageId, Title, Slug, Icon, IsArchived, Kind)`. Uma página que menciona e também embute a
aberta aparece uma vez por tipo. A página as mostra embaixo do documento, depois das subpáginas.

Uma página na lixeira mantém as arestas que apontam para ela; as leituras as filtram, e restaurar as
traz de volta. Excluir de vez remove as arestas nos dois sentidos.

## 4. Blocos sincronizados

- **O original** é um bloco `synced` cujos `children` são o conteúdo, editado onde mora.
- **"Copiar para sincronizar"** no menu dele põe o bloco na área de transferência. Colado em outra
  página, vira um `synced-ref` que mostra os blocos do original **somente leitura**, com um caminho para
  abrir o original e editar lá. Assim duas páginas nunca escrevem o mesmo bloco.
- Uma cópia conta como link do tipo `embed`, então a página do original lista a página da cópia em
  "Mencionada em".

## 5. Numa página pública

Uma página mencionada aparece como "Página privada", e um bloco de link de página ou uma cópia
sincronizada não mostra nada da outra página. Só a página do link é pública.
