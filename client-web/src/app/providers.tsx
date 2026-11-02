import { useEffect, useMemo, type ReactNode } from 'react'
import { StyleProvider } from '@ant-design/cssinjs'
import { App as AntdApp, ConfigProvider } from 'antd'
import ptBR from 'antd/locale/pt_BR'
import enUS from 'antd/locale/en_US'
import dayjs from 'dayjs'
import 'dayjs/locale/pt-br'
import updateLocale from 'dayjs/plugin/updateLocale'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { queryClient } from '@/lib/queryClient'
import { AuthProvider } from '@/modules/identity/context/AuthProvider'
import { PreferencesProvider } from '@/modules/identity/context/PreferencesProvider'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { appTheme } from './theme'

dayjs.extend(updateLocale)

// antd's pickers and calendars read dayjs' locale: its language and the first day of the week. Set
// during render, not in an effect, so the first paint already has them.
function syncDayjsLocale(language: string, weekStartsOn: string) {
  const code = language === 'en' ? 'en' : 'pt-br'
  dayjs.locale(code)
  dayjs.updateLocale(code, { weekStart: weekStartsOn === 'monday' ? 1 : 0 })
}

function ThemedApp({ children }: { children: ReactNode }) {
  const { isDark, language, palette, accentColor, weekStartsOn } = usePreferences()
  const theme = useMemo(() => appTheme({ isDark, palette, accentColor }), [isDark, palette, accentColor])
  syncDayjsLocale(language, weekStartsOn)

  // Scrollbars and native controls are the browser's: they follow this, not the antd theme.
  useEffect(() => {
    document.documentElement.style.colorScheme = isDark ? 'dark' : 'light'
  }, [isDark])

  return (
    <ConfigProvider
      locale={language === 'en' ? enUS : ptBR}
      theme={theme}
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
