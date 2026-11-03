import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useLocation } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CardDto } from '../../models'
import { CardsListPage } from './CardsListPage'

const card: CardDto = {
  id: 'k1',
  name: 'Nubank',
  brand: 'Mastercard',
  lastFour: '1234',
  creditLimit: 5000,
  closingDay: 5,
  dueDay: 12,
  currency: 'BRL',
  accountId: 'a1',
  archivedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function Location() {
  return <output data-testid="location">{useLocation().pathname}</output>
}

function serveCards() {
  server.use(
    http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [card] })),
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [{ id: 'a1', name: 'Conta', currency: 'BRL' }] })),
  )
}

describe('CardsListPage', () => {
  it('renders cards from the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [card] })),
    )
    renderWithProviders(<CardsListPage />)
    expect(await screen.findByText('Nubank')).toBeInTheDocument()
    expect(screen.getByText(/Mastercard · ····1234/)).toBeInTheDocument()
    expect(screen.queryByText('Active')).not.toBeInTheDocument()
    expect(screen.getByText('1 card')).toBeInTheDocument()
  })

  it('opens the card from the row and keeps its action menu from navigating', async () => {
    serveCards()
    const user = userEvent.setup()
    renderWithProviders(<><CardsListPage /><Location /></>)
    await user.click(await screen.findByRole('button', { name: 'More actions for Nubank' }))
    expect(screen.getByTestId('location')).toHaveTextContent('/')
    await user.click(screen.getByRole('menuitem', { name: 'Edit' }))
    expect(await screen.findByRole('dialog', { name: 'Edit card' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('row', { name: /Nubank/ }))
    expect(screen.getByTestId('location')).toHaveTextContent('/finances/cards/k1')
  })

  it('requests archived cards through the switch and only marks archived rows', async () => {
    server.use(http.get(`${FINANCES_BASE}/cards`, ({ request }) => HttpResponse.json({ success: true, data:
      new URL(request.url).searchParams.get('includeArchived') === 'true'
        ? [card, { ...card, id: 'old', name: 'Old card', archivedAt: '2026-01-01T00:00:00Z' }] : [card],
    })))
    const user = userEvent.setup()
    renderWithProviders(<CardsListPage />)
    await screen.findByText('Nubank')
    expect(screen.queryByText('Old card')).not.toBeInTheDocument()
    await user.click(screen.getByRole('switch', { name: 'Show archived' }))
    expect(await screen.findByText('Old card')).toBeInTheDocument()
    expect(screen.getAllByText('Archived')).toHaveLength(1)
    expect(screen.queryByText('Active')).not.toBeInTheDocument()
  })

  it('requires confirmation before archiving and identifies the card', async () => {
    serveCards()
    let archived = false
    server.use(
      http.post(`${FINANCES_BASE}/cards/k1/archive`, () => { archived = true; return HttpResponse.json({ success: true, data: card }) }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CardsListPage />)
    await user.click(await screen.findByRole('button', { name: 'More actions for Nubank' }))
    await user.click(screen.getByRole('menuitem', { name: 'Archive' }))
    const archiveDialog = await screen.findByRole('dialog')
    expect(within(archiveDialog).getByText('Nubank')).toBeInTheDocument()
    expect(archived).toBe(false)
    await user.click(within(archiveDialog).getByRole('button', { name: 'Archive' }))
    await waitFor(() => expect(archived).toBe(true))
  })

  it('requires confirmation before deleting and identifies the card', async () => {
    serveCards()
    let deleted = false
    server.use(http.delete(`${FINANCES_BASE}/cards/k1`, () => { deleted = true; return new HttpResponse(null, { status: 204 }) }))
    const user = userEvent.setup()
    renderWithProviders(<CardsListPage />)
    await user.click(await screen.findByRole('button', { name: 'More actions for Nubank' }))
    await user.click(screen.getByRole('menuitem', { name: 'Delete' }))
    const deleteDialog = await screen.findByRole('dialog')
    expect(within(deleteDialog).getByText('Nubank')).toBeInTheDocument()
    expect(deleted).toBe(false)
    await user.click(within(deleteDialog).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleted).toBe(true))
  })

  it('explains the empty list', async () => {
    server.use(http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [] })))
    renderWithProviders(<CardsListPage />)
    expect(await screen.findByText('No cards to show. Add your first card.')).toBeInTheDocument()
  })

  it('unarchives through the row menu without opening the card or asking for a destructive confirmation', async () => {
    let unarchived = false
    server.use(
      http.get(`${FINANCES_BASE}/cards`, ({ request }) => HttpResponse.json({ success: true, data:
        new URL(request.url).searchParams.get('includeArchived') === 'true'
          ? [{ ...card, archivedAt: '2026-01-01T00:00:00Z' }] : [],
      })),
      http.post(`${FINANCES_BASE}/cards/k1/unarchive`, () => {
        unarchived = true
        return HttpResponse.json({ success: true, data: card })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<><CardsListPage /><Location /></>)
    await user.click(screen.getByRole('switch', { name: 'Show archived' }))
    await user.click(await screen.findByRole('button', { name: 'More actions for Nubank' }))
    await user.click(screen.getByRole('menuitem', { name: 'Unarchive' }))
    await waitFor(() => expect(unarchived).toBe(true))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/')
  })
})
