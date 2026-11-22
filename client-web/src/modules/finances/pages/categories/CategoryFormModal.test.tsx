import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { UserCategoryDto } from '../../models'
import { CategoryFormModal } from './CategoryFormModal'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function userCategoriesHandler() {
  return http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] }))
}

describe('CategoryFormModal', () => {
  it('creates a user category defaulting to the expense nature', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      userCategoriesHandler(),
      http.post(`${FINANCES_BASE}/categories`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: { id: 'u1', name: 'Hobby' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CategoryFormModal open onClose={onClose} />)

    await user.type(screen.getByRole('textbox'), 'Hobby')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ name: 'Hobby', nature: 'expense' })
  })

  it('validates that the name is required', async () => {
    let posted = false
    server.use(
      userCategoriesHandler(),
      http.post(`${FINANCES_BASE}/categories`, () => {
        posted = true
        return HttpResponse.json({ success: true, data: { id: 'u1', name: 'x' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CategoryFormModal open onClose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter the category name.')).toBeInTheDocument()
    expect(posted).toBe(false)
  })

  it('clears an incompatible parent when changing nature before creation', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [
        { id: 'parent', name: 'Hobbies', nature: 'expense', parentCategoryId: null, children: [] },
      ] })),
      http.post(`${FINANCES_BASE}/categories`, async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ success: true, data: { id: 'new' } })
      }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<CategoryFormModal open onClose={vi.fn()} />)
    await user.type(screen.getByRole('textbox', { name: 'Name' }), 'Freelance')
    await user.click(screen.getByRole('combobox', { name: 'Parent category' }))
    await user.click(await screen.findByText('Hobbies', { selector: '.ant-select-item-option-content' }))
    await user.click(screen.getByRole('combobox', { name: 'Nature' }))
    await user.click(screen.getByText('Income', { selector: '.ant-select-item-option-content' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(body).toMatchObject({ name: 'Freelance', nature: 'income', parentCategoryId: null, displayOrder: 0 }))
  })

  it('updates an existing category (nature locked) via PUT', async () => {
    const category: UserCategoryDto = {
      id: 'u1',
      name: 'Old',
      nature: 'income',
      parentCategoryId: null,
      color: null,
      icon: null,
      displayOrder: 0,
      isActive: true,
      children: [],
    } as UserCategoryDto
    let putCalled = false
    const onClose = vi.fn()
    server.use(
      userCategoriesHandler(),
      http.put(`${FINANCES_BASE}/categories/u1`, () => {
        putCalled = true
        return HttpResponse.json({ success: true, data: { ...category, name: 'New' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CategoryFormModal open category={category} onClose={onClose} />)

    const input = screen.getByRole('textbox') as HTMLInputElement
    expect(input.value).toBe('Old')
    await user.clear(input)
    await user.type(input, 'New')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(putCalled).toBe(true))
    expect(onClose).toHaveBeenCalled()
  })
})
