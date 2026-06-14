import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import ptBR from './locales/pt-BR.json'
import en from './locales/en.json'
import type { AppLanguage } from '@/modules/identity/models'

const LANGUAGE_KEY = 'pandora.language'
export const SUPPORTED_LANGUAGES: AppLanguage[] = ['pt-BR', 'en']

// Default language: English. Uses the one saved on the device when present.
export function getStoredLanguage(): AppLanguage {
  const stored = localStorage.getItem(LANGUAGE_KEY)
  if (stored && (SUPPORTED_LANGUAGES as string[]).includes(stored)) {
    return stored as AppLanguage
  }
  return 'en'
}

export function storeLanguage(language: AppLanguage): void {
  localStorage.setItem(LANGUAGE_KEY, language)
}

i18n.use(initReactI18next).init({
  resources: {
    'pt-BR': { translation: ptBR },
    en: { translation: en },
  },
  lng: getStoredLanguage(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

export default i18n
