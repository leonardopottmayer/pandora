import type { ReactElement, ReactNode } from 'react'
import { render, type RenderOptions } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { App as AntdApp, ConfigProvider } from 'antd'
import { I18nextProvider } from 'react-i18next'
import i18n from '@/i18n'

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
              <MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
            </AntdApp>
          </ConfigProvider>
        </QueryClientProvider>
      </I18nextProvider>
    )
  }

  return { client, ...render(ui, { wrapper: Wrapper, ...options }) }
}
