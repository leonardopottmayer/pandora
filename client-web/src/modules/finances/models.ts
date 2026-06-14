// Tipos do módulo financeiro — espelham os DTOs/Requests do backend
// (Pottmayer.Pandora.Modules.Finances.Application.Dtos / .Presentation.Requests).
// O JSON do backend é camelCase; datas `DateOnly` chegam como "yyyy-MM-dd" e
// `DateTimeOffset` como ISO 8601. Valores `decimal` chegam como number.

// ---------------------------------------------------------------------------
// Enums (string unions + arrays para iteração na UI)
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
// DTOs de resposta
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
  /** Descritor neutro para textos de sistema; null para descrições do usuário. */
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

// ---------------------------------------------------------------------------
// Requests (payloads de criação/edição)
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

/** Filtros do extrato/lista de transações (GET /transactions e /accounts/{id}/transactions). */
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
