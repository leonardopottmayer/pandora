import { createContext, useContext } from 'react'
import type { AppLanguage, AppTheme } from '../models'

export interface PreferencesContextValue {
  theme: AppTheme
  setTheme: (theme: AppTheme) => void
  /** true when the resolved theme (accounting for 'system') is dark. */
  isDark: boolean
  language: AppLanguage
  setLanguage: (language: AppLanguage) => void
}

export const PreferencesContext = createContext<PreferencesContextValue | null>(null)

export function usePreferences(): PreferencesContextValue {
  const ctx = useContext(PreferencesContext)
  if (!ctx) throw new Error('usePreferences must be used within a PreferencesProvider')
  return ctx
}
