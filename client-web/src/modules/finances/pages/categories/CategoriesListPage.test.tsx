import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { CategoriesListPage } from './CategoriesListPage'

const userCategory = {
  id: 'u1',
  name: 'Hobby',
  nature: 'expense',
  isActive: true,
  children: [],
}

const systemCategory = { id: 's1', name: 'Alimentação', children: [] }

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function handlers() {
  return [
    http.get(`${FINANCES_BASE}/categories/system`, () =>
      HttpResponse.json({ success: true, data: [systemCategory] }),
    ),
    http.get(`${FINANCES_BASE}/categories`, () =>
      HttpResponse.json({ success: true, data: [userCategory] }),
    ),
  ]
}

describe('CategoriesListPage', () => {
  it('renders user and system categories', async () => {
    server.use(...handlers())
    renderWithProviders(<CategoriesListPage />)
    expect(await screen.findByText('Hobby')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
  })

  it('deactivates a user category through the active switch', async () => {
    let action: string | null = null
    server.use(
      ...handlers(),
      http.post(`${FINANCES_BASE}/categories/u1/deactivate`, () => {
        action = 'deactivate'
        return HttpResponse.json({ success: true, data: { ...userCategory, isActive: false } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CategoriesListPage />)

    await screen.findByText('Hobby')
    // The first switch in the row toggles the active state (checked → deactivate).
    const switches = screen.getAllByRole('switch')
    await user.click(switches[switches.length - 1])
    await waitFor(() => expect(action).toBe('deactivate'))
  })

  it('opens the create modal', async () => {
    server.use(...handlers())
    const user = userEvent.setup()
    renderWithProviders(<CategoriesListPage />)
    await screen.findByText('Hobby')
    await user.click(screen.getByRole('button', { name: /New/ }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })
})
