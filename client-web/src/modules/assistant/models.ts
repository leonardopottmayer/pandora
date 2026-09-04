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

export type ReachabilityErrorKind = 'no_key' | 'rejected' | 'unreachable'

/** The outcome of a reachability probe: one minimal round-trip to the provider with the user's key. */
export interface ReachabilityResult {
  ok: boolean
  latencyMs: number
  reply: string | null
  error: string | null
  errorKind: ReachabilityErrorKind | null
}
