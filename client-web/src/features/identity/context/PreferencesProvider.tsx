import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import * as preferencesService from '../services/preferences.service'
import type { AppLanguage, AppTheme } from '../models'
import { useAuth } from './auth-context'
import { PreferencesContext } from './preferences-context'
import i18n, { getStoredLanguage, storeLanguage } from '@/i18n'

const SAVE_DEBOUNCE_MS = 800

function prefersDark(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const [theme, setThemeState] = useState<AppTheme>('system')
  const [language, setLanguageState] = useState<AppLanguage>(getStoredLanguage)
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const applyLanguage = useCallback((next: AppLanguage) => {
    setLanguageState(next)
    storeLanguage(next)
    void i18n.changeLanguage(next)
  }, [])

  // Carrega preferencias do backend ao autenticar.
  useEffect(() => {
    if (!isAuthenticated) return
    let cancelled = false
    preferencesService
      .getPreferences()
      .then((prefs) => {
        if (cancelled) return
        if (prefs.theme) setThemeState(prefs.theme)
        if (prefs.language) applyLanguage(prefs.language)
      })
      .catch(() => {
        /* mantem os valores atuais em caso de erro */
      })
    return () => {
      cancelled = true
    }
  }, [isAuthenticated, applyLanguage])

  // Persiste o par (theme, language) na conta, com debounce.
  const persist = useCallback(
    (nextTheme: AppTheme, nextLanguage: AppLanguage) => {
      if (!isAuthenticated) return
      if (saveTimer.current) clearTimeout(saveTimer.current)
      saveTimer.current = setTimeout(() => {
        preferencesService.upsertPreferences(nextTheme, nextLanguage).catch(() => {
          /* ja aplicado localmente; ignora erro de persistencia */
        })
      }, SAVE_DEBOUNCE_MS)
    },
    [isAuthenticated],
  )

  const setTheme = useCallback(
    (next: AppTheme) => {
      setThemeState(next)
      persist(next, language)
    },
    [persist, language],
  )

  const setLanguage = useCallback(
    (next: AppLanguage) => {
      applyLanguage(next)
      persist(theme, next)
    },
    [applyLanguage, persist, theme],
  )

  const isDark = theme === 'dark' || (theme === 'system' && prefersDark())

  return (
    <PreferencesContext.Provider value={{ theme, setTheme, isDark, language, setLanguage }}>
      {children}
    </PreferencesContext.Provider>
  )
}
