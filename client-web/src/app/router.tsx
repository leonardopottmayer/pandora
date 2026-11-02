import type { ComponentType } from 'react'
import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute, PublicOnlyRoute } from '@/modules/identity/components/RouteGuards'
import { LoginPage } from '@/modules/identity/pages/LoginPage'
import { RegisterPage } from '@/modules/identity/pages/RegisterPage'
import { MfaChallengePage } from '@/modules/identity/pages/MfaChallengePage'
import { ForgotPasswordPage } from '@/modules/identity/pages/ForgotPasswordPage'
import { ResetPasswordPage } from '@/modules/identity/pages/ResetPasswordPage'
import { ActivateAccountPage } from '@/modules/identity/pages/ActivateAccountPage'
import { NotFoundPage } from '@/pages/NotFoundPage'

// Each screen downloads when it is first opened, not with the app: one bundle with every module was
// 3.9 MB. The router waits for the chunk before it navigates, so the current screen stays meanwhile.
const page = <M,>(load: () => Promise<M>, name: keyof M) => async () => ({
  Component: (await load())[name] as ComponentType,
})

export const router = createBrowserRouter([
  // Public (redirect if already authenticated)
  { path: '/login', element: <PublicOnlyRoute><LoginPage /></PublicOnlyRoute> },
  { path: '/register', element: <PublicOnlyRoute><RegisterPage /></PublicOnlyRoute> },
  { path: '/mfa', element: <PublicOnlyRoute><MfaChallengePage /></PublicOnlyRoute> },
  { path: '/forgot-password', element: <PublicOnlyRoute><ForgotPasswordPage /></PublicOnlyRoute> },

  // Open public routes (accessed via email link)
  { path: '/reset-password', element: <ResetPasswordPage /> },
  { path: '/activate', element: <ActivateAccountPage /> },

  // Protected (inside the AppLayout)
  {
    element: <ProtectedRoute />,
    children: [
      { index: true, lazy: page(() => import('@/pages/HomePage'), 'HomePage') },
      { path: 'account', lazy: page(() => import('@/modules/identity/pages/ProfilePage'), 'ProfilePage') },
      { path: 'account/security', lazy: page(() => import('@/modules/identity/pages/SecurityPage'), 'SecurityPage') },
      { path: 'account/password', lazy: page(() => import('@/modules/identity/pages/ChangePasswordPage'), 'ChangePasswordPage') },
      { path: 'account/devices', lazy: page(() => import('@/modules/identity/pages/DevicesPage'), 'DevicesPage') },
      { path: 'settings', lazy: page(() => import('@/modules/identity/pages/SettingsPage'), 'SettingsPage') },
      { path: 'settings/assistant', lazy: page(() => import('@/modules/assistant/pages/AssistantSettingsPage'), 'AssistantSettingsPage') },
      { path: 'assistant', lazy: page(() => import('@/modules/assistant/pages/AssistantPage'), 'AssistantPage') },

      // Integrations
      { path: 'integrations/connections', lazy: page(() => import('@/modules/integrations/pages/ConnectionsPage'), 'ConnectionsPage') },

      // Channels
      { path: 'channels/notifications', lazy: page(() => import('@/modules/channels/pages/NotificationsPage'), 'NotificationsPage') },

      // Finances (telas reais substituem o placeholder conforme cada area e implementada)
      { path: 'finances/accounts', lazy: page(() => import('@/modules/finances/pages/accounts/AccountsListPage'), 'AccountsListPage') },
      { path: 'finances/accounts/:id', lazy: page(() => import('@/modules/finances/pages/accounts/AccountDetailPage'), 'AccountDetailPage') },
      { path: 'finances/transactions', lazy: page(() => import('@/modules/finances/pages/transactions/TransactionsListPage'), 'TransactionsListPage') },
      { path: 'finances/recurring', lazy: page(() => import('@/modules/finances/pages/recurring/RecurringTransactionsListPage'), 'RecurringTransactionsListPage') },
      { path: 'finances/inbox', lazy: page(() => import('@/modules/finances/pages/inbox/InboxPage'), 'InboxPage') },
      { path: 'finances/cards', lazy: page(() => import('@/modules/finances/pages/cards/CardsListPage'), 'CardsListPage') },
      { path: 'finances/cards/:id', lazy: page(() => import('@/modules/finances/pages/cards/CardDetailPage'), 'CardDetailPage') },
      { path: 'finances/statements/:id', lazy: page(() => import('@/modules/finances/pages/statements/StatementDetailPage'), 'StatementDetailPage') },
      { path: 'finances/categories', lazy: page(() => import('@/modules/finances/pages/categories/CategoriesListPage'), 'CategoriesListPage') },
      { path: 'finances/tags', lazy: page(() => import('@/modules/finances/pages/tags/TagsListPage'), 'TagsListPage') },
      { path: 'finances/audit', lazy: page(() => import('@/modules/finances/pages/audit/AuditPage'), 'AuditPage') },
      { path: 'finances/imports', lazy: page(() => import('@/modules/finances/pages/imports/ImportsListPage'), 'ImportsListPage') },
      { path: 'finances/imports/:id', lazy: page(() => import('@/modules/finances/pages/imports/ImportDetailPage'), 'ImportDetailPage') },
      { path: 'finances/import-layouts', lazy: page(() => import('@/modules/finances/pages/imports/ImportLayoutsPage'), 'ImportLayoutsPage') },

      // Notes
      // Agenda
      { path: 'agenda', lazy: page(() => import('@/modules/agenda/pages/today/TodayPage'), 'TodayPage') },
      { path: 'agenda/reminders', lazy: page(() => import('@/modules/agenda/pages/reminders/RemindersListPage'), 'RemindersListPage') },
      { path: 'agenda/tasks', lazy: page(() => import('@/modules/agenda/pages/tasks/TasksListPage'), 'TasksListPage') },
      { path: 'agenda/calendar', lazy: page(() => import('@/modules/agenda/pages/calendar/CalendarPage'), 'CalendarPage') },
      { path: 'agenda/settings', lazy: page(() => import('@/modules/agenda/pages/settings/AgendaSettingsPage'), 'AgendaSettingsPage') },

      { path: 'notes', lazy: page(() => import('@/modules/notes/pages/SpacesPage'), 'SpacesPage') },
      // Before the :id route, so "graph" is read as the view and not as a page id.
      { path: 'notes/graph', lazy: page(() => import('@/modules/notes/pages/NotesGraphPage'), 'NotesGraphPage') },
      { path: 'notes/library', lazy: page(() => import('@/modules/notes/pages/LibraryPage'), 'LibraryPage') },
      { path: 'notes/s/:spaceId', lazy: page(() => import('@/modules/notes/pages/NotesPage'), 'NotesPage') },
      { path: 'notes/:id', lazy: page(() => import('@/modules/notes/pages/NotesPage'), 'NotesPage') },

      // Files
      { path: 'files', lazy: page(() => import('@/modules/files/pages/CatalogPage'), 'CatalogPage') },
      { path: 'files/roots', lazy: page(() => import('@/modules/files/pages/RootsPage'), 'RootsPage') },
      { path: 'files/filters', lazy: page(() => import('@/modules/files/pages/FiltersPage'), 'FiltersPage') },
      { path: 'files/review', lazy: page(() => import('@/modules/files/pages/ReviewPage'), 'ReviewPage') },
      { path: 'files/settings', lazy: page(() => import('@/modules/files/pages/FilesSettingsPage'), 'FilesSettingsPage') },

      // Communications
      { path: 'mail', lazy: page(() => import('@/modules/communications/pages/MailPage'), 'MailPage') },
      { path: 'mail/accounts', lazy: page(() => import('@/modules/communications/pages/MailAccountsPage'), 'MailAccountsPage') },
      { path: 'mail/rules', lazy: page(() => import('@/modules/communications/pages/MailRulesPage'), 'MailRulesPage') },
    ],
  },

  { path: '*', element: <NotFoundPage /> },
])
