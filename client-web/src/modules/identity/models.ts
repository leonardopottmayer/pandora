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
export type WeekStartsOn = 'sunday' | 'monday'

export interface UserPreferences {
  theme: AppTheme
  language: AppLanguage
  /** IANA time zone (e.g. "America/Sao_Paulo"). */
  timeZone: string
  weekStartsOn: WeekStartsOn
  /** Signed minutes, relative to the item's anchor (e.g. -15 = fifteen minutes before). */
  defaultAlertOffsetMinutes: number
}

/** A paired device (Pandora Desktop, a headless agent, a phone) — calls the API with its own key. */
export interface Device {
  id: string
  name: string
  platform: 'windows' | 'linux' | 'macos' | 'android' | 'ios'
  form: 'desktop' | 'headless' | 'mobile'
  scopes: string[]
  createdAt: string
  lastSeenAt: string | null
}

/** The pairing response — the only time the key is ever returned. */
export interface DeviceRegistration {
  device: Device
  key: string
}
