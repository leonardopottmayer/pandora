# Módulo Notes

> Um Notion/Obsidian pessoal dentro do monolito modular Pandora.
> **Idioma:** o inglês é a documentação principal. 🇺🇸 [English version](../README.md).

O módulo **Notes** é uma base de conhecimento para um único usuário: **pages** hierárquicas numa
sidebar em árvore, uma **biblioteca de mídia** de imagens e arquivos, links entre páginas com backlinks,
tags, busca full-text, uma **visão em grafo** force-directed e um editor de blocos estilo Notion.

**Uma página é um documento de blocos** (desde a reforma em blocos, 2026-11-07): cada parágrafo,
título, item de lista, imagem ou tabela é um bloco com id estável, guardado como um documento `jsonb`
por página. Links, tags, uso de mídia e o texto da busca saem dos blocos a cada save. O plano e o
formato estão no [Plano de Produto](product-plan.md); os arquivos abaixo ainda descrevem a era do
markdown até cada etapa do plano reescrevê-los.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o panorama de produto e o vocabulário, depois vá ao tópico que
precisar. Cada arquivo traz o *contexto de produto* (o que significa para o usuário e por quê) e as
*regras técnicas* (aggregates, invariantes, schema, endpoints).

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | Visão, princípios, linguagem ubíqua, escopo (dentro/fora) |
| 2 | [Arquitetura](architecture.md) | Estrutura dos projetos, blocos de DDD, ports, decisões de design |
| 3 | [Modelo de dados](data-model.md) | Catálogo completo do schema (`nte001`–`nte011`): colunas, constraints, índices |
| 4 | [Pages e hierarquia](pages-and-hierarchy.md) | Aggregate `Page`, árvore, slug, mover/reordenar, favoritar, arquivar, soft delete |
| 4a | [Espaços](spaces.md) | Agrupar pages como espaços do Confluence, o espaço padrão, mover entre espaços |
| 5 | [Editor e blocos ricos](editor.md) | CodeMirror 6, autosave, preview, slash commands, callouts, tabelas, autocomplete |
| 6 | [Biblioteca de mídia](media-library.md) | Itens e pastas, miniaturas, storage, servir autenticado, o que fica pronto para vídeo |
| 7 | [Wikilinks e backlinks](wikilinks-and-backlinks.md) | Parse de `[[alvo]]`, arestas `PageLink`, linked mentions, create-on-click |
| 8 | [Tags](tags.md) | Parse de `#tag`, normalização, arestas derivadas, varredura de órfãs, cores, filtros |
| 9 | [Busca](search.md) | Coluna gerada `tsvector`, tradução para `tsquery`, excerpt, command palette |
| 10 | [Visão em grafo](graph-view.md) | Grafo global e local, passeio pela vizinhança, grau, renderização |
| 10a | [Histórico de versões](version-history.md) | Snapshots completos, comparação e restauração reversível |
| 11 | [Referência de API](api-reference.md) | Todos os endpoints em `/api/v{n}/notes` |
| 12 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |
| 13 | [Plano de Produto — reforma em blocos](product-plan.md) | O formato dos blocos, dados derivados, decisões e as etapas B1–B11 |

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Notes.*` (.NET 10, DDD, comandos/queries estilo CQRS).
- **Schema:** schema PostgreSQL `notes`, tabelas com prefixo `nteXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/notes` (React + TanStack Query + TipTap).
- **Base da API:** `/api/v{version}/notes`, autenticada, escopada ao usuário do token.
- **Migrations:** `migrations/migrations/notes/`.
