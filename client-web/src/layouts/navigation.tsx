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
  RobotOutlined,
  TagsOutlined,
  AuditOutlined,
  RetweetOutlined,
  InboxOutlined,
  CloudUploadOutlined,
  LayoutOutlined,
  BookOutlined,
  FileTextOutlined,
  PictureOutlined,
  ScheduleOutlined,
  CalendarOutlined,
  CheckSquareOutlined,
  BellOutlined,
  NotificationOutlined,
  ApiOutlined,
  LaptopOutlined,
  FolderOpenOutlined,
  HddOutlined,
  FilterOutlined,
  CheckSquareFilled,
  MailOutlined,
  UserOutlined,
  ControlOutlined,
  TeamOutlined,
  SafetyCertificateOutlined,
  HistoryOutlined,
} from '@ant-design/icons'
import type { ReactNode } from 'react'

/** A screen shows (in the menu and at its route) when its feature is on and the user holds its permission. */
export interface NavScreen {
  key: string
  labelKey: string
  path: string
  icon?: ReactNode
  /** Overrides the module's feature (Finances imports have their own flag). */
  feature?: string
  permission?: string
}

export interface NavModule {
  key: string
  labelKey: string
  icon: ReactNode
  /** The flag that switches the whole module off. A module with no screen left is hidden too. */
  feature?: string
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
    feature: 'finances',
    labelKey: 'nav.finances',
    icon: <WalletOutlined />,
    screens: [
      { key: 'fin-accounts', labelKey: 'nav.accounts', path: '/finances/accounts', icon: <BankOutlined />, permission: 'finances.accounts.read' },
      { key: 'fin-transactions', labelKey: 'nav.transactions', path: '/finances/transactions', icon: <SwapOutlined />, permission: 'finances.transactions.read' },
      { key: 'fin-recurring', labelKey: 'nav.recurring', path: '/finances/recurring', icon: <RetweetOutlined />, permission: 'finances.recurrences.read' },
      { key: 'fin-inbox', labelKey: 'nav.inbox', path: '/finances/inbox', icon: <InboxOutlined />, permission: 'finances.pending.read' },
      { key: 'fin-cards', labelKey: 'nav.cards', path: '/finances/cards', icon: <CreditCardOutlined />, permission: 'finances.cards.read' },
      { key: 'fin-categories', labelKey: 'nav.categories', path: '/finances/categories', icon: <AppstoreAddOutlined />, permission: 'finances.categories.read' },
      { key: 'fin-tags', labelKey: 'nav.tags', path: '/finances/tags', icon: <TagsOutlined />, permission: 'finances.tags.read' },
      { key: 'fin-audit', labelKey: 'nav.audit', path: '/finances/audit', icon: <AuditOutlined />, permission: 'finances.audit.read' },
      { key: 'fin-imports', labelKey: 'nav.imports', path: '/finances/imports', icon: <CloudUploadOutlined />, permission: 'finances.imports.read', feature: 'finances.import' },
      { key: 'fin-import-layouts', labelKey: 'nav.importLayouts', path: '/finances/import-layouts', icon: <LayoutOutlined />, permission: 'finances.imports.read', feature: 'finances.import' },
    ],
  },
  {
    key: 'agenda',
    feature: 'agenda',
    labelKey: 'nav.agenda',
    icon: <ScheduleOutlined />,
    screens: [
      { key: 'agenda-today', labelKey: 'nav.agendaToday', path: '/agenda', icon: <ScheduleOutlined />, permission: 'agenda.events.read' },
      { key: 'agenda-calendar', labelKey: 'nav.agendaCalendar', path: '/agenda/calendar', icon: <CalendarOutlined />, permission: 'agenda.events.read' },
      { key: 'agenda-tasks', labelKey: 'nav.agendaTasks', path: '/agenda/tasks', icon: <CheckSquareOutlined />, permission: 'agenda.tasks.read' },
      { key: 'agenda-reminders', labelKey: 'nav.agendaReminders', path: '/agenda/reminders', icon: <BellOutlined />, permission: 'agenda.reminders.read' },
      { key: 'agenda-settings', labelKey: 'nav.agendaSettings', path: '/agenda/settings', icon: <SettingOutlined />, permission: 'agenda.preferences.read' },
    ],
  },
  {
    key: 'notes',
    feature: 'notes',
    labelKey: 'nav.notes',
    icon: <BookOutlined />,
    screens: [
      { key: 'notes-spaces', labelKey: 'nav.notesSpaces', path: '/notes', icon: <FileTextOutlined />, permission: 'notes.spaces.read' },
      { key: 'notes-library', labelKey: 'nav.notesLibrary', path: '/notes/library', icon: <PictureOutlined />, permission: 'notes.media.read' },
    ],
  },
  {
    key: 'mail',
    feature: 'communications',
    labelKey: 'nav.mail',
    icon: <MailOutlined />,
    screens: [
      { key: 'mail-inbox', labelKey: 'nav.mailInbox', path: '/mail', icon: <MailOutlined />, permission: 'communications.messages.read' },
      { key: 'mail-accounts', labelKey: 'nav.mailAccounts', path: '/mail/accounts', icon: <UserOutlined />, permission: 'communications.mailboxes.read' },
      { key: 'mail-rules', labelKey: 'nav.mailRules', path: '/mail/rules', icon: <FilterOutlined />, permission: 'communications.rules.read' },
    ],
  },
  {
    key: 'files',
    feature: 'files',
    labelKey: 'nav.files',
    icon: <FolderOpenOutlined />,
    screens: [
      { key: 'files-catalog', labelKey: 'nav.filesCatalog', path: '/files', icon: <FolderOpenOutlined />, permission: 'files.entries.read' },
      { key: 'files-roots', labelKey: 'nav.filesRoots', path: '/files/roots', icon: <HddOutlined />, permission: 'files.roots.read' },
      { key: 'files-filters', labelKey: 'nav.filesFilters', path: '/files/filters', icon: <FilterOutlined />, permission: 'files.filters.read' },
      { key: 'files-review', labelKey: 'nav.filesReview', path: '/files/review', icon: <CheckSquareFilled />, permission: 'files.review.read' },
      { key: 'files-settings', labelKey: 'nav.filesSettings', path: '/files/settings', icon: <SettingOutlined />, permission: 'files.preferences.read' },
    ],
  },
  {
    key: 'assistant',
    feature: 'assistant',
    labelKey: 'nav.assistant',
    icon: <RobotOutlined />,
    screens: [
      { key: 'assistant-bar', labelKey: 'nav.assistantBar', path: '/assistant', icon: <RobotOutlined />, permission: 'assistant.commands.run' },
      { key: 'assistant-settings', labelKey: 'nav.assistantSettings', path: '/settings/assistant', icon: <SettingOutlined />, permission: 'assistant.profile.read' },
    ],
  },
  {
    key: 'integrations',
    feature: 'integrations',
    labelKey: 'nav.integrations',
    icon: <ApiOutlined />,
    screens: [
      { key: 'connections', labelKey: 'nav.connections', path: '/integrations/connections', icon: <ApiOutlined />, permission: 'integrations.accounts.read' },
    ],
  },
  {
    key: 'channels',
    feature: 'channels',
    labelKey: 'nav.channels',
    icon: <NotificationOutlined />,
    screens: [
      { key: 'notifications', labelKey: 'nav.notifications', path: '/channels/notifications', icon: <NotificationOutlined />, permission: 'channels.notifications.read' },
    ],
  },
  {
    key: 'account',
    labelKey: 'nav.account',
    icon: <SafetyOutlined />,
    screens: [
      { key: 'profile', labelKey: 'nav.profile', path: '/account', icon: <HomeOutlined /> },
      { key: 'security', labelKey: 'nav.security', path: '/account/security', icon: <SafetyOutlined /> },
      { key: 'devices', labelKey: 'nav.devices', path: '/account/devices', icon: <LaptopOutlined /> },
      { key: 'settings', labelKey: 'nav.settings', path: '/settings', icon: <SettingOutlined /> },
    ],
  },
  {
    key: 'admin',
    labelKey: 'nav.admin',
    icon: <ControlOutlined />,
    screens: [
      { key: 'admin-users', labelKey: 'nav.adminUsers', path: '/admin/users', icon: <TeamOutlined />, permission: 'admin.users.read' },
      { key: 'admin-roles', labelKey: 'nav.adminRoles', path: '/admin/roles', icon: <SafetyCertificateOutlined />, permission: 'admin.roles.read' },
      { key: 'admin-features', labelKey: 'nav.adminFeatures', path: '/admin/features', icon: <ControlOutlined />, permission: 'admin.features.manage' },
      { key: 'admin-audit', labelKey: 'nav.adminAudit', path: '/admin/audit', icon: <HistoryOutlined />, permission: 'admin.audit.read' },
    ],
  },
]
