import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import i18n from '@/i18n'
import type { PageSnapshot } from '../models'
import { PageHistoryDrawer } from './PageHistoryDrawer'

const BASE = `${TEST_API_BASE}/api/v1/notes`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })
const snapshot: PageSnapshot = {
  title: 'Original', contentMarkdown: 'Original body', icon: null, spaceId: 's1', parentId: null,
  orderIndex: 0, isFavorite: false, archivedAt: null, deletedAt: null,
}
const versions = [3, 2, 1].map((number) => ({ number, createdAt: `2026-11-02T12:0${number}:00Z`, source: 'edit', restoredFrom: null }))

beforeEach(async () => {
  await i18n.changeLanguage('en')
  server.use(
    http.get(`${BASE}/pages/p1/versions`, () => ok(versions)),
    http.get(`${BASE}/pages/p1/versions/:number`, ({ params }) => ok({
      ...versions.find((v) => v.number === Number(params.number)),
      snapshot: { ...snapshot, title: `Title ${params.number}`, contentMarkdown: `Body ${params.number}` },
    })),
    http.get(`${BASE}/spaces`, () => ok([{ id: 's1', name: 'Home' }])),
    http.get(`${BASE}/pages`, () => ok([])),
  )
})

describe('PageHistoryDrawer', () => {
  it('compares arbitrary versions and restores with the latest known version as a guard', async () => {
    const restored = vi.fn()
    let requestBody: unknown
    server.use(http.post(`${BASE}/pages/p1/versions/1/restore`, async ({ request }) => {
      requestBody = await request.json()
      return ok({ id: 'p1', ...snapshot })
    }))
    renderWithProviders(<PageHistoryDrawer pageId="p1" onClose={vi.fn()} onRestored={restored} />)
    await screen.findByText('+ Body 3')
    expect(screen.getByText('− Body 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Restore version' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: /3 versions/ }))
    await userEvent.click(screen.getByRole('button', { name: /^v1 ·/ }))
    await screen.findByText('+ Body 1')
    await userEvent.click(screen.getByRole('button', { name: 'Restore version' }))
    await screen.findAllByText('Restore version 1?')
    const buttons = screen.getAllByRole('button', { name: 'Restore version' })
    await userEvent.click(buttons.at(-1)!)
    await waitFor(() => expect(restored).toHaveBeenCalledOnce())
    expect(requestBody).toEqual({ expectedVersion: 3 })
  })

  it('keeps the drawer open and reports a stale restore without replacing the editor', async () => {
    const restored = vi.fn()
    server.use(http.post(`${BASE}/pages/p1/versions/1/restore`, () => HttpResponse.json(
      { success: false, errorCode: 'Pages.HistoryConflict', errorMessage: 'Page changed elsewhere.' }, { status: 409 },
    )))
    renderWithProviders(<PageHistoryDrawer pageId="p1" onClose={vi.fn()} onRestored={restored} />)
    await screen.findByText('+ Body 3')
    await userEvent.click(screen.getByRole('button', { name: /3 versions/ }))
    await userEvent.click(screen.getByRole('button', { name: /^v1 ·/ }))
    await screen.findByText('+ Body 1')
    await userEvent.click(screen.getByRole('button', { name: 'Restore version' }))
    await screen.findAllByText('Restore version 1?')
    await userEvent.click(screen.getAllByRole('button', { name: 'Restore version' }).at(-1)!)
    await waitFor(() => expect(screen.getByText(/Page changed elsewhere|Could not restore/)).toBeInTheDocument())
    expect(restored).not.toHaveBeenCalled()
  })

  it('detects a parent change even when both parents have the same title', async () => {
    server.use(
      http.get(`${BASE}/pages`, () => ok([{ id: 'parent-2', title: 'Same title' }, { id: 'parent-3', title: 'Same title' }])),
      http.get(`${BASE}/pages/p1/versions/:number`, ({ params }) => ok({
        ...versions.find((v) => v.number === Number(params.number)),
        snapshot: { ...snapshot, parentId: `parent-${params.number}` },
      })),
    )
    renderWithProviders(<PageHistoryDrawer pageId="p1" onClose={vi.fn()} onRestored={vi.fn()} />)
    const field = await screen.findByRole('rowheader', { name: 'Parent page' })
    expect(within(field.closest('tr')!).getAllByRole('cell').map((cell) => cell.textContent)).toEqual(['Same title', 'Same title'])
  })
})
