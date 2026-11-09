# Módulo Notes

> Um Notion/Confluence pessoal dentro do monolito modular Pandora.
> 🇺🇸 [English version](../README.md).

O **Notes** é uma base de conhecimento de um usuário só. As páginas ficam em **espaços** e
**pastas**, podem ter subpáginas, e são **documentos de blocos**: cada parágrafo, título, item de
lista, imagem, tabela, callout ou embed é um bloco com id estável, guardado como um documento `jsonb`
por página. Links, tags, uso de mídia e o texto de busca são derivados dos blocos a cada salvamento.

Em volta das páginas: biblioteca de mídia, links entre páginas com backlinks, tags, busca de texto,
grafo, histórico de versões, comentários, modelos, notas diárias, links públicos e ações de IA pelo
[Assistant](../../assistant/pt-BR/README.md).

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o produto e o vocabulário, depois o tópico que precisar.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | O que o módulo faz, princípios, glossário, escopo |
| 2 | [Arquitetura](architecture.md) | Projetos, aggregates, `PageContent`, dados derivados, decisões |
| 3 | [Modelo de dados](data-model.md) | Catálogo `nte001`–`nte017`: colunas, constraints, índices |
| 4 | [Blocos e editor](blocks.md) | O formato dos blocos, cada tipo de bloco e de inline, o editor |
| 5 | [Páginas e hierarquia](pages-and-hierarchy.md) | Páginas, pastas, a árvore, lixeira, modelos, notas diárias, configurações da página, exportação |
| 6 | [Espaços](spaces.md) | Agrupar páginas como os espaços do Confluence, o espaço padrão |
| 7 | [Links e backlinks](links-and-backlinks.md) | Menções, blocos de link de página, cópias sincronizadas, "Mencionada em" |
| 8 | [Tags](tags.md) | O bloco de tags, normalização, cores, filtros |
| 9 | [Busca](search.md) | `tsvector` sobre o texto derivado, a paleta, o destaque |
| 10 | [Biblioteca de mídia](media-library.md) | Itens e pastas, miniaturas, armazenamento, entrega, que páginas usam o quê |
| 11 | [Grafo](graph-view.md) | Grafo global e local, famílias, inspeção |
| 12 | [Histórico de versões](version-history.md) | Snapshots, comparação, restauração |
| 13 | [Comentários](comments.md) | Conversas em texto ou blocos, datas dos blocos |
| 14 | [IA](ai.md) | Ações de texto, resumo da página, pergunte às suas notas, bloco de IA, captura pelo Telegram, ferramentas do Assistant |
| 15 | [Referência da API](api-reference.md) | Todos os endpoints em `/api/v{n}/notes` |
| 16 | [Status de implementação](implementation-status.md) | O que está construído, pontos em aberto |
| 17 | [Plano de produto](product-plan.md) | O que falta |

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Notes.*` (.NET 10, DDD, commands/queries sobre o mediator do Tars).
- **Schema:** schema PostgreSQL `notes`, tabelas `nte001`–`nte017`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/notes` (React, TanStack Query, TipTap 3).
- **Base da API:** `/api/v{version}/notes`, autenticada, restrita ao usuário do token; páginas públicas
  em `/api/v{version}/public/notes/pages/{token}`.
- **Migrations:** `migrations/migrations/notes/`.
