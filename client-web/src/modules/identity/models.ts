export interface Tokens {
  accessToken: string
  accessTokenExpiresAt: number
  refreshToken: string
  refreshTokenExpiresAt: string
}

export interface MfaChallenge {
  ticket: string
  expiresAt: string
}

/** Sign-in response: either tokens (no MFA) or an MFA challenge. */
export interface SignInResult {
  tokens: Tokens | null
  mfa: MfaChallenge | null
}

export interface SignUpRequest {
  name: string
  username: string
  email: string
  password: string
}

/** Current user (future GET /api/v1/identity/me). */
export interface CurrentUser {
  id: string
  name: string
  email: string
  username: string
}

export interface MfaStatus {
  enabled: boolean
  remainingRecoveryCodes: number
}

export interface MfaSetup {
  secret: string
  otpauthUri: string
}

export interface RecoveryCodes {
  recoveryCodes: string[]
}

export type AppTheme = 'light' | 'dark' | 'system'
export type AppLanguage = 'pt-BR' | 'en'

export interface UserPreferences {
  theme: AppTheme
  language: AppLanguage
}
