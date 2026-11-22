import { describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CommentDto } from '../models'
import { CommentsPanel } from './CommentsPanel'

const ok = (data: unknown) => HttpResponse.json({ success: true, data })
const now = new Date().toISOString()
const comment = (over: Partial<CommentDto>): CommentDto => ({
  id: 'c', pageId: 'p1', blockId: 'b1', threadId: null, quote: null, body: '', resolvedAt: null, createdAt: now, editedAt: null, ...over,
})

const comments = [
  comment({ id: 't1', quote: 'março', body: 'Não seria abril?' }),
  comment({ id: 'r1', threadId: 't1', body: 'Abril, sim.' }),
  comment({ id: 't2', blockId: 'gone', body: 'Bloco velho' }),
  comment({ id: 't3', body: 'Já resolvido', resolvedAt: now }),
]

describe('CommentsPanel', () => {
  it('lists the open threads with their replies, and answers one', async () => {
    let replied: unknown = null
    server.use(
      http.post(`${TEST_API_BASE}/api/v1/notes/comments/t1/replies`, async ({ request }) => {
        replied = await request.json()
        return ok(comment({ id: 'r2', threadId: 't1', body: 'Ok' }))
      }),
      http.get(`${TEST_API_BASE}/api/v1/notes/pages/p1/comments`, () => ok(comments)),
    )
    const jump = vi.fn()
    renderWithProviders(
      <CommentsPanel pageId="p1" open comments={comments} focusThread={null} blockExists={(id) => id !== 'gone'} onJump={jump} onClose={() => undefined} />,
    )

    expect(await screen.findByText('Não seria abril?')).toBeInTheDocument()
    expect(screen.getByText('Abril, sim.')).toBeInTheDocument()
    expect(screen.getByText('Block removed')).toBeInTheDocument()
    expect(screen.queryByText('Já resolvido')).toBeNull()

    await userEvent.click(screen.getByText('março'))
    expect(jump).toHaveBeenCalledWith('b1')

    const thread = screen.getByText('Não seria abril?').closest('section')!
    await userEvent.type(within(thread).getByLabelText('Reply'), 'Ok')
    await userEvent.click(within(thread).getByRole('button', { name: 'Reply' }))
    await waitFor(() => expect(replied).toEqual({ body: 'Ok' }))
  })

  it('opens on the resolved list when the thread in focus is resolved', async () => {
    renderWithProviders(
      <CommentsPanel pageId="p1" open comments={comments} focusThread="t3" blockExists={() => true} onJump={() => undefined} onClose={() => undefined} />,
    )
    expect(await screen.findByText('Já resolvido')).toBeInTheDocument()
    expect(screen.queryByText('Não seria abril?')).toBeNull()
  })
})
