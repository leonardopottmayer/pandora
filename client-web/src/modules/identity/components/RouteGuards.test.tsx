import type { ReactNode } from 'react'
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { AuthContext, type AuthContextValue } from '../context/auth-context'
import { ProtectedRoute, PublicOnlyRoute } from './RouteGuards'

function authValue(overrides: Partial<AuthContextValue>): AuthContextValue {
  return {
    user: null,
    loginLabel: null,
    isAuthenticated: false,
    isLoading: false,
    login: async () => ({ tokens: null, mfa: null }),
    completeMfa: async () => {},
    logout: async () => {},
    reloadUser: async () => {},
    ...overrides,
  }
}

function renderWithAuth(value: AuthContextValue, ui: ReactNode, initialEntry = '/secret') {
  return render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={[initialEntry]}>{ui}</MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('ProtectedRoute', () => {
  it('shows a spinner while the session is loading', () => {
    const { container } = renderWithAuth(
      authValue({ isLoading: true }),
      <Routes>
        <Route path="/secret" element={<ProtectedRoute />} />
      </Routes>,
    )
    expect(container.querySelector('.ant-spin')).toBeInTheDocument()
  })

  it('redirects to /login when unauthenticated', () => {
    renderWithAuth(
      authValue({ isAuthenticated: false }),
      <Routes>
        <Route path="/secret" element={<ProtectedRoute />} />
        <Route path="/login" element={<div>Login Page</div>} />
      </Routes>,
    )
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })
})

describe('PublicOnlyRoute', () => {
  it('renders its children when unauthenticated', () => {
    renderWithAuth(
      authValue({ isAuthenticated: false }),
      <Routes>
        <Route path="/secret" element={<PublicOnlyRoute><div>Public Content</div></PublicOnlyRoute>} />
      </Routes>,
    )
    expect(screen.getByText('Public Content')).toBeInTheDocument()
  })

  it('redirects authenticated users to the home page', () => {
    renderWithAuth(
      authValue({ isAuthenticated: true }),
      <Routes>
        <Route path="/secret" element={<PublicOnlyRoute><div>Public Content</div></PublicOnlyRoute>} />
        <Route path="/" element={<div>Home Page</div>} />
      </Routes>,
    )
    expect(screen.getByText('Home Page')).toBeInTheDocument()
  })
})
