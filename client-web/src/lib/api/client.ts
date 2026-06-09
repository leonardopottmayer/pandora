import axios, { type AxiosError } from 'axios'
import { getAccessToken, getRefreshToken, setTokens, clearTokens } from './tokenStorage'
import { isSuccessEnvelope, isErrorEnvelope, errorFromEnvelope } from './envelope'
import i18n from '@/i18n'

function getBaseURL(): string {
  const fromEnv = import.meta.env.VITE_API_URL
  if (fromEnv && fromEnv.trim() !== '') return fromEnv.trim()
  if (import.meta.env.DEV) return 'https://localhost:61182'
  return ''
}

const baseURL = getBaseURL()

export const apiClient = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
})

let onUnauthorized: (() => void) | null = null

export function setOnUnauthorized(callback: (() => void) | null): void {
  onUnauthorized = callback
}

// Every request carries the Bearer token when present and the current language
// (so the backend can localize error messages via Accept-Language).
apiClient.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  config.headers['Accept-Language'] = i18n.language
  return config
})

interface TokenPayload {
  accessToken: string
  refreshToken: string
}

/** Reads the TokenDto from a body that may or may not be wrapped in the Tars envelope. */
function readTokenPayload(data: unknown): TokenPayload | null {
  const body = isSuccessEnvelope(data) ? data.data : data
  if (body && typeof body === 'object' && 'accessToken' in body) {
    const t = body as { accessToken?: string; refreshToken?: string }
    if (t.accessToken) return { accessToken: t.accessToken, refreshToken: t.refreshToken ?? '' }
  }
  return null
}

// Unwraps the Tars envelope on success responses (payload in `data`).
function unwrapResponse(response: { data: unknown }): void {
  if (isSuccessEnvelope(response.data)) {
    response.data = response.data.data
  }
}

// Ensures a single concurrent refresh; simultaneous 401s await the same result.
let activeRefreshPromise: Promise<string | null> | null = null

async function doRefresh(): Promise<string | null> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    clearTokens()
    onUnauthorized?.()
    return null
  }
  try {
    const res = await axios.post(
      `${baseURL}/api/v1/identity/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' }, validateStatus: () => true },
    )
    if (res.status !== 200) {
      clearTokens()
      onUnauthorized?.()
      return null
    }
    const tokens = readTokenPayload(res.data)
    if (tokens) {
      setTokens(tokens.accessToken, tokens.refreshToken || undefined)
      return tokens.accessToken
    }
    clearTokens()
    onUnauthorized?.()
    return null
  } catch {
    clearTokens()
    onUnauthorized?.()
    return null
  }
}

apiClient.interceptors.response.use(
  (response) => {
    unwrapResponse(response)
    return response
  },
  async (error: AxiosError) => {
    // 4xx/5xx errors with envelope: reject with ApiResponseError (friendly message).
    const data = error.response?.data
    if (isErrorEnvelope(data)) {
      return Promise.reject(errorFromEnvelope(data))
    }

    const originalRequest = error.config as (typeof error.config & { _retry?: boolean }) | undefined
    if (error.response?.status !== 401 || !originalRequest || originalRequest._retry) {
      return Promise.reject(error)
    }

    if (!activeRefreshPromise) {
      activeRefreshPromise = doRefresh().finally(() => {
        activeRefreshPromise = null
      })
    }

    const newAccess = await activeRefreshPromise
    if (newAccess) {
      originalRequest.headers.Authorization = `Bearer ${newAccess}`
      originalRequest._retry = true
      return apiClient(originalRequest)
    }
    return Promise.reject(error)
  },
)
