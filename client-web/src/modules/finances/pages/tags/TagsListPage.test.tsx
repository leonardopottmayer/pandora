import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { TagsListPage } from './TagsListPage'

const tags = [{ id: 't1', name: 'Travel', color: '#1677ff' }, { id: 't2', name: 'Sa\u00fade', color: null }]
function listHandler() {
  return http.get(`${FINANCES_BASE}/tags`, () => HttpResponse.json({ success: true, data: tags }))
}
beforeAll(async () => { await i18n.changeLanguage('en') })

describe('TagsListPage', () => {
  it('renders tags and counts them, and filters without accents or case', async () => {
    server.use(listHandler())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<TagsListPage />)
    expect(await screen.findByText('Travel')).toBeInTheDocument()
    expect(screen.getByText('2 tags')).toBeInTheDocument()
    const search = screen.getByPlaceholderText('Search tags')
    await user.type(search, ' SAUDE ')
    expect(screen.queryByText('Travel')).not.toBeInTheDocument()
    expect(screen.getByText('Sa\u00fade')).toBeInTheDocument()
    expect(screen.getByText('1 tag')).toBeInTheDocument()
    await user.clear(search)
    await user.type(search, 'missing')
    expect(screen.getByText('No tags match this search.')).toBeInTheDocument()
    await user.clear(search)
    expect(screen.getByText('Travel')).toBeInTheDocument()
  })

  it('opens editing by clicking a row', async () => {
    server.use(listHandler())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<TagsListPage />)
    await user.click(await screen.findByText('Travel'))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('Travel')
  })

  it('opens creation from the heading', async () => {
    server.use(listHandler())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<TagsListPage />)
    await user.click(screen.getByRole('button', { name: /New tag/ }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('')
  })

  it('deletes only after confirming the named tag and warning about its links', async () => {
    let deleteCalled = false
    server.use(listHandler(), http.delete(`${FINANCES_BASE}/tags/t1`, () => {
      deleteCalled = true
      return new HttpResponse(null, { status: 204 })
    }))
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<TagsListPage />)
    await screen.findByText('Travel')
    await user.click(screen.getByLabelText('More actions for Travel'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await user.click(screen.getByRole('menuitem', { name: 'Delete' }))
    const confirmation = within(await screen.findByRole('dialog'))
    expect(confirmation.getByText('Travel')).toBeInTheDocument()
    expect(confirmation.getAllByText('Delete this tag? Its links will be removed.').length).toBeGreaterThan(0)
    expect(deleteCalled).toBe(false)
    await user.click(confirmation.getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleteCalled).toBe(true))
  })
})
