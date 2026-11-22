import { beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { SpaceDto } from '../models'
import { SpacesPage } from './SpacesPage'

const BASE = `${TEST_API_BASE}/api/v1/notes/spaces`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const space = (over: Partial<SpaceDto>): SpaceDto => ({
  id: 's1',
  name: 'Home',
  icon: '🏠',
  color: '#2dcd88',
  description: null,
  isDefault: false,
  isArchived: false,
  pageCount: 0,
  lastEditedAt: null,
  createdAt: '2026-11-01T00:00:00Z',
  ...over,
})

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('SpacesPage', () => {
  beforeEach(() => {
    server.use(
      http.get(BASE, () =>
        ok([
          space({ isDefault: true, pageCount: 3, lastEditedAt: new Date().toISOString(), description: 'Life stuff' }),
          space({ id: 's2', name: 'Work', icon: null, pageCount: 1 }),
          space({ id: 's3', name: 'Empty' }),
        ]),
      ),
    )
  })

  it('shows each space with what it holds', async () => {
    renderWithProviders(<SpacesPage />)

    const home = await screen.findByRole('link', { name: 'Home' })
    expect(within(home).getByText('Default')).toBeInTheDocument()
    expect(within(home).getByText('Life stuff')).toBeInTheDocument()
    expect(within(home).getByText(/3 pages · edited/)).toBeInTheDocument()
    expect(within(screen.getByRole('link', { name: 'Work' })).getByText('1 page')).toBeInTheDocument()
  })

  it('keeps the default space and a full one from being deleted', async () => {
    const user = userEvent.setup()
    renderWithProviders(<SpacesPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Home' }))
    expect(screen.queryByRole('menuitem', { name: 'Delete' })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: 'Archive' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'More actions for Work' }))
    expect(await screen.findByRole('menuitem', { name: 'Delete (move its pages first)' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
  })

  it('deletes an empty space after confirming', async () => {
    let deleted = ''
    server.use(
      http.delete(`${BASE}/:id`, ({ params }) => {
        deleted = String(params.id)
        return ok(true)
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<SpacesPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Empty' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Delete' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleted).toBe('s3'))
  })

  it('creates a space from the form', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post(BASE, async ({ request }) => {
        bodies.push(await request.json())
        return ok(space({ id: 'new', name: 'Travel' }))
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<SpacesPage />)

    await user.click(await screen.findByRole('button', { name: /New space/ }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByRole('textbox', { name: 'Name' }), 'Travel')
    await user.type(within(dialog).getByRole('textbox', { name: 'Description' }), 'Trips and plans')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(bodies).toEqual([{ name: 'Travel', icon: null, color: null, description: 'Trips and plans' }]),
    )
  })
})
