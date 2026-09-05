export type ConfirmationLevel = 'strict' | 'balanced' | 'trusting'

/** A user's assistant configuration. Holds no secret — the API key lives in Integrations. */
export interface AssistantProfile {
  provider: string
  model: string
  isEnabled: boolean
  localeOverride: string | null
  confirmationLevel: ConfirmationLevel
}

/** A chat provider the assistant supports, and whether the user has a key stored for it. */
export interface AssistantProvider {
  provider: string
  displayName: string
  keyConfigured: boolean
  /** A non-secret masked hint (e.g. the last four characters), or null when no key is configured. */
  keyHint: string | null
}

/** How one interpretation ended. Mirrors the backend's InvocationStatus. */
export type InvocationStatus =
  | 'executed'
  | 'failed'
  | 'clarification'
  | 'rejected'
  | 'provider-error'
  | 'pending-confirmation'
  | 'cancelled'
  | 'expired'

/** What one /interpret (or confirm/cancel) call produced. */
export interface InterpretResult {
  invocationId: string
  conversationId: string
  status: InvocationStatus
  commandName: string | null
  /** The exact tool-call arguments as raw JSON, or null when the model produced none. */
  arguments: string | null
  message: string
}

/** One row of the assistant's audit trail. */
export interface Invocation {
  id: string
  conversationId: string
  utterance: string
  commandName: string | null
  arguments: string | null
  status: InvocationStatus
  result: string | null
  error: string | null
  provider: string
  model: string
  latencyMs: number
  promptTokens: number
  completionTokens: number
  expiresAt: string | null
  createdAt: string
}

export type ReachabilityErrorKind = 'no_key' | 'rejected' | 'unreachable'

/** The outcome of a reachability probe: one minimal round-trip to the provider with the user's key. */
export interface ReachabilityResult {
  ok: boolean
  latencyMs: number
  reply: string | null
  error: string | null
  errorKind: ReachabilityErrorKind | null
}
