import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute, PublicOnlyRoute } from '@/modules/identity/components/RouteGuards'
import { LoginPage } from '@/modules/identity/pages/LoginPage'
import { RegisterPage } from '@/modules/identity/pages/RegisterPage'
import { MfaChallengePage } from '@/modules/identity/pages/MfaChallengePage'
import { ForgotPasswordPage } from '@/modules/identity/pages/ForgotPasswordPage'
import { ResetPasswordPage } from '@/modules/identity/pages/ResetPasswordPage'
import { ActivateAccountPage } from '@/modules/identity/pages/ActivateAccountPage'
import { ProfilePage } from '@/modules/identity/pages/ProfilePage'
import { ChangePasswordPage } from '@/modules/identity/pages/ChangePasswordPage'
import { SecurityPage } from '@/modules/identity/pages/SecurityPage'
import { SettingsPage } from '@/modules/identity/pages/SettingsPage'
import { HomePage } from '@/pages/HomePage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { AccountsListPage } from '@/modules/finances/pages/accounts/AccountsListPage'
import { AccountDetailPage } from '@/modules/finances/pages/accounts/AccountDetailPage'
import { CategoriesListPage } from '@/modules/finances/pages/categories/CategoriesListPage'
import { TagsListPage } from '@/modules/finances/pages/tags/TagsListPage'
import { TransactionsListPage } from '@/modules/finances/pages/transactions/TransactionsListPage'
import { RecurringTransactionsListPage } from '@/modules/finances/pages/recurring/RecurringTransactionsListPage'
import { InboxPage } from '@/modules/finances/pages/inbox/InboxPage'
import { CardsListPage } from '@/modules/finances/pages/cards/CardsListPage'
import { CardDetailPage } from '@/modules/finances/pages/cards/CardDetailPage'
import { StatementDetailPage } from '@/modules/finances/pages/statements/StatementDetailPage'
import { AuditPage } from '@/modules/finances/pages/audit/AuditPage'
import { ImportsListPage } from '@/modules/finances/pages/imports/ImportsListPage'
import { ImportDetailPage } from '@/modules/finances/pages/imports/ImportDetailPage'
import { ImportLayoutsPage } from '@/modules/finances/pages/imports/ImportLayoutsPage'
import { NotesPage } from '@/modules/notes/pages/NotesPage'
import { NotesGraphPage } from '@/modules/notes/pages/NotesGraphPage'
import { TodayPage } from '@/modules/agenda/pages/today/TodayPage'
import { RemindersListPage } from '@/modules/agenda/pages/reminders/RemindersListPage'
import { TasksListPage } from '@/modules/agenda/pages/tasks/TasksListPage'
import { CalendarPage } from '@/modules/agenda/pages/calendar/CalendarPage'

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
      { index: true, element: <HomePage /> },
      { path: 'account', element: <ProfilePage /> },
      { path: 'account/security', element: <SecurityPage /> },
      { path: 'account/password', element: <ChangePasswordPage /> },
      { path: 'settings', element: <SettingsPage /> },

      // Finances (telas reais substituem o placeholder conforme cada area e implementada)
      { path: 'finances/accounts', element: <AccountsListPage /> },
      { path: 'finances/accounts/:id', element: <AccountDetailPage /> },
      { path: 'finances/transactions', element: <TransactionsListPage /> },
      { path: 'finances/recurring', element: <RecurringTransactionsListPage /> },
      { path: 'finances/inbox', element: <InboxPage /> },
      { path: 'finances/cards', element: <CardsListPage /> },
      { path: 'finances/cards/:id', element: <CardDetailPage /> },
      { path: 'finances/statements/:id', element: <StatementDetailPage /> },
      { path: 'finances/categories', element: <CategoriesListPage /> },
      { path: 'finances/tags', element: <TagsListPage /> },
      { path: 'finances/audit', element: <AuditPage /> },
      { path: 'finances/imports', element: <ImportsListPage /> },
      { path: 'finances/imports/:id', element: <ImportDetailPage /> },
      { path: 'finances/import-layouts', element: <ImportLayoutsPage /> },

      // Notes
      // Agenda
      { path: 'agenda', element: <TodayPage /> },
      { path: 'agenda/reminders', element: <RemindersListPage /> },
      { path: 'agenda/tasks', element: <TasksListPage /> },
      { path: 'agenda/calendar', element: <CalendarPage /> },

      { path: 'notes', element: <NotesPage /> },
      // Before the :id route, so "graph" is read as the view and not as a page id.
      { path: 'notes/graph', element: <NotesGraphPage /> },
      { path: 'notes/:id', element: <NotesPage /> },
    ],
  },

  { path: '*', element: <NotFoundPage /> },
])
