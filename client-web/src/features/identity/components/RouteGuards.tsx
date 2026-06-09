import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Spin } from 'antd'
import { useAuth } from '../context/auth-context'
import { AppLayout } from '@/layouts/AppLayout'

function FullScreenSpin() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spin size="large" />
    </div>
  )
}

/** Requires a session; renders the authenticated layout. */
export function ProtectedRoute() {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <FullScreenSpin />
  if (!isAuthenticated) return <Navigate to="/login" state={{ from: location }} replace />
  return <AppLayout />
}

/** Public routes (login, register): redirects anyone already authenticated. */
export function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()
  if (isLoading) return <FullScreenSpin />
  if (isAuthenticated) return <Navigate to="/" replace />
  return <>{children}</>
}
