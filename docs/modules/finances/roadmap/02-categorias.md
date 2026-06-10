# Fase 02 — Categorias

> Pré-requisitos: fase 01.
> Referência: [finances-module.md](../finances-module.md) §3.4, §6 (fin002, fin003), D5.

## Objetivo

As duas dimensões de categorização disponíveis antes do ledger existir: categorias do **sistema** (seed, globais) e do **usuário** (CRUD próprio), em tabelas e FKs separadas.

## Escopo

### Inclui
- Migrations:
  - `create-table-fin002-system-category`;
  - `insert-fin002-system-categories` (seed: ~20 grupos pai / ~120 filhos conforme §3.4 — Housing, Food, Transport, …, Misc Income; cada grupo com um filho `is_other = true`);
  - `create-table-fin003-user-category`.
- Domínio:
  - `SystemCategory` como dado de referência (leitura via `ISystemCategoryReader`, sem comportamento);
  - aggregate `UserCategory`: hierarquia de 2 níveis (filho não tem filho), `transaction_nature` do filho = do pai, nome único por (usuário, pai), desativação não destrutiva (`is_active`).
- API:
  - `GET /categories/system` — árvore completa (filtro `nature`, `includeInactive`);
  - `/categories` — CRUD das do usuário + ativar/desativar + reordenação (`display_order`).
- Auditoria: `category.created/updated/activated/deactivated` com diff de campos.

### Não inclui
- Uso das categorias em lançamentos (fase 04) e regras de categorização (fase 11).

## Critérios de aceite
1. Seed idempotente e re-executável em ambiente novo; árvore do sistema retorna os 2 níveis ordenados por `display_order`.
2. Usuário não consegue criar 3º nível, filho com nature diferente do pai, nem editar categoria de sistema.
3. Desativar categoria com uso futuro em lançamentos não é bloqueante (validação só impede uso em *novos* registros — verificada de fato na fase 04).
4. Eventos de auditoria visíveis no `GET /audit` para cada mutação.
