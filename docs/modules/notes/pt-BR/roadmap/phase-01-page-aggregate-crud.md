# Fase 01 — `Page` aggregate + migration + CRUD

> Bloco no plano: MVP §7.0.2 · Depende de: 00 · Camada: backend

## Objetivo

Modelar o aggregate root `Page`, criar a migration e expor CRUD completo (Commands +
Queries), incluindo hierarquia pai/filho e as operações de organização (mover,
favoritar, arquivar).

## Escopo

### Domínio (`Domain`)

- Aggregate `Page`: `Id, ParentId?, Title, Slug, ContentMarkdown, Icon?, OrderIndex,
  IsFavorite, IsArchived, CreatedAt, UpdatedAt`.
- Regras:
  - Hierarquia só via `ParentId`; **sem ciclos** — validar no reparent.
  - Deletar = **soft-delete** (`IsArchived`/flag de deleção conforme padrão do projeto).
  - `Slug` gerado a partir do título (resolver colisões).

### Application

- Commands: criar page (raiz ou filha), atualizar conteúdo/título, mover/reparentar,
  reordenar entre irmãs, favoritar, arquivar, deletar (soft).
- Queries: obter page por id/slug, listar árvore (para a sidebar), listar filhas.

### Persistence + migration

- Mapeamento EF/persistência espelhando o padrão do Finances.
- Migration em `migrations/migrations/notes` criando a tabela `pages`.

### Presentation

- Endpoints REST `GET/POST/PUT/DELETE /api/notes/pages` cobrindo os commands/queries acima.

## Verify

- Testes de integração criam, leem e **movem** uma page (cobrindo reparent válido).
- Teste que rejeita reparent que criaria ciclo.
- Migration aplica limpa em banco novo; app sobe e os endpoints respondem.

## Fora de escopo

- Anexos, wikilinks, busca, frontend (fases seguintes).
- Tags (opcional no MVP; deixar para depois se não for trivial espelhar o padrão do Finances).
