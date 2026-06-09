import type { ReactNode } from 'react'
import { StyleProvider } from '@ant-design/cssinjs'
import { App as AntdApp, ConfigProvider, theme as antdTheme } from 'antd'
import ptBR from 'antd/locale/pt_BR'
import enUS from 'antd/locale/en_US'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { queryClient } from '@/lib/queryClient'
import { AuthProvider } from '@/features/identity/context/AuthProvider'
import { PreferencesProvider } from '@/features/identity/context/PreferencesProvider'
import { usePreferences } from '@/features/identity/context/preferences-context'

function ThemedApp({ children }: { children: ReactNode }) {
  const { isDark, language } = usePreferences()
  return (
    <ConfigProvider
      locale={language === 'en' ? enUS : ptBR}
      theme={{
        algorithm: isDark ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
        token: { borderRadius: 6 },
      }}
    >
      {/* App: context for message/notification/modal with the theme applied */}
      <AntdApp className="h-full">
        {children}
        <ReactQueryDevtools initialIsOpen={false} />
      </AntdApp>
    </ConfigProvider>
  )
}

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    // layer: emits antd styles into the @layer antd (see src/index.css)
    <StyleProvider layer>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <PreferencesProvider>
            <ThemedApp>{children}</ThemedApp>
          </PreferencesProvider>
        </AuthProvider>
      </QueryClientProvider>
    </StyleProvider>
  )
}
