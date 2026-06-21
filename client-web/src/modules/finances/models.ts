// Finances module types — mirror the backend DTOs/Requests
// (Pottmayer.Pandora.Modules.Finances.Application.Dtos / .Presentation.Requests).
// Backend JSON is camelCase; `DateOnly` dates arrive as "yyyy-MM-dd" and
// `DateTimeOffset` as ISO 8601. `decimal` values arrive as number.

// ---------------------------------------------------------------------------
// Enums (string unions + arrays for UI iteration)
// ---------------------------------------------------------------------------

export const ACCOUNT_TYPES = [
  'cash',
  'checking',
  'savings',
  'international',
  'crypto',
  'investment',
  'other',
] as const
export type AccountType = (typeof ACCOUNT_TYPES)[number]

export const TRANSACTION_KINDS = [
  'opening-balance',
  'income',
  'expense',
  'transfer-in',
  'transfer-out',
  'investment-contribution',
  'investment-redemption',
  'yield',
  'adjustment',
  'refund',
  'card-statement-payment',
] as const
export type TransactionKind = (typeof TRANSACTION_KINDS)[number]

export const TRANSACTION_STATUSES = ['pending', 'posted', 'void'] as const
export type TransactionStatus = (typeof TRANSACTION_STATUSES)[number]

export const TRANSACTION_NATURES = ['income', 'expense'] as const
export type TransactionNature = (typeof TRANSACTION_NATURES)[number]

export const STATEMENT_STATUSES = ['open', 'closed', 'partially-paid', 'paid', 'overdue'] as const
export type StatementStatus = (typeof STATEMENT_STATUSES)[number]

export const TRANSACTION_ORIGINS = ['manual', 'import', 'recurrence', 'projection'] as const
export type TransactionOrigin = (typeof TRANSACTION_ORIGINS)[number]

export const RECURRENCE_FREQUENCIES = ['daily', 'weekly', 'monthly', 'yearly'] as const
export type RecurrenceFrequency = (typeof RECURRENCE_FREQUENCIES)[number]

export const RECURRING_STATUSES = ['active', 'paused', 'finished'] as const
export type RecurringStatus = (typeof RECURRING_STATUSES)[number]

export const PENDING_STATUSES = ['pending', 'approved', 'rejected'] as const
export type PendingStatus = (typeof PENDING_STATUSES)[number]

export const PENDING_SOURCES = ['recurrence', 'import'] as const
export type PendingSource = (typeof PENDING_SOURCES)[number]

export const IMPORT_STATUSES = ['received', 'parsing', 'completed', 'failed', 'aborted'] as const
export type ImportStatus = (typeof IMPORT_STATUSES)[number]

export const IMPORT_ROW_STATUSES = ['pending', 'skipped', 'suggestion-created', 'error'] as const
export type ImportRowStatus = (typeof IMPORT_ROW_STATUSES)[number]

export const DEDUP_STATUSES = ['new', 'certain', 'suspected', 'matched'] as const
export type DedupStatus = (typeof DEDUP_STATUSES)[number]

export const TAGGABLE_ENTITY_TYPES = [
  'account',
  'card',
  'card-statement',
  'transaction',
  'recurring-transaction',
  'pending-transaction',
] as const
export type TaggableEntityType = (typeof TAGGABLE_ENTITY_TYPES)[number]

// ---------------------------------------------------------------------------
// Response DTOs
// ---------------------------------------------------------------------------

export interface AccountDto {
  id: string
  name: string
  type: AccountType
  currency: string
  institution: string | null
  description: string | null
  color: string | null
  icon: string | null
  displayOrder: number
  archivedAt: string | null
}

export interface AccountBalanceDto {
  accountId: string
  currency: string
  posted: number
  projected: number
}

export interface TransactionDto {
  id: string
  accountId: string | null
  cardStatementId: string | null
  cardId: string | null
  paidStatementId: string | null
  kind: TransactionKind
  status: TransactionStatus
  amount: number
  currency: string
  occurredOn: string
  description: string
  /** Neutral descriptor for system-generated text; null for user-authored descriptions. */
  descriptionKey: string | null
  descriptionArgs: string[] | null
  payee: string | null
  notes: string | null
  systemCategoryId: string | null
  userCategoryId: string | null
  transferGroupId: string | null
  fxRate: number | null
  installmentPlanId: string | null
  installmentNumber: number | null
  origin: TransactionOrigin
  postedAt: string | null
  voidedAt: string | null
  voidReason: string | null
  statementReferenceMonth: string | null
  statementDueDate: string | null
}

export interface CardDto {
  id: string
  name: string
  brand: string | null
  lastFour: string | null
  creditLimit: number | null
  closingDay: number
  dueDay: number
  currency: string
  defaultPaymentAccountId: string | null
  archivedAt: string | null
}

export interface CardAvailableLimitDto {
  cardId: string
  creditLimit: number | null
  availableLimit: number | null
}

export interface CardStatementDto {
  id: string
  cardId: string
  referenceMonth: string
  closingDate: string
  dueDate: string
  status: StatementStatus
  totalAmount: number
  paidAmount: number
  remainingAmount: number
  closedAt: string | null
  paidAt: string | null
  overdueAt: string | null
}

export interface CardStatementDetailDto {
  statement: CardStatementDto
  transactions: TransactionDto[]
}

export interface InstallmentItemDto {
  number: number
  transactionId: string
  statementId: string
  referenceMonth: string
  amount: number
  status: TransactionStatus
  statementStatus: StatementStatus
}

export interface InstallmentPlanDto {
  id: string
  cardId: string
  origin: 'manual' | 'import'
  description: string
  installmentCount: number
  totalAmount: number
  totalIsEstimate: boolean
  firstReferenceMonth: string
  remainingAmount: number
  paidInstallments: number
  systemCategoryId: string | null
  userCategoryId: string | null
  installments: InstallmentItemDto[]
}

export interface SystemCategoryDto {
  id: string
  code: string
  name: string
  nature: TransactionNature
  color: string | null
  icon: string | null
  displayOrder: number
  isOther: boolean
  isActive: boolean
  children: SystemCategoryDto[]
}

export interface UserCategoryDto {
  id: string
  name: string
  nature: TransactionNature
  parentCategoryId: string | null
  color: string | null
  icon: string | null
  displayOrder: number
  isActive: boolean
  children: UserCategoryDto[]
}

export interface TagDto {
  id: string
  name: string
  color: string | null
}

export interface TagLinkDto {
  id: string
  tagId: string
  entityType: TaggableEntityType
  entityId: string
}

export interface AuditEventDto {
  id: string
  actorUserId: string | null
  entityType: string
  entityId: string
  eventType: string
  data: string | null
  correlationId: string | null
  occurredAt: string
}

export interface RecurringTransactionDto {
  id: string
  name: string
  accountId: string | null
  cardId: string | null
  kind: TransactionKind
  amount: number | null
  amountIsEstimate: boolean
  description: string
  payee: string | null
  systemCategoryId: string | null
  userCategoryId: string | null
  frequency: RecurrenceFrequency
  interval: number
  dayOfMonth: number | null
  weekday: number | null
  startDate: string
  endDate: string | null
  maxOccurrences: number | null
  status: RecurringStatus
  autoPost: boolean
  autoGenerate: boolean
  nextOccurrenceOn: string
  occurrencesCount: number
  createdAt: string
  updatedAt: string | null
}

export interface PendingTransactionDto {
  id: string
  source: PendingSource
  recurringTransactionId: string | null
  accountId: string | null
  cardId: string | null
  kind: TransactionKind
  amount: number | null
  currency: string
  occurredOn: string
  description: string
  payee: string | null
  notes: string | null
  systemCategoryId: string | null
  userCategoryId: string | null
  suggestedStatementId: string | null
  /** Immutable JSON snapshot of the originally generated proposal. */
  originalPayload: string
  status: PendingStatus
  decidedAt: string | null
  decidedBy: string | null
  rejectionReason: string | null
  transactionId: string | null
  /** Import provenance / dedup — null for recurrence-sourced suggestions. */
  importRowId: string | null
  dedupStatus: DedupStatus | null
  duplicateOfTransactionId: string | null
  createdAt: string
  updatedAt: string | null
}

// ---------------------------------------------------------------------------
// Requests (create/update payloads)
// ---------------------------------------------------------------------------

export interface CreateAccountRequest {
  name: string
  type: AccountType
  currency: string
  institution?: string | null
  description?: string | null
  color?: string | null
  icon?: string | null
  displayOrder: number
  openingBalance?: number | null
}

export interface UpdateAccountRequest {
  name: string
  type: AccountType
  institution?: string | null
  description?: string | null
  color?: string | null
  icon?: string | null
  displayOrder: number
}

export interface CreateCardRequest {
  name: string
  brand?: string | null
  lastFour?: string | null
  creditLimit?: number | null
  closingDay: number
  dueDay: number
  currency: string
  defaultPaymentAccountId?: string | null
}

export interface UpdateCardRequest {
  name: string
  brand?: string | null
  lastFour?: string | null
  creditLimit?: number | null
  closingDay: number
  dueDay: number
  defaultPaymentAccountId?: string | null
}

export interface CreateTransactionRequest {
  accountId?: string | null
  cardId?: string | null
  cardStatementId?: string | null
  kind: TransactionKind
  amount: number
  occurredOn: string
  description: string
  payee?: string | null
  notes?: string | null
  systemCategoryId?: string | null
  userCategoryId?: string | null
  installments?: number
}

export interface CreateTransferRequest {
  fromAccountId: string
  toAccountId: string
  amountOut: number
  amountIn?: number | null
  fxRate?: number | null
  occurredOn: string
  description: string
  notes?: string | null
}

export interface UpdateTransactionRequest {
  description: string
  payee?: string | null
  notes?: string | null
  systemCategoryId?: string | null
  userCategoryId?: string | null
}

export interface VoidTransactionRequest {
  reason?: string | null
  voidEntirePlan?: boolean
}

export interface PayStatementRequest {
  accountId: string
  amount: number
  occurredOn?: string | null
  description?: string | null
  notes?: string | null
  fxRate?: number | null
}

export interface CreateUserCategoryRequest {
  name: string
  nature: TransactionNature
  parentCategoryId?: string | null
  color?: string | null
  icon?: string | null
  displayOrder: number
}

export interface UpdateUserCategoryRequest {
  name: string
  color?: string | null
  icon?: string | null
  displayOrder: number
}

export interface CreateTagRequest {
  name: string
  color?: string | null
}

export interface UpdateTagRequest {
  name: string
  color?: string | null
}

export interface LinkTagRequest {
  entityType: TaggableEntityType
  entityId: string
}

export interface SetEntityTagsRequest {
  tagIds: string[]
}

export interface CreateRecurringTransactionRequest {
  name: string
  accountId?: string | null
  cardId?: string | null
  kind: TransactionKind
  amount?: number | null
  amountIsEstimate: boolean
  description: string
  payee?: string | null
  systemCategoryId?: string | null
  userCategoryId?: string | null
  frequency: RecurrenceFrequency
  interval: number
  dayOfMonth?: number | null
  weekday?: number | null
  startDate: string
  endDate?: string | null
  maxOccurrences?: number | null
  autoPost: boolean
  autoGenerate: boolean
}

export interface UpdateRecurringTransactionRequest {
  name: string
  amount?: number | null
  amountIsEstimate: boolean
  description: string
  payee?: string | null
  systemCategoryId?: string | null
  userCategoryId?: string | null
  endDate?: string | null
  maxOccurrences?: number | null
  autoPost: boolean
  autoGenerate: boolean
}

export interface GenerateRecurringTransactionOccurrenceRequest {
  destination: 'inbox' | 'transactions'
  advanceSchedule: boolean
  occurredOn?: string | null
  amount?: number | null
  description?: string | null
  payee?: string | null
  notes?: string | null
  systemCategoryId?: string | null
  userCategoryId?: string | null
}

export interface GeneratedOccurrenceDto {
  destination: 'inbox' | 'transactions'
  pending: PendingTransactionDto | null
  transaction: TransactionDto | null
}

export interface UpdatePendingTransactionRequest {
  kind: TransactionKind
  amount?: number | null
  occurredOn: string
  description: string
  payee?: string | null
  notes?: string | null
  systemCategoryId?: string | null
  userCategoryId?: string | null
  suggestedStatementId?: string | null
}

export interface RejectPendingTransactionRequest {
  reason?: string | null
}

export interface ApprovePendingTransactionBatchRequest {
  ids: string[]
}

/** Links an import suggestion to a transaction the user already has (no new transaction is created). */
export interface LinkPendingTransactionRequest {
  transactionId: string
}

/** Turns two account suggestions (one outflow, one inflow) into a transfer pair. */
export interface CreateTransferFromPendingRequest {
  outflowPendingId: string
  inflowPendingId: string
  description?: string | null
  occurredOn?: string | null
}

/** Filters for the pending transaction inbox (GET /pending-transactions). */
export interface PendingTransactionFilters {
  source?: PendingSource
  accountId?: string
  cardId?: string
  from?: string
  to?: string
  skip?: number
  take?: number
}

// ---------------------------------------------------------------------------
// Import DTOs (phases 09 / 10)
// ---------------------------------------------------------------------------

export interface ImportLayoutDto {
  id: string
  layoutCode: string
  name: string
  bankName: string | null
  fileFormat: string
  accountType: string
  isSystemLayout: boolean
  createdAt: string
}

export interface ImportFileDto {
  id: string
  userId: string
  layoutId: string | null
  accountId: string | null
  cardId: string | null
  fileName: string
  fileHash: string
  fileSize: number
  correlationId: string
  status: ImportStatus
  totalRows: number
  parsedRows: number
  errorRows: number
  duplicateRows: number
  suggestionRows: number
  retryCount: number
  errorMessage: string | null
  startedAt: string | null
  completedAt: string | null
  createdAt: string
}

export interface ImportRowDto {
  id: string
  importFileId: string
  rowIndex: number
  rawData: string
  parsedPayload: string | null
  externalId: string | null
  dedupKey: string | null
  dedupStatus: DedupStatus
  matchedTransactionId: string | null
  matchedPendingTransactionId: string | null
  installmentNumber: number | null
  installmentCount: number | null
  matchedInstallmentPlanId: string | null
  pendingTransactionId: string | null
  status: ImportRowStatus
  errorMessage: string | null
  createdAt: string
}

export interface ImportFileFilters {
  skip?: number
  take?: number
}

/** Filters for the transaction list (GET /transactions and /accounts/{id}/transactions). */
export interface TransactionFilters {
  accountId?: string
  from?: string
  to?: string
  kind?: TransactionKind
  status?: TransactionStatus
  systemCategoryId?: string
  userCategoryId?: string
  text?: string
  origin?: TransactionOrigin
  tags?: string[]
  skip?: number
  take?: number
}
