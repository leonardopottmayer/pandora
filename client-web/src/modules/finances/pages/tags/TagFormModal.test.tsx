import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TagDto } from '../../models'
import { TagFormModal } from './TagFormModal'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('TagFormModal', () => {
  it('validates that the name is required', async () => {
    let posted = false
    server.use(
      http.post(`${FINANCES_BASE}/tags`, () => {
        posted = true
        return HttpResponse.json({ success: true, data: { id: 't1', name: 'x', color: null } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TagFormModal open onClose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter the tag name.')).toBeInTheDocument()
    expect(posted).toBe(false)
  })

  it('creates a tag and closes', async () => {
    let body: unknown
    const onClose = vi.fn()
    server.use(
      http.post(`${FINANCES_BASE}/tags`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { id: 't1', name: 'Travel', color: null } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TagFormModal open onClose={onClose} />)

    await user.type(screen.getByRole('textbox'), 'Travel')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ name: 'Travel' })
  })

  it('pre-fills and updates an existing tag', async () => {
    const tag: TagDto = { id: 't1', name: 'Old', color: '#1677ff' }
    let putCalled = false
    const onClose = vi.fn()
    server.use(
      http.put(`${FINANCES_BASE}/tags/t1`, () => {
        putCalled = true
        return HttpResponse.json({ success: true, data: { ...tag, name: 'New' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TagFormModal open tag={tag} onClose={onClose} />)

    const input = screen.getByRole('textbox') as HTMLInputElement
    expect(input.value).toBe('Old')
    await user.clear(input)
    await user.type(input, 'New')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(putCalled).toBe(true))
    expect(onClose).toHaveBeenCalled()
  })

  it('previews name changes and sends null when removing an existing color', async () => {
    let body: unknown
    server.use(http.put(`${FINANCES_BASE}/tags/t1`, async ({ request }) => {
      body = await request.json()
      return HttpResponse.json({ success: true, data: { id: 't1', name: 'Renamed', color: null } })
    }))
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<TagFormModal open tag={{ id: 't1', name: 'Old', color: '#ffffff' }} onClose={vi.fn()} />)
    const dialog = within(screen.getByRole('dialog'))
    await user.clear(dialog.getByRole('textbox', { name: 'Name' }))
    await user.type(dialog.getByRole('textbox', { name: 'Name' }), 'Renamed')
    expect(dialog.getByText('Renamed')).toBeInTheDocument()
    await user.click(dialog.getByRole('button', { name: 'Clear color' }))
    expect(dialog.getByRole('button', { name: 'Clear color' })).toBeDisabled()
    await user.click(dialog.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(body).toEqual({ name: 'Renamed', color: null }))
  })
})
