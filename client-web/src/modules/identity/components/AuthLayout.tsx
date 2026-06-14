import type { ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { LanguageSwitcher } from './LanguageSwitcher'
import './AuthLayout.css'

export function AuthLayout({ children }: { children: ReactNode }) {
  const { pathname } = useLocation()
  const { t } = useTranslation()

  return (
    <div className="auth-layout">
      <aside className="auth-layout__branding">
        <div className="auth-layout__branding-hero">
          <div className="auth-layout__branding-logo" aria-hidden>
            P
          </div>
          <h1 className="auth-layout__branding-title">{t('common.appName')}</h1>
          <p className="auth-layout__branding-desc">{t('authLayout.desc')}</p>
        </div>
      </aside>
      <main className="auth-layout__form-panel">
        <div className="auth-layout__form-inner">
          <div className="mb-4 flex justify-end">
            <LanguageSwitcher />
          </div>
          <div key={pathname} className="auth-layout__form-animated">
            {children}
          </div>
        </div>
      </main>
    </div>
  )
}
