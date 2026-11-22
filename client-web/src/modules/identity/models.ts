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

/** Current user (GET /api/v1/identity/me). */
export interface CurrentUser {
  id: string
  name: string
  email: string
  username: string
  /** Feature flags switched on in the system. */
  features: string[]
  /** The user's effective permission keys (wildcards already expanded). */
  permissions: string[]
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
export type AppPalette = 'default' | 'claude' | 'reddit'
export type WeekStartsOn = 'sunday' | 'monday'

export interface UserPreferences {
  theme: AppTheme
  language: AppLanguage
  /** IANA time zone (e.g. "America/Sao_Paulo"). */
  timeZone: string
  weekStartsOn: WeekStartsOn
  palette: AppPalette
  /** #rrggbb, or null for the palette's own. */
  accentColor: string | null
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
