# Módulo Notes

> Um Notion/Obsidian pessoal dentro do monolito modular Pandora.
> **Idioma:** o inglês é a documentação principal. 🇺🇸 [English version](../README.md).

O módulo **Notes** é uma base de conhecimento em markdown para um único usuário: **pages**
hierárquicas numa sidebar em árvore, **anexos** de imagem/arquivo inline, `[[wikilinks]]` com
backlinks, `#tags`, busca full-text, uma **visão em grafo** force-directed e um editor CodeMirror
estilo Obsidian com slash commands, callouts e tabelas markdown.

A regra que rege o módulo inteiro: **o markdown é a única fonte da verdade.** Links e tags não são
cadastrados em formulário — são escritos no texto, e o backend materializa as linhas a cada save. Um
`.md` exportado leva junto tudo que importa; o banco só guarda o que o texto puro não consegue
lembrar (a cor de uma tag) e o que o texto puro não responde rápido (o grafo de links, o vetor de
busca).

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o panorama de produto e o vocabulário, depois vá ao tópico que
precisar. Cada arquivo traz o *contexto de produto* (o que significa para o usuário e por quê) e as
*regras técnicas* (aggregates, invariantes, schema, endpoints).

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | Visão, princípios, linguagem ubíqua, escopo (dentro/fora) |
| 2 | [Arquitetura](architecture.md) | Estrutura dos projetos, blocos de DDD, ports, decisões de design |
| 3 | [Modelo de dados](data-model.md) | Catálogo completo do schema (`nte001`–`nte006`): colunas, constraints, índices |
| 4 | [Pages e hierarquia](pages-and-hierarchy.md) | Aggregate `Page`, árvore, slug, mover/reordenar, favoritar, arquivar, soft delete |
| 5 | [Editor e blocos ricos](editor.md) | CodeMirror 6, autosave, preview, slash commands, callouts, tabelas, autocomplete |
| 6 | [Anexos e storage](attachments.md) | `IFileStorage`, `DatabaseFileStorage`, upload/download, embed autenticado |
| 7 | [Wikilinks e backlinks](wikilinks-and-backlinks.md) | Parse de `[[alvo]]`, arestas `PageLink`, linked mentions, create-on-click |
| 8 | [Tags](tags.md) | Parse de `#tag`, normalização, arestas derivadas, varredura de órfãs, cores, filtros |
| 9 | [Busca](search.md) | Coluna gerada `tsvector`, tradução para `tsquery`, excerpt, command palette |
| 10 | [Visão em grafo](graph-view.md) | Grafo global e local, passeio pela vizinhança, grau, renderização |
| 11 | [Referência de API](api-reference.md) | Todos os endpoints em `/api/v{n}/notes` |
| 12 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Notes.*` (.NET 10, DDD, comandos/queries estilo CQRS).
- **Schema:** schema PostgreSQL `notes`, tabelas com prefixo `nteXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/notes` (React + TanStack Query + CodeMirror 6).
- **Base da API:** `/api/v{version}/notes`, autenticada, escopada ao usuário do token.
- **Migrations:** `migrations/migrations/notes/`.
