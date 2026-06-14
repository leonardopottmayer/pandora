import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { TagsListPage } from './TagsListPage'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('TagsListPage', () => {
  it('renders tags from the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/tags`, () =>
        HttpResponse.json({ success: true, data: [{ id: 't1', name: 'Travel', color: '#1677ff' }] }),
      ),
    )
    renderWithProviders(<TagsListPage />)
    expect(await screen.findByText('Travel')).toBeInTheDocument()
  })

  it('deletes a tag through the confirm popover', async () => {
    let deleteCalled = false
    server.use(
      http.get(`${FINANCES_BASE}/tags`, () =>
        HttpResponse.json({ success: true, data: [{ id: 't1', name: 'Travel', color: null }] }),
      ),
      http.delete(`${FINANCES_BASE}/tags/t1`, () => {
        deleteCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TagsListPage />)

    await screen.findByText('Travel')
    await user.click(screen.getByRole('button', { name: /Delete/ }))
    // Popconfirm confirm button (the OK button inside the popover).
    const confirmButtons = await screen.findAllByRole('button', { name: /Delete/ })
    await user.click(confirmButtons[confirmButtons.length - 1])
    await waitFor(() => expect(deleteCalled).toBe(true))
  })
})
