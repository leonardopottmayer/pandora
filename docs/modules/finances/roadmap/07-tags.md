# Fase 07 — Tags

> Pré-requisitos: fase 04 (entidades a serem tagueadas existindo; cartões/faturas se a 05 já estiver feita).
> Referência: [finances-module.md](../finances-module.md) §3.5, §6 (fin004, fin005).

## Objetivo

Rótulos livres do usuário, atreláveis a qualquer entidade do módulo, e filtráveis em todas as listagens.

## Escopo

### Inclui
- Migrations `create-table-fin004-tag` e `create-table-fin005-tag-link` (vínculo polimórfico `entity_type` + `entity_id`, único por trio; sem FK física — integridade na aplicação).
- Aggregate `Tag` (nome único por usuário, cor); serviço de vínculo valida que a entidade-alvo existe e pertence ao usuário.
- `entity_type` suportados: `account`, `card`, `card_statement`, `transaction`, `recurring_transaction`, `pending_transaction` (os dois últimos passam a valer quando a fase 08 existir — o enum já nasce completo).
- API: `/tags` CRUD; `POST /tags/{id}/links` / `DELETE /tags/{id}/links/{entityType}/{entityId}`; atalho `PUT .../{entity}/{id}/tags` (substitui o conjunto) nas entidades principais.
- Filtro por tag no extrato (`GET /transactions?tags=`), em contas e cartões.
- Exclusão de tag remove os vínculos (com auditoria do que foi removido).
- Auditoria: `tag.created/updated/deleted/linked/unlinked`.

### Não inclui
- Análises por tag em relatórios — fase 12.

## Critérios de aceite
1. Vincular tag a entidade inexistente ou de outro usuário falha com 404.
2. Mesmo trio (tag, tipo, id) não duplica.
3. Filtro por múltiplas tags no extrato (semântica OR; AND se trivial) funciona com paginação.
4. Excluir tag limpa vínculos e audita.
