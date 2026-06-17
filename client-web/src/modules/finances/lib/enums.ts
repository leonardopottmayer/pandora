import type {
  AccountType,
  PendingStatus,
  RecurrenceFrequency,
  RecurringStatus,
  StatementStatus,
  TransactionKind,
  TransactionNature,
  TransactionStatus,
} from '../models'

// Each enum maps to an i18n key (`finances.enums.*`) and an antd `Tag` colour.
// Keys are resolved with `t(...)` in the UI; here we only store the structure.

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

export const RECURRING_STATUS_META: Record<RecurringStatus, EnumMeta> = {
  active: { labelKey: 'finances.enums.recurringStatus.active', color: 'green' },
  paused: { labelKey: 'finances.enums.recurringStatus.paused', color: 'gold' },
  finished: { labelKey: 'finances.enums.recurringStatus.finished', color: 'default' },
}

export const PENDING_STATUS_META: Record<PendingStatus, EnumMeta> = {
  pending: { labelKey: 'finances.enums.pendingStatus.pending', color: 'gold' },
  approved: { labelKey: 'finances.enums.pendingStatus.approved', color: 'green' },
  rejected: { labelKey: 'finances.enums.pendingStatus.rejected', color: 'red' },
}

export function recurrenceFrequencyLabelKey(frequency: RecurrenceFrequency): string {
  return `finances.enums.recurrenceFrequency.${frequency}`
}

/** Transaction flow direction on the balance: in (+), out (−), or neutral. */
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

/** Currencies suggested in selects (the backend accepts any 3–10 letter code). */
export const COMMON_CURRENCIES = ['BRL', 'USD', 'EUR', 'GBP', 'BTC', 'ETH', 'USDT'] as const
