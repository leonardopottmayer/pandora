import {
  AppstoreOutlined,
  HomeOutlined,
  SettingOutlined,
  SafetyOutlined,
  WalletOutlined,
  BankOutlined,
  SwapOutlined,
  CreditCardOutlined,
  AppstoreAddOutlined,
  TagsOutlined,
  AuditOutlined,
  RetweetOutlined,
  InboxOutlined,
  CloudUploadOutlined,
  LayoutOutlined,
} from '@ant-design/icons'
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
    key: 'finances',
    labelKey: 'nav.finances',
    icon: <WalletOutlined />,
    screens: [
      { key: 'fin-accounts', labelKey: 'nav.accounts', path: '/finances/accounts', icon: <BankOutlined /> },
      { key: 'fin-transactions', labelKey: 'nav.transactions', path: '/finances/transactions', icon: <SwapOutlined /> },
      { key: 'fin-recurring', labelKey: 'nav.recurring', path: '/finances/recurring', icon: <RetweetOutlined /> },
      { key: 'fin-inbox', labelKey: 'nav.inbox', path: '/finances/inbox', icon: <InboxOutlined /> },
      { key: 'fin-cards', labelKey: 'nav.cards', path: '/finances/cards', icon: <CreditCardOutlined /> },
      { key: 'fin-categories', labelKey: 'nav.categories', path: '/finances/categories', icon: <AppstoreAddOutlined /> },
      { key: 'fin-tags', labelKey: 'nav.tags', path: '/finances/tags', icon: <TagsOutlined /> },
      { key: 'fin-audit', labelKey: 'nav.audit', path: '/finances/audit', icon: <AuditOutlined /> },
      { key: 'fin-imports', labelKey: 'nav.imports', path: '/finances/imports', icon: <CloudUploadOutlined /> },
      { key: 'fin-import-layouts', labelKey: 'nav.importLayouts', path: '/finances/import-layouts', icon: <LayoutOutlined /> },
    ],
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
