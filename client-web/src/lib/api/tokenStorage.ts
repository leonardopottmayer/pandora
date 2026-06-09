const ACCESS_TOKEN_KEY = 'pandora.access_token'
const REFRESH_TOKEN_KEY = 'pandora.refresh_token'
// Rotulo digitado no login (email/username). Fallback de exibicao enquanto o
// backend nao expoe um GET /me com nome/email.
const LOGIN_LABEL_KEY = 'pandora.login_label'

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY)
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function setTokens(accessToken: string, refreshToken?: string | null): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, accessToken)
  if (refreshToken != null) {
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
  }
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(LOGIN_LABEL_KEY)
}

export function getLoginLabel(): string | null {
  return localStorage.getItem(LOGIN_LABEL_KEY)
}

export function setLoginLabel(label: string): void {
  localStorage.setItem(LOGIN_LABEL_KEY, label)
}
