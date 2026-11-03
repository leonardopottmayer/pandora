import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { CategoriesListPage } from './CategoriesListPage'

const userCategory = { id: 'u1', name: 'Hobby', nature: 'expense', isActive: true, parentCategoryId: null, displayOrder: 0, color: null, icon: null, children: [] }
const child = { ...userCategory, id: 'u2', name: 'Painting', parentCategoryId: 'u1' }
const income = { ...userCategory, id: 'u3', name: 'Freelance', nature: 'income' }
const systemCategory = { ...userCategory, id: 's1', name: 'Food', children: [{ ...userCategory, id: 's2', name: 'Restaurants' }] }

beforeAll(async () => { await i18n.changeLanguage('en') })

function handlers() {
  return [
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [systemCategory] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [{ ...userCategory, children: [child] }, income] })),
  ]
}

function panel() { return within(screen.getByRole('tabpanel')) }

describe('CategoriesListPage', () => {
  it('starts with personal categories and shows the read-only system catalog on its tab', async () => {
    server.use(...handlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoriesListPage />)
    expect(await screen.findByText('Hobby')).toBeInTheDocument()
    expect(screen.queryByText('Food')).not.toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'System categories' }))
    expect(await screen.findByText('Food')).toBeInTheDocument()
    expect(panel().queryByRole('button', { name: /More actions/ })).not.toBeInTheDocument()
    expect(panel().queryByRole('switch')).not.toBeInTheDocument()
  })

  it('deactivates through the named row menu without opening the edit dialog', async () => {
    let action: string | null = null
    server.use(...handlers(), http.post(`${FINANCES_BASE}/categories/u1/deactivate`, () => {
      action = 'deactivate'
      return HttpResponse.json({ success: true, data: { ...userCategory, isActive: false } })
    }))
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoriesListPage />)
    await screen.findByText('Hobby')
    await user.click(screen.getByLabelText('More actions for Hobby'))
    await user.click(screen.getByRole('menuitem', { name: 'Deactivate' }))
    await waitFor(() => expect(action).toBe('deactivate'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('opens creation', async () => {
    server.use(...handlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoriesListPage />)
    await screen.findByText('Hobby')
    await user.click(screen.getByRole('button', { name: /New category/ }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })

  it('edits a category by clicking its row', async () => {
    server.use(...handlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoriesListPage />)
    await screen.findByText('Hobby')
    await user.click(screen.getByText('Hobby'))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('Hobby')
  })

  it('finds a child with its parent, hides unrelated branches and keeps filters per tab', async () => {
    server.use(...handlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoriesListPage />)
    await screen.findByText('Hobby')
    await user.type(panel().getByPlaceholderText('Search categories'), 'painting')
    expect(await screen.findByText('Painting')).toBeInTheDocument()
    expect(screen.getByText('Hobby')).toBeInTheDocument()
    expect(screen.queryByText('Freelance')).not.toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'System categories' }))
    await user.type(panel().getByPlaceholderText('Search categories'), 'restaurants')
    expect(await screen.findByText('Restaurants')).toBeInTheDocument()
    expect(panel().getByText('Food')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'My categories' }))
    expect(panel().getByPlaceholderText('Search categories')).toHaveValue('painting')
    await user.click(panel().getByRole('button', { name: 'Clear filters' }))
    expect(panel().getByText('Freelance')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'System categories' }))
    expect(panel().getByPlaceholderText('Search categories')).toHaveValue('restaurants')
  })

  it('filters by nature and requests inactive categories when enabled', async () => {
    let includeInactive = false
    server.use(http.get(`${FINANCES_BASE}/categories`, ({ request }) => {
      includeInactive = new URL(request.url).searchParams.get('includeInactive') === 'true'
      return HttpResponse.json({ success: true, data: [userCategory, income, ...(includeInactive ? [{ ...userCategory, id: 'old', name: 'Old', isActive: false }] : [])] })
    }), ...handlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoriesListPage />)
    await screen.findByText('Hobby')
    await user.click(panel().getByRole('combobox', { name: 'Nature' }))
    await user.click(screen.getByText('Income', { selector: '.ant-select-item-option-content' }))
    expect(panel().getByText('Freelance')).toBeInTheDocument()
    expect(panel().queryByText('Hobby')).not.toBeInTheDocument()
    await user.click(panel().getByRole('button', { name: 'Clear filters' }))
    await user.click(panel().getByRole('switch', { name: 'Show inactive' }))
    expect(await screen.findByText('Old')).toBeInTheDocument()
    expect(includeInactive).toBe(true)
    expect(panel().getByText('Inactive')).toBeInTheDocument()
    await user.click(panel().getByLabelText('More actions for Old'))
    expect(screen.getByRole('menuitem', { name: 'Activate' })).toBeInTheDocument()
  })
})
