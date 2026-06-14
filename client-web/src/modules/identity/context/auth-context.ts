import { createContext, useContext } from 'react'
import type { CurrentUser, SignInResult } from '../models'

export interface AuthContextValue {
  user: CurrentUser | null
  /** Display label: real name (when /me exists) or the typed login. */
  loginLabel: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (emailOrUsername: string, password: string) => Promise<SignInResult>
  completeMfa: (ticket: string, code: string) => Promise<void>
  logout: () => Promise<void>
  reloadUser: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
