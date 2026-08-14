# Fase 05 — Busca full-text + Ctrl+K

> Bloco no plano: MVP §7.1.6 · Depende de: 01, 03 · Camada: backend + frontend

## Objetivo

Achar pages por trecho do título ou do corpo, via full-text do Postgres, acessível por
um command palette (Ctrl+K). Fecha o MVP.

## Escopo

### Backend

- `tsvector` sobre `Title + ContentMarkdown` (coluna gerada ou índice, conforme padrão
  do projeto). Migration em `migrations/migrations/notes`.
- Query/endpoint de busca: `GET /api/notes/search?q=...` retornando pages que casam,
  com o mínimo pra exibir (id, título, ícone, trecho).

### Frontend

- Command palette (Ctrl+K) que consulta o endpoint e navega para a page escolhida.

## Verify

- Buscar por um trecho do **corpo** de uma page a encontra.
- Ctrl+K abre o palette, digita, seleciona e navega.

## Fora de escopo

- Ranking/realce de resultados (previsto para v2 no plano — §3).

## Notas de implementação (fase concluída)

- Coluna **gerada** `search_vector` em `notes.nte001_page`
  (`to_tsvector('simple', title || ' ' || content_markdown)`, `STORED`) + índice GIN. Sendo o
  Postgres quem mantém o vetor, nenhum caminho de save pode esquecer de atualizá-lo.
- Configuração `simple` (só lower-case, sem stemming): o caderno mistura PT-BR e EN, e escolher
  uma língua faria o outro idioma casar pior.
- No EF a coluna é **shadow property** (`PageColumns.SearchVector`) — o `Page` não conhece o vetor;
  a query chega nele por `EF.Property<NpgsqlTsVector>(...).Matches(...)`.
- `PageSearch` (Domain) traduz o que o usuário digitou: cada palavra vira `palavra:*` unida por
  `&`. Pontuação é **descartada**, não escapada — nada digitado pode virar sintaxe de `tsquery`.
  O mesmo tipo corta o `Excerpt` (160 chars em volta do primeiro match).
- Backend: `GET /api/v1/notes/pages/search?q=...`, teto de 20 resultados, ordenado por título.
  Pages arquivadas aparecem (com a flag); soft-deletadas não.
- Frontend: `SearchPalette` montado na `NotesPage` — Ctrl+K (Cmd+K no Mac) em *capture*, para
  ganhar do editor; termo com debounce de 200ms; setas/Enter/Esc. O palette é do módulo Notes,
  não do `AppLayout`: ele só busca pages.
- **Botão de busca: feito depois**, no lugar que já estava previsto — uma lupa no topo da sidebar,
  ao lado das de grafo e nova page. O atalho tinha ficado como única porta de entrada, e o que não
  se vê não se usa; o `title` do botão mostra `Ctrl+K`, então ele também ensina o atalho.
  - Para o botão abrir o mesmo modal, o estado `open` **subiu para a página** (`SearchPalette` virou
    controlado, com `open`/`onOpenChange`); o listener do atalho continua dentro do palette, que é
    de quem ele é. Nada da busca em si foi duplicado.
  - A `NotesGraphPage` monta a mesma sidebar, então passou a montar o palette também — senão o botão
    ficaria morto (ou sumiria) numa das duas rotas. De brinde, Ctrl+K passou a funcionar no grafo.
