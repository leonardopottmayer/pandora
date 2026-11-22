import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { Block } from '../lib/blocks'
import type { PageDto, UpdatePageRequest } from '../models'
import { NotesPage } from './NotesPage'

// The real editor's acceptance transaction is covered by VisualEditor.test.tsx. Here the seam lets
// the page's debounced saves be exercised independently of selection and popup positioning.
vi.mock('../components/visual/VisualEditor', () => ({
  VisualEditor: ({ onChange }: { onChange: (blocks: Block[], source?: 'assistant') => void }) => <>
    <button onClick={() => onChange([{ id: 'b', type: 'paragraph', content: [{ text: 'AI text' }] }], 'assistant')}>Accept AI</button>
    <button onClick={() => onChange([{ id: 'b', type: 'paragraph', content: [{ text: 'Manual text' }] }])}>Type manually</button>
  </>,
}))

const BASE = `${TEST_API_BASE}/api/v1/notes`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

beforeAll(async () => { await i18n.changeLanguage('en') })

describe('AI autosave origin', () => {
  it('keeps assistant origin through debounced typing, then returns later manual saves to edit', async () => {
    const requests: UpdatePageRequest[] = []
    const page = {
      id: 'p1', spaceId: 's1', title: 'Writing', slug: 'writing', icon: null, parentId: null, folderId: null,
      content: { version: 1, blocks: [] }, tags: [], isLocked: false, appearance: { cover: null, width: 'normal', font: 'default', smallText: false },
    } as unknown as PageDto
    server.use(
      http.get(`${BASE}/pages/p1`, () => ok(page)),
      http.get(`${BASE}/pages`, () => ok([page])),
      http.get(`${BASE}/spaces`, () => ok([{ id: 's1', name: 'Home', isDefault: true }])),
      http.get(`${BASE}/folders`, () => ok([])),
      http.get(`${BASE}/tags`, () => ok([])),
      http.get(`${BASE}/pages/recent`, () => ok([])),
      http.get(`${BASE}/pages/p1/backlinks`, () => ok([])),
      http.get(`${BASE}/pages/p1/comments`, () => ok([])),
      http.get(`${BASE}/pages/p1/block-stamps`, () => ok([])),
      http.post(`${BASE}/pages/p1/viewed`, () => ok(true)),
      http.put(`${BASE}/pages/p1`, async ({ request }) => {
        const body = await request.json() as UpdatePageRequest
        requests.push(body)
        return ok({ ...page, content: body.content })
      }),
    )
    renderWithProviders(<Routes><Route path="/notes/:id" element={<NotesPage />} /></Routes>, { route: '/notes/p1' })
    await userEvent.click(await screen.findByRole('button', { name: 'Accept AI' }))
    await userEvent.click(screen.getByRole('button', { name: 'Type manually' }))
    await waitFor(() => expect(requests).toHaveLength(1), { timeout: 3000 })
    expect(requests[0].source).toBe('assistant')
    expect(requests[0].content.blocks[0].content).toEqual([{ text: 'Manual text' }])

    // Waiting for Saved also ensures that the mutation resolved and the accepted revision was recorded.
    await screen.findByText('Saved')
    await userEvent.click(screen.getByRole('button', { name: 'Type manually' }))
    await waitFor(() => expect(requests).toHaveLength(2), { timeout: 3000 })
    expect(requests[1].source).toBeUndefined()
  })
})
