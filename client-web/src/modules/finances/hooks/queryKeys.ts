import type { PendingTransactionFilters, TransactionFilters } from '../models'

// Central query key factory for the finances module. Centralising prevents
// mismatches between callers that query and those that invalidate in the TanStack Query cache.
export const financeKeys = {
  all: ['finances'] as const,

  accounts: () => [...financeKeys.all, 'accounts'] as const,
  accountList: (params: { includeArchived?: boolean; tags?: string[] } = {}) =>
    [...financeKeys.accounts(), 'list', params] as const,
  account: (id: string) => [...financeKeys.accounts(), 'detail', id] as const,
  accountBalance: (id: string) => [...financeKeys.accounts(), 'balance', id] as const,
  accountTransactions: (id: string, filters: TransactionFilters = {}) =>
    [...financeKeys.accounts(), 'transactions', id, filters] as const,

  transactions: () => [...financeKeys.all, 'transactions'] as const,
  transactionList: (filters: TransactionFilters = {}) =>
    [...financeKeys.transactions(), 'list', filters] as const,

  cards: () => [...financeKeys.all, 'cards'] as const,
  cardList: (params: { includeArchived?: boolean; tags?: string[] } = {}) =>
    [...financeKeys.cards(), 'list', params] as const,
  card: (id: string) => [...financeKeys.cards(), 'detail', id] as const,
  cardAvailableLimit: (id: string) => [...financeKeys.cards(), 'available-limit', id] as const,
  cardStatements: (id: string) => [...financeKeys.cards(), 'statements', id] as const,
  cardInstallmentPlans: (id: string) => [...financeKeys.cards(), 'installment-plans', id] as const,

  statements: () => [...financeKeys.all, 'statements'] as const,
  statement: (id: string) => [...financeKeys.statements(), 'detail', id] as const,

  installmentPlans: () => [...financeKeys.all, 'installment-plans'] as const,
  installmentPlan: (id: string) => [...financeKeys.installmentPlans(), 'detail', id] as const,

  categories: () => [...financeKeys.all, 'categories'] as const,
  systemCategories: (params: { nature?: string; includeInactive?: boolean } = {}) =>
    [...financeKeys.categories(), 'system', params] as const,
  userCategories: (params: { includeInactive?: boolean } = {}) =>
    [...financeKeys.categories(), 'user', params] as const,

  tags: () => [...financeKeys.all, 'tags'] as const,
  tagList: () => [...financeKeys.tags(), 'list'] as const,
  tagLinks: (id: string) => [...financeKeys.tags(), 'links', id] as const,

  audit: () => [...financeKeys.all, 'audit'] as const,
  auditTimeline: (params: object = {}) =>
    [...financeKeys.audit(), 'timeline', params] as const,

  recurring: () => [...financeKeys.all, 'recurring'] as const,
  recurringList: () => [...financeKeys.recurring(), 'list'] as const,
  recurringItem: (id: string) => [...financeKeys.recurring(), 'detail', id] as const,

  pending: () => [...financeKeys.all, 'pending'] as const,
  pendingList: (filters: PendingTransactionFilters = {}) =>
    [...financeKeys.pending(), 'list', filters] as const,
}
