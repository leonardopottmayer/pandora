import type {
  AccountType,
  StatementStatus,
  TransactionKind,
  TransactionNature,
  TransactionStatus,
} from '../models'

// Cada enum mapeia para uma chave i18n (`finances.enums.*`) e uma cor de antd `Tag`.
// As chaves são resolvidas com `t(...)` na UI; aqui guardamos só a estrutura.

export interface EnumMeta {
  labelKey: string
  color?: string
}

export const ACCOUNT_TYPE_META: Record<AccountType, EnumMeta> = {
  cash: { labelKey: 'finances.enums.accountType.cash' },
  checking: { labelKey: 'finances.enums.accountType.checking' },
  savings: { labelKey: 'finances.enums.accountType.savings' },
  international: { labelKey: 'finances.enums.accountType.international' },
  crypto: { labelKey: 'finances.enums.accountType.crypto' },
  investment: { labelKey: 'finances.enums.accountType.investment' },
  other: { labelKey: 'finances.enums.accountType.other' },
}

export const TRANSACTION_STATUS_META: Record<TransactionStatus, EnumMeta> = {
  pending: { labelKey: 'finances.enums.transactionStatus.pending', color: 'gold' },
  posted: { labelKey: 'finances.enums.transactionStatus.posted', color: 'green' },
  void: { labelKey: 'finances.enums.transactionStatus.void', color: 'default' },
}

export const STATEMENT_STATUS_META: Record<StatementStatus, EnumMeta> = {
  open: { labelKey: 'finances.enums.statementStatus.open', color: 'blue' },
  closed: { labelKey: 'finances.enums.statementStatus.closed', color: 'geekblue' },
  'partially-paid': { labelKey: 'finances.enums.statementStatus.partiallyPaid', color: 'gold' },
  paid: { labelKey: 'finances.enums.statementStatus.paid', color: 'green' },
  overdue: { labelKey: 'finances.enums.statementStatus.overdue', color: 'red' },
}

export const TRANSACTION_NATURE_META: Record<TransactionNature, EnumMeta> = {
  income: { labelKey: 'finances.enums.nature.income', color: 'green' },
  expense: { labelKey: 'finances.enums.nature.expense', color: 'red' },
}

/** Direção de uma transação no saldo: entrada (+), saída (−) ou neutra. */
export type FlowDirection = 'in' | 'out' | 'neutral'

const KIND_DIRECTION: Record<TransactionKind, FlowDirection> = {
  'opening-balance': 'neutral',
  income: 'in',
  expense: 'out',
  'transfer-in': 'in',
  'transfer-out': 'out',
  'investment-contribution': 'out',
  'investment-redemption': 'in',
  yield: 'in',
  adjustment: 'neutral',
  refund: 'in',
  'card-statement-payment': 'out',
}

export function kindDirection(kind: TransactionKind): FlowDirection {
  return KIND_DIRECTION[kind]
}

export function transactionKindLabelKey(kind: TransactionKind): string {
  return `finances.enums.transactionKind.${camelize(kind)}`
}

function camelize(value: string): string {
  return value.replace(/-([a-z])/g, (_, c: string) => c.toUpperCase())
}

/** Moedas oferecidas como sugestão nos selects (o backend aceita qualquer código 3–10 letras). */
export const COMMON_CURRENCIES = ['BRL', 'USD', 'EUR', 'GBP', 'BTC', 'ETH', 'USDT'] as const
