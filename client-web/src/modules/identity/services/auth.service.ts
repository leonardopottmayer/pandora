import { apiClient } from '@/lib/api/client'
import { setTokens, clearTokens, getRefreshToken } from '@/lib/api/tokenStorage'
import type { SignInResult, SignUpRequest, Tokens } from '../models'

const AUTH_BASE = '/api/v1/identity/auth'
const MFA_BASE = '/api/v1/identity/mfa'

export async function signIn(emailOrUsername: string, password: string): Promise<SignInResult> {
  const { data } = await apiClient.post<SignInResult>(`${AUTH_BASE}/signin`, {
    emailOrUsername,
    password,
  })
  if (data.tokens) {
    setTokens(data.tokens.accessToken, data.tokens.refreshToken)
  }
  return data
}

/** Completes the MFA challenge with the TOTP code (or a recovery code). */
export async function completeMfaChallenge(ticket: string, code: string): Promise<Tokens> {
  const { data } = await apiClient.post<Tokens>(`${MFA_BASE}/challenge`, { ticket, code })
  setTokens(data.accessToken, data.refreshToken)
  return data
}

export async function signUp(request: SignUpRequest): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/signup`, request)
}

export async function activateAccount(token: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/activate`, { token })
}

export async function forgotPassword(email: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/password/forgot`, { email })
}

export async function resetPassword(token: string, newPassword: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/password/reset`, { token, newPassword })
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/password/change`, { currentPassword, newPassword })
}

export async function signOut(): Promise<void> {
  const refreshToken = getRefreshToken()
  try {
    await apiClient.post(`${AUTH_BASE}/signout`, { refreshToken: refreshToken ?? '' })
  } finally {
    clearTokens()
  }
}
