import { AppstoreOutlined, HomeOutlined, SettingOutlined, SafetyOutlined } from '@ant-design/icons'
import type { ReactNode } from 'react'

export interface NavScreen {
  key: string
  labelKey: string
  path: string
  icon?: ReactNode
}

export interface NavModule {
  key: string
  labelKey: string
  icon: ReactNode
  screens: NavScreen[]
}

// Apenas modulos com endpoints reais no backend. labelKey e traduzido no AppLayout.
export const navigationModules: NavModule[] = [
  {
    key: 'dashboard',
    labelKey: 'nav.start',
    icon: <AppstoreOutlined />,
    screens: [{ key: 'home', labelKey: 'nav.home', path: '/', icon: <HomeOutlined /> }],
  },
  {
    key: 'account',
    labelKey: 'nav.account',
    icon: <SafetyOutlined />,
    screens: [
      { key: 'profile', labelKey: 'nav.profile', path: '/account', icon: <HomeOutlined /> },
      { key: 'security', labelKey: 'nav.security', path: '/account/security', icon: <SafetyOutlined /> },
      { key: 'settings', labelKey: 'nav.settings', path: '/settings', icon: <SettingOutlined /> },
    ],
  },
]
