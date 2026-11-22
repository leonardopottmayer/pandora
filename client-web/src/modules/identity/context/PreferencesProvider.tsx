import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { ApiResponseError } from '@/lib/api/envelope'
import * as preferencesService from '../services/preferences.service'
import type { AppLanguage, AppPalette, AppTheme, UserPreferences, WeekStartsOn } from '../models'
import { useAuth } from './auth-context'
import { PreferencesContext } from './preferences-context'
import i18n, { getStoredLanguage, storeLanguage } from '@/i18n'

const SAVE_DEBOUNCE_MS = 800
const APPEARANCE_KEY = 'pandora.appearance'

interface Appearance {
  theme: AppTheme
  palette: AppPalette
  accentColor: string | null
}

/** Kept here too, so a page load does not flash another look until the account answers. */
function storedAppearance(): Appearance {
  try {
    const stored = localStorage.getItem(APPEARANCE_KEY)
    if (stored) return JSON.parse(stored) as Appearance
  } catch {
    // Unreadable is the same as nothing stored.
  }
  return { theme: 'system', palette: 'default', accentColor: null }
}

function storeAppearance(appearance: Appearance): void {
  try {
    localStorage.setItem(APPEARANCE_KEY, JSON.stringify(appearance))
  } catch {
    // Without storage, the account's choice just arrives a moment later.
  }
}

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
  const [initial] = useState(storedAppearance)
  const [theme, setThemeState] = useState<AppTheme>(initial.theme)
  const [palette, setPaletteState] = useState<AppPalette>(initial.palette)
  const [accentColor, setAccentColorState] = useState<string | null>(initial.accentColor)
  const [language, setLanguageState] = useState<AppLanguage>(getStoredLanguage)
  const [timeZone, setTimeZoneState] = useState<string>(browserTimeZone)
  const [weekStartsOn, setWeekStartsOnState] = useState<WeekStartsOn>('sunday')
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Mirrors the latest values so the debounced persist always sends a complete,
  // fresh snapshot without re-creating the callback on every field change.
  const latest = useRef<UserPreferences>({
    theme,
    language,
    timeZone,
    weekStartsOn,
    palette,
    accentColor,
  })

  const rememberAppearance = useCallback(() => {
    const { theme, palette, accentColor } = latest.current
    storeAppearance({ theme, palette, accentColor })
  }, [])

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
        if (prefs.palette) {
          setPaletteState(prefs.palette)
          latest.current.palette = prefs.palette
        }
        if (prefs.accentColor !== undefined) {
          setAccentColorState(prefs.accentColor)
          latest.current.accentColor = prefs.accentColor
        }
        rememberAppearance()
      })
      .catch((err) => {
        if (cancelled) return
        // No preferences row yet: seed the account with the browser-detected zone (and the current
        // local defaults) so a real time zone is persisted on first authenticated load, instead of the
        // backend forever falling back to its default. Only on this specific case — a transient error
        // must not overwrite the account with defaults.
        if (err instanceof ApiResponseError && err.code === 'Users.PreferencesNotFound') {
          preferencesService.upsertPreferences({ ...latest.current }).catch(() => {
            /* best-effort seed; the next login retries */
          })
        }
        /* otherwise keeps the current values on error */
      })
    return () => {
      cancelled = true
    }
  }, [isAuthenticated, applyLanguage, rememberAppearance])

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
      rememberAppearance()
      persist()
    },
    [persist, rememberAppearance],
  )

  const setPalette = useCallback(
    (next: AppPalette) => {
      setPaletteState(next)
      latest.current.palette = next
      rememberAppearance()
      persist()
    },
    [persist, rememberAppearance],
  )

  const setAccentColor = useCallback(
    (next: string | null) => {
      setAccentColorState(next)
      latest.current.accentColor = next
      rememberAppearance()
      persist()
    },
    [persist, rememberAppearance],
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
        palette,
        setPalette,
        accentColor,
        setAccentColor,
      }}
    >
      {children}
    </PreferencesContext.Provider>
  )
}
