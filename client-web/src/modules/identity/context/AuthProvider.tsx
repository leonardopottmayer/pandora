import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { getAccessToken, getLoginLabel, setLoginLabel } from '@/lib/api/tokenStorage'
import { setOnUnauthorized } from '@/lib/api/client'
import * as authService from '../services/auth.service'
import * as usersService from '../services/users.service'
import type { CurrentUser, SignInResult } from '../models'
import { AuthContext, type AuthContextValue } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loginLabel, setLabel] = useState<string | null>(getLoginLabel())
  const [isAuthenticated, setIsAuthenticated] = useState(() => getAccessToken() != null)
  const [isLoading, setIsLoading] = useState(true)

  const reloadUser = useCallback(async () => {
    if (!getAccessToken()) {
      setUser(null)
      return
    }
    try {
      setUser(await usersService.getMe())
    } catch {
      setUser(null)
    }
  }, [])

  // Initial load: validates an existing session.
  useEffect(() => {
    let active = true
    async function init() {
      await reloadUser()
      if (active) setIsLoading(false)
    }
    void init()
    return () => {
      active = false
    }
  }, [reloadUser])

  // When the refresh fails, the client notifies us: tear down the session.
  useEffect(() => {
    setOnUnauthorized(() => {
      setIsAuthenticated(false)
      setUser(null)
    })
    return () => setOnUnauthorized(null)
  }, [])

  const login = useCallback(
    async (emailOrUsername: string, password: string): Promise<SignInResult> => {
      const result = await authService.signIn(emailOrUsername, password)
      setLoginLabel(emailOrUsername)
      setLabel(emailOrUsername)
      if (result.tokens) {
        setIsAuthenticated(true)
        await reloadUser()
      }
      return result
    },
    [reloadUser],
  )

  const completeMfa = useCallback(
    async (ticket: string, code: string) => {
      await authService.completeMfaChallenge(ticket, code)
      setIsAuthenticated(true)
      await reloadUser()
    },
    [reloadUser],
  )

  const logout = useCallback(async () => {
    await authService.signOut()
    setIsAuthenticated(false)
    setUser(null)
    setLabel(null)
  }, [])

  const value: AuthContextValue = {
    user,
    loginLabel,
    isAuthenticated,
    isLoading,
    login,
    completeMfa,
    logout,
    reloadUser,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
