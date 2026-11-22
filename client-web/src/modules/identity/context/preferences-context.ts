import { createContext, useContext } from 'react'
import type { AppLanguage, AppPalette, AppTheme, WeekStartsOn } from '../models'

export interface PreferencesContextValue {
  theme: AppTheme
  setTheme: (theme: AppTheme) => void
  /** true when the resolved theme (accounting for 'system') is dark. */
  isDark: boolean
  language: AppLanguage
  setLanguage: (language: AppLanguage) => void
  timeZone: string
  setTimeZone: (timeZone: string) => void
  weekStartsOn: WeekStartsOn
  setWeekStartsOn: (weekStartsOn: WeekStartsOn) => void
  palette: AppPalette
  setPalette: (palette: AppPalette) => void
  accentColor: string | null
  setAccentColor: (accentColor: string | null) => void
}

export const PreferencesContext = createContext<PreferencesContextValue | null>(null)

export function usePreferences(): PreferencesContextValue {
  const ctx = useContext(PreferencesContext)
  if (!ctx) throw new Error('usePreferences must be used within a PreferencesProvider')
  return ctx
}
