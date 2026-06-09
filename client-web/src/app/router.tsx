import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute, PublicOnlyRoute } from '@/features/identity/components/RouteGuards'
import { LoginPage } from '@/features/identity/pages/LoginPage'
import { RegisterPage } from '@/features/identity/pages/RegisterPage'
import { MfaChallengePage } from '@/features/identity/pages/MfaChallengePage'
import { ForgotPasswordPage } from '@/features/identity/pages/ForgotPasswordPage'
import { ResetPasswordPage } from '@/features/identity/pages/ResetPasswordPage'
import { ActivateAccountPage } from '@/features/identity/pages/ActivateAccountPage'
import { ProfilePage } from '@/features/identity/pages/ProfilePage'
import { ChangePasswordPage } from '@/features/identity/pages/ChangePasswordPage'
import { SecurityPage } from '@/features/identity/pages/SecurityPage'
import { SettingsPage } from '@/features/identity/pages/SettingsPage'
import { HomePage } from '@/pages/HomePage'
import { NotFoundPage } from '@/pages/NotFoundPage'

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
    ],
  },

  { path: '*', element: <NotFoundPage /> },
])
