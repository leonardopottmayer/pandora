/** Field-level error item in the Tars envelope. */
export interface TarsFieldError {
  code: string
  message: string
}

/** Tars success envelope: the payload comes in `data`. */
export interface TarsSuccessEnvelope<T> {
  success: true
  data: T
  metadata?: unknown
  traceId?: string | null
}

/** Tars error envelope. */
export interface TarsErrorEnvelope {
  success: false
  errorCode: string | null
  errorMessage: string | null
  fieldErrors: TarsFieldError[] | null
  traceId: string | null
}

export function isSuccessEnvelope(value: unknown): value is TarsSuccessEnvelope<unknown> {
  return (
    value !== null &&
    typeof value === 'object' &&
    (value as { success?: unknown }).success === true &&
    'data' in value
  )
}

export function isErrorEnvelope(value: unknown): value is TarsErrorEnvelope {
  return (
    value !== null && typeof value === 'object' && (value as { success?: unknown }).success === false
  )
}

export class ApiResponseError extends Error {
  readonly code: string | null
  readonly fieldErrors: TarsFieldError[] | null
  readonly traceId: string | null

  constructor(
    message: string,
    code: string | null = null,
    fieldErrors: TarsFieldError[] | null = null,
    traceId: string | null = null,
  ) {
    super(message)
    this.name = 'ApiResponseError'
    this.code = code
    this.fieldErrors = fieldErrors
    this.traceId = traceId
  }

  /** Most useful message to display to the user. */
  get firstMessage(): string {
    return this.fieldErrors?.[0]?.message ?? this.message
  }
}

export function errorFromEnvelope(env: TarsErrorEnvelope): ApiResponseError {
  const message = env.errorMessage ?? env.fieldErrors?.[0]?.message ?? 'Request failed.'
  return new ApiResponseError(message, env.errorCode, env.fieldErrors, env.traceId)
}

/** Extracts a readable error message from any caught error. */
export function toErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiResponseError) return err.firstMessage
  if (err instanceof Error && err.message) return err.message
  return fallback
}
