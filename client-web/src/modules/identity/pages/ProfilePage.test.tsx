import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/utils'
import { AuthContext, type AuthContextValue } from '../context/auth-context'
import { ProfilePage } from './ProfilePage'

function renderProfile(value: Partial<AuthContextValue>) {
  return renderWithProviders(
    <AuthContext.Provider value={value as AuthContextValue}>
      <ProfilePage />
    </AuthContext.Provider>,
  )
}

const user = { id: 'u1', name: 'Ana Maria Souza', email: 'ana@mail.test', username: 'anasouza' }

describe('ProfilePage', () => {
  it("shows the first and last name's initials and the user's data", () => {
    renderProfile({ user, loginLabel: null })

    expect(screen.getByText('AS')).toBeInTheDocument()
    expect(screen.getAllByText('Ana Maria Souza').length).toBeGreaterThan(0)
    expect(screen.getByText('anasouza')).toBeInTheDocument()
  })

  it('takes one initial from a single name', () => {
    renderProfile({ user: { ...user, name: 'Ana' }, loginLabel: null })

    expect(screen.getByText('A')).toBeInTheDocument()
  })

  it('links to the account screens', () => {
    renderProfile({ user, loginLabel: null })

    expect(screen.getByRole('link', { name: 'Security' })).toHaveAttribute('href', '/account/security')
    expect(screen.getByRole('link', { name: 'Change password' })).toHaveAttribute('href', '/account/password')
    expect(screen.getByRole('link', { name: 'Devices' })).toHaveAttribute('href', '/account/devices')
  })

  it('falls back to the login identifier without user data', () => {
    renderProfile({ user: null, loginLabel: 'ana' })

    expect(screen.getByText(/identifier used at login: ana/)).toBeInTheDocument()
  })
})
