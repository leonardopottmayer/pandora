# Busca

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Tags](tags.md), [Referência de API](api-reference.md)

---

## 1. O vetor

A busca full-text roda sobre uma **coluna gerada `STORED`** em `nte001_page`:

```sql
search_vector tsvector GENERATED ALWAYS AS (
    to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_markdown, ''))
) STORED
```

com um índice **GIN** sobre ela. Sendo o Postgres quem mantém o vetor, nenhum caminho de save pode
esquecer de atualizá-lo — que é exatamente o motivo de ser coluna gerada em vez de algo que a
aplicação escreve.

A configuração é **`simple`**: só lower-case, sem stemming, sem adivinhar idioma. O caderno mistura
português e inglês, e escolher uma língua faria o outro idioma casar pior.

No EF a coluna é **shadow property** (`PageColumns.SearchVector`) — o aggregate `Page` não sabe que o
vetor existe. A query chega nele por `EF.Property<NpgsqlTsVector>(…).Matches(…)`.

## 2. Traduzindo o que o usuário digitou

O `PageSearch` (Domain) transforma o termo num `tsquery`: cada palavra vira `palavra:*`, unidas por
`&`. Ou seja, toda palavra tem que estar presente, e a última é prefixo — um palette casa enquanto
ainda está sendo digitado.

Pontuação é **descartada, não escapada**: nada digitado pode virar sintaxe de `tsquery`. Quando não
sobra nada pesquisável, o resultado é string vazia e o chamador responde sem resultados em vez de
consultar.

O mesmo tipo corta o **excerpt**: 160 caracteres em volta da primeira palavra do termo, guardando ~30
caracteres de contexto antes do match, com reticências marcando o corte. Um hit só de título cai no
começo do corpo.

## 3. O endpoint

`GET /notes/pages/search?q=…&tagIds=…`

- Teto de **20 resultados**, ordenados por **título**.
- Pages **arquivadas** aparecem, com a flag; soft-deletadas nunca.
- Com filtro de tag, a query lê `20 × 10` hits e intersecta depois — cortar antes deixaria de fora uma
  page que satisfaz os dois critérios.
- Com tags e **`q` vazio**, lista as pages daquelas tags: é assim que se navega uma tag.

`PageSearchResultDto(Id, Title, Slug, Icon, IsArchived, Excerpt)` é o mínimo que o palette precisa
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

## 5. Não implementado

**Ranking e realce** estão previstos para v2 e não estão aqui: sem ordenação por `ts_rank`, sem
`ts_headline` — o excerpt é uma fatia crua e a ordenação é alfabética por título. Ver
[Status de implementação](implementation-status.md).
