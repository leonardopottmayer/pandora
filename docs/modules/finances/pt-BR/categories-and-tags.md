# Categorias e tags

[← Voltar ao índice](README.md) · Aggregates: `SystemCategory`, `UserCategory`, `Tag`, `TagLink` · Tabelas: `fin002`, `fin003`, `fin004`, `fin005`

---

## Categorias

Há **duas dimensões de categorização independentes** (decisão de design D5). Um lançamento pode ter
uma categoria de sistema **e** uma do usuário ao mesmo tempo — análises podem cruzar as duas.

### Categorias do sistema (`fin002_system_category`)

- **Mantidas pelo sistema, seed, globais** — iguais para todos os usuários, sem `user_id`.
- **Hierárquicas, 2 níveis** (pai → filho) via `parent_category_id`.
- Tipadas por `transaction_nature` (`expense` | `income`).
- Cada uma tem `code` único global, mais `color`, `icon`, `display_order`, `is_active` e `is_other`
  (o filho fallback do grupo, ex.: "Outras moradia").
- Atualizadas por migration sem tocar nos dados do usuário. `created_by NULL` marca linhas de seed.
- **Somente-leitura para o usuário** — expostas via `GET /categories/system` (árvore; filtro por
  `nature`, `includeInactive`). Lidas via `ISystemCategoryReader`; sem comportamento/aggregate.

O seed cobre a taxonomia comum de finanças pessoais: grupos de despesa (Moradia, Alimentação,
Transporte, Saúde, Educação, Cuidados Pessoais, Família, Compras, Entretenimento, Viagem, Despesas
Financeiras, Assinaturas, Despesas de Trabalho, Pets, …) e grupos de receita (Renda Principal, Renda
de Investimentos, Renda de Vendas, Renda de Apoio, …), cada um com um conjunto de filhos incluindo um
fallback `is_other`. Códigos de sistema específicos referenciados pelo app incluem
`credit-card-payment` (usado em pagamentos de fatura).

### Categorias do usuário (`fin003_user_category`)

- CRUD completo, escopado a `user_id`.
- **Hierárquicas, 2 níveis**: um filho não pode ter filho; a `transaction_nature` do filho deve ser
  igual à do pai.
- Nome único por `(user_id, name, parent_category_id)`.
- **Desativação não destrutiva** (`is_active`): desativar uma categoria nunca quebra lançamentos
  existentes (a FK permanece); ela só some de *novos* lançamentos.
- API `/categories`: `GET` (listar), `POST`, `PUT {id}`, `POST {id}/activate`, `POST {id}/deactivate`.

Auditoria: `category.created`, `category.updated`, `category.activated`, `category.deactivated`.

## Tags (`fin004_tag` / `fin005_tag_link`)

- **Rótulos livres** do usuário: `name` (único por usuário), `color`.
- **Vínculo polimórfico** (`TagLink`): uma tag pode se atrelar a qualquer um destes `entity_type`:
  `account`, `card`, `card-statement`, `transaction`, `recurring-transaction`, `pending-transaction`.
- Sem FK física no `entity_id` (polimórfico) — a aplicação valida que a entidade-alvo existe e
  pertence ao usuário. O trio `(tag_id, entity_type, entity_id)` é único (sem vínculo duplicado).
- Excluir uma tag **cascateia** seus vínculos (`ON DELETE CASCADE`), auditado.
- Filtrável em listagens (ex.: `GET /transactions?tags=`).

### API

| Método | Rota | Propósito |
|---|---|---|
| GET / POST / PUT / DELETE | `/tags`, `/tags/{id}` | CRUD |
| GET | `/tags/{id}/links` | Vínculos de uma tag |
| POST | `/tags/{id}/links` | Adicionar vínculo |
| DELETE | `/tags/{id}/links/{entityType}/{entityId}` | Remover vínculo |
| PUT | `/{entity}/{id}/tags` | Substituir o conjunto inteiro de tags de uma conta/cartão/fatura/lançamento |

Auditoria: `tag.created/updated/deleted/linked/unlinked`.
