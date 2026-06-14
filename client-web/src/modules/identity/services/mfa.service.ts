import { apiClient } from '@/lib/api/client'
import type { MfaSetup, MfaStatus, RecoveryCodes } from '../models'

const MFA_BASE = '/api/v1/identity/mfa'

export async function getMfaStatus(): Promise<MfaStatus> {
  const { data } = await apiClient.get<MfaStatus>(`${MFA_BASE}/status`)
  return data
}

/** Starts enrollment: returns the Base32 secret and the otpauth URI (QR Code). */
export async function setupMfa(): Promise<MfaSetup> {
  const { data } = await apiClient.post<MfaSetup>(`${MFA_BASE}/setup`)
  return data
}

/** Confirms enrollment with a TOTP code; returns the recovery codes (once). */
export async function enableMfa(code: string): Promise<RecoveryCodes> {
  const { data } = await apiClient.post<RecoveryCodes>(`${MFA_BASE}/enable`, { code })
  return data
}

export async function disableMfa(password: string, code: string): Promise<void> {
  await apiClient.post(`${MFA_BASE}/disable`, { password, code })
}

export async function regenerateRecoveryCodes(password: string, code: string): Promise<RecoveryCodes> {
  const { data } = await apiClient.post<RecoveryCodes>(`${MFA_BASE}/recovery-codes`, { password, code })
  return data
}
