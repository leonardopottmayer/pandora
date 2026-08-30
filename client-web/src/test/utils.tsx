import type { ReactElement, ReactNode } from 'react'
import { render, type RenderOptions } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { App as AntdApp, ConfigProvider } from 'antd'
import { I18nextProvider } from 'react-i18next'
import i18n from '@/i18n'
import { PreferencesContext, type PreferencesContextValue } from '@/modules/identity/context/preferences-context'

/** A static preferences context so components using `usePreferences()` render without the provider. */
const testPreferences: PreferencesContextValue = {
  theme: 'light',
  setTheme: () => {},
  isDark: false,
  language: 'en',
  setLanguage: () => {},
  timeZone: 'America/Sao_Paulo',
  setTimeZone: () => {},
  weekStartsOn: 'sunday',
  setWeekStartsOn: () => {},
  defaultAlertOffsetMinutes: -15,
  setDefaultAlertOffsetMinutes: () => {},
}

/** Fresh client per test: no retries, no caching across tests. */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  })
}

interface RenderWithProvidersOptions extends Omit<RenderOptions, 'wrapper'> {
  route?: string
  queryClient?: QueryClient
}

/** Minimal QueryClient-only wrapper for hook tests, plus a spy on invalidateQueries. */
export function createHookWrapper(client = createTestQueryClient()) {
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
  return { client, wrapper: Wrapper }
}

/** Renders `ui` wrapped in the providers a finances page expects (Query, Router, antd App, i18n). */
export function renderWithProviders(
  ui: ReactElement,
  { route = '/', queryClient, ...options }: RenderWithProvidersOptions = {},
) {
  const client = queryClient ?? createTestQueryClient()

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <I18nextProvider i18n={i18n}>
        <QueryClientProvider client={client}>
          <ConfigProvider>
            <AntdApp>
              <PreferencesContext.Provider value={testPreferences}>
                <MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
              </PreferencesContext.Provider>
            </AntdApp>
          </ConfigProvider>
        </QueryClientProvider>
      </I18nextProvider>
    )
  }

  return { client, ...render(ui, { wrapper: Wrapper, ...options }) }
}
