# Busca

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Tags](tags.md), [Referência de API](api-reference.md)

---

## 1. O vetor

A busca full-text roda sobre uma **coluna gerada `STORED`** em `nte001_page`:

```sql
search_vector tsvector GENERATED ALWAYS AS (
    to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_text, ''))
) STORED
```

com um índice **GIN** sobre ela. O `content_text` é derivado dos blocos a cada salvamento (`PageContent`): as palavras de todo inline, células de tabela, legendas, código, propriedades da página e a saída aceita dos blocos de IA. Chips contam como suas palavras: uma data como o valor, um status ou item como o rótulo, uma fórmula como o LaTeX.

Sendo o Postgres quem mantém o vetor, nenhum caminho de save pode
esquecer de atualizá-lo — que é exatamente o motivo de ser coluna gerada em vez de algo que a
aplicação escreve.

A configuração é **`simple`**: só lower-case, sem stemming, sem adivinhar idioma. O caderno mistura
português e inglês, e escolher uma língua faria o outro idioma casar pior.

No EF a coluna é **shadow property** (`PageColumns.SearchVector`) — o aggregate `Page` não sabe que o
vetor existe. A query chega nele por `EF.Property<NpgsqlTsVector>(…).Matches(…)`.

## 2. Traduzindo o que o usuário digitou

O `PageSearch` (Domain) transforma o termo num `tsquery`: cada palavra vira `palavra:*`, unidas por
`&`. Ou seja, toda palavra tem que estar presente, e cada uma é prefixo — um palette casa enquanto
ainda está sendo digitado.

Pontuação é **descartada, não escapada**: nada digitado pode virar sintaxe de `tsquery`. Quando não
sobra nada pesquisável, o resultado é string vazia e o chamador responde sem resultados em vez de
consultar.

O mesmo tipo corta o **excerpt**: 160 caracteres em volta do primeiro prefixo de palavra encontrado, guardando ~30
caracteres de contexto antes do match, com reticências marcando o corte. Um hit só de título cai no
começo do corpo.

## 3. O endpoint

`GET /notes/pages/search?q=…&tagIds=…&spaceId=…`

- Teto de **20 resultados**: correspondências no título primeiro, depois `ts_rank_cd` decrescente no
  vetor salvo (considera repetições e proximidade), e título e id para desempates estáveis.
- Pages **arquivadas** aparecem, com a flag; soft-deletadas nunca.
- Filtros de dono, espaço e interseção de tags entram **antes** do ranking e do limite. Não há mais
  uma janela intermediária de 200 resultados que possa esconder uma página com a tag procurada.
- Com tags e **`q` vazio**, lista as pages daquelas tags: é assim que se navega uma tag.

`PageSearchResultDto(Id, SpaceId, Title, Slug, Icon, IsArchived, Excerpt)` é o mínimo que o palette precisa
para desenhar uma linha e abri-la.

## 4. O command palette

`components/SearchPalette.tsx`:

- **Ctrl+K** (Cmd+K no Mac), registrado em fase de *capture* para ganhar do editor.
- O termo tem debounce de 200 ms; setas / Enter / Esc guiam a lista.
- O palette é do módulo Notes, não do `AppLayout` — ele só busca pages.
- O estado `open` mora **na página**, não no palette (`open` / `onOpenChange`), porque o botão de lupa
  da sidebar abre o mesmo modal. O listener de teclado continua dentro do palette, de quem ele é. Nada
  da busca em si foi duplicado.
- A lupa fica no topo da sidebar, ao lado dos botões de grafo e nova page, e o `title` dela mostra
  `Ctrl+K` — então o botão também ensina o atalho. A `NotesGraphPage` monta a mesma sidebar, então
  monta o palette também; senão o botão ficaria morto numa das duas rotas. De brinde, Ctrl+K funciona
  na rota do grafo.

## 5. Realce

`SearchHighlight` destaca palavras inteiras que começam com um prefixo da consulta no título e no
trecho, sem diferenciar maiúsculas, usando letras/números Unicode e pontuação como separador.
Acentos continuam significativos, como na busca `simple` existente; não há stemming ou busca aproximada.

A API mantém `Excerpt` como **texto puro**, também utilizável pelo assistente. O palette cria texto
React e nós `<mark>` com tokens do tema, sem inserir HTML de `ts_headline`. HTML literal continua texto. Enquanto resultados anteriores ficam visíveis durante a próxima
consulta, o realce é suspenso até chegarem os novos resultados. Não há mudança de schema.

## 6. Pergunte às suas notas

Uma aba ao lado da busca responde a uma pergunta a partir das notas em vez de listar páginas. Ela usa o
mesmo vetor com uma consulta OU; ver [IA §3](ai.md#3-pergunte-às-suas-notas).
