# Referência de API

[← Voltar ao índice](README.md)

Base: **`/api/v{version}/finances`**. Todo endpoint é autenticado e escopado ao usuário do token. Um
recurso de outro usuário retorna **404** (não 403). Os controllers ficam em `Presentation/Controllers`.

---

## Contas — `/accounts`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/accounts` | Listar (filtrar arquivadas) |
| GET | `/accounts/{id}` | Detalhe |
| POST | `/accounts` | Criar (saldo inicial opcional) |
| PUT | `/accounts/{id}` | Atualizar |
| DELETE | `/accounts/{id}` | Excluir (só sem histórico) |
| POST | `/accounts/{id}/archive` · `/unarchive` | Arquivar / desarquivar |
| GET | `/accounts/{id}/balance` | Saldo postado + projetado |
| GET | `/accounts/{id}/transactions` | Extrato da conta |
| PUT | `/accounts/{id}/tags` | Substituir conjunto de tags |

## Cartões — `/cards`

| Método | Rota | Propósito |
|---|---|---|
| GET / POST | `/cards`, `/cards/{id}` (GET) | Listar / criar / detalhe |
| PUT / DELETE | `/cards/{id}` | Atualizar / excluir (só sem histórico) |
| POST | `/cards/{id}/archive` · `/unarchive` | Arquivar / desarquivar |
| GET | `/cards/{id}/statements` | Faturas |
| GET | `/cards/{id}/installment-plans` | Planos de parcelamento |
| GET | `/cards/{id}/available-limit` | Limite − faturas não pagas |
| PUT | `/cards/{id}/tags` | Substituir conjunto de tags |

## Faturas — `/statements`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/statements/{id}` | Detalhe + lançamentos |
| POST | `/statements/{id}/pay` | Pagar de uma conta |
| POST | `/statements/{id}/settle` | Write-off sem caixa (onboarding) |
| POST | `/statements/{id}/close` | Fechamento manual |
| POST | `/statements/{id}/reopen` | Reabrir para novas compras |
| PUT | `/statements/{id}/tags` | Substituir conjunto de tags |

## Lançamentos — `/transactions`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/transactions` | Listar (filtros ricos) |
| GET | `/transactions/{id}` | Detalhe |
| POST | `/transactions` | Criar (conta/cartão; `installments`) |
| POST | `/transactions/transfer` | Criar um par de transferência |
| PUT | `/transactions/{id}` | Editar campos cosméticos |
| POST | `/transactions/{id}/post` | Postar um lançamento agendado |
| POST | `/transactions/{id}/void` · `/unvoid` · `/reverse` | Cancelar / desfazer / estornar |
| PUT | `/transactions/{id}/tags` | Substituir conjunto de tags |

## Planos de parcelamento — `/installment-plans`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/installment-plans/{id}` | Detalhe do plano |

## Categorias

| Método | Rota | Propósito |
|---|---|---|
| GET | `/categories/system` | Árvore de categorias do sistema |
| GET / POST | `/categories` | Categorias do usuário: listar / criar |
| PUT | `/categories/{id}` | Atualizar |
| POST | `/categories/{id}/activate` · `/deactivate` | Ativar / desativar |

## Tags — `/tags`

| Método | Rota | Propósito |
|---|---|---|
| GET / POST | `/tags` | Listar / criar |
| PUT / DELETE | `/tags/{id}` | Atualizar / excluir |
| GET | `/tags/{id}/links` | Vínculos de uma tag |
| POST | `/tags/{id}/links` | Adicionar vínculo |
| DELETE | `/tags/{id}/links/{entityType}/{entityId}` | Remover vínculo |

## Recorrências — `/recurring-transactions`

| Método | Rota | Propósito |
|---|---|---|
| GET / POST | `/recurring-transactions`, `/{id}` (GET) | Listar / criar / detalhe |
| PUT / DELETE | `/recurring-transactions/{id}` | Atualizar / excluir |
| POST | `/recurring-transactions/{id}/pause` · `/resume` · `/generate` | Controle / geração sob demanda |

## Transações pendentes (inbox) — `/pending-transactions`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/pending-transactions` | Inbox (filtros) |
| PUT | `/pending-transactions/{id}` | Editar proposta |
| POST | `/pending-transactions/{id}/approve` · `/reject` · `/link` | Decidir |
| POST | `/pending-transactions/approve-batch` | Aprovar em lote |
| POST | `/pending-transactions/transfer` | Montar transferência de duas sugestões |

## Importação — `/imports`

| Método | Rota | Propósito |
|---|---|---|
| POST | `/imports` | Upload (multipart) |
| GET | `/imports` · `/imports/{id}` | Listar / status + contadores |
| GET | `/imports/{id}/rows` | Linhas (bruto + dedup) |
| POST | `/imports/{id}/abort` · `/retry` | Descartar / reprocessar |

## Layouts de importação — `/import-layouts`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/import-layouts` | Layouts de sistema |

## Auditoria — `/audit`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/audit?entityType=&entityId=` | Timeline da entidade |
| GET | `/audit?correlationId=` | Tudo de uma operação |
