import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import * as preferencesService from '../services/preferences.service'
import type { AppLanguage, AppTheme, UserPreferences, WeekStartsOn } from '../models'
import { useAuth } from './auth-context'
import { PreferencesContext } from './preferences-context'
import i18n, { getStoredLanguage, storeLanguage } from '@/i18n'

const SAVE_DEBOUNCE_MS = 800

function prefersDark(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches
}

function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Sao_Paulo'
  } catch {
    return 'America/Sao_Paulo'
  }
}

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const [theme, setThemeState] = useState<AppTheme>('system')
  const [language, setLanguageState] = useState<AppLanguage>(getStoredLanguage)
  const [timeZone, setTimeZoneState] = useState<string>(browserTimeZone)
  const [weekStartsOn, setWeekStartsOnState] = useState<WeekStartsOn>('sunday')
  const [defaultAlertOffsetMinutes, setDefaultAlertOffsetMinutesState] = useState<number>(-15)
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Mirrors the latest values so the debounced persist always sends a complete,
  // fresh snapshot without re-creating the callback on every field change.
  const latest = useRef<UserPreferences>({
    theme,
    language,
    timeZone,
    weekStartsOn,
    defaultAlertOffsetMinutes,
  })

  const applyLanguage = useCallback((next: AppLanguage) => {
    setLanguageState(next)
    latest.current.language = next
    storeLanguage(next)
    void i18n.changeLanguage(next)
  }, [])

  // Loads preferences from the backend on authentication.
  useEffect(() => {
    if (!isAuthenticated) return
    let cancelled = false
    preferencesService
      .getPreferences()
      .then((prefs) => {
        if (cancelled) return
        if (prefs.theme) {
          setThemeState(prefs.theme)
          latest.current.theme = prefs.theme
        }
        if (prefs.language) applyLanguage(prefs.language)
        if (prefs.timeZone) {
          setTimeZoneState(prefs.timeZone)
          latest.current.timeZone = prefs.timeZone
        }
        if (prefs.weekStartsOn) {
          setWeekStartsOnState(prefs.weekStartsOn)
          latest.current.weekStartsOn = prefs.weekStartsOn
        }
        if (typeof prefs.defaultAlertOffsetMinutes === 'number') {
          setDefaultAlertOffsetMinutesState(prefs.defaultAlertOffsetMinutes)
          latest.current.defaultAlertOffsetMinutes = prefs.defaultAlertOffsetMinutes
        }
      })
      .catch(() => {
        /* keeps the current values on error */
      })
    return () => {
      cancelled = true
    }
  }, [isAuthenticated, applyLanguage])

  // Persists the full preferences snapshot to the account, with debounce.
  const persist = useCallback(() => {
    if (!isAuthenticated) return
    if (saveTimer.current) clearTimeout(saveTimer.current)
    saveTimer.current = setTimeout(() => {
      preferencesService.upsertPreferences({ ...latest.current }).catch(() => {
        /* already applied locally; ignore persistence error */
      })
    }, SAVE_DEBOUNCE_MS)
  }, [isAuthenticated])

  const setTheme = useCallback(
    (next: AppTheme) => {
      setThemeState(next)
      latest.current.theme = next
      persist()
    },
    [persist],
  )

  const setLanguage = useCallback(
    (next: AppLanguage) => {
      applyLanguage(next)
      persist()
    },
    [applyLanguage, persist],
  )

  const setTimeZone = useCallback(
    (next: string) => {
      setTimeZoneState(next)
      latest.current.timeZone = next
      persist()
    },
    [persist],
  )

  const setWeekStartsOn = useCallback(
    (next: WeekStartsOn) => {
      setWeekStartsOnState(next)
      latest.current.weekStartsOn = next
      persist()
    },
    [persist],
  )

  const setDefaultAlertOffsetMinutes = useCallback(
    (next: number) => {
      setDefaultAlertOffsetMinutesState(next)
      latest.current.defaultAlertOffsetMinutes = next
      persist()
    },
    [persist],
  )

  const isDark = theme === 'dark' || (theme === 'system' && prefersDark())

  return (
    <PreferencesContext.Provider
      value={{
        theme,
        setTheme,
        isDark,
        language,
        setLanguage,
        timeZone,
        setTimeZone,
        weekStartsOn,
        setWeekStartsOn,
        defaultAlertOffsetMinutes,
        setDefaultAlertOffsetMinutes,
      }}
    >
      {children}
    </PreferencesContext.Provider>
  )
}
