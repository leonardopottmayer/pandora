import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import { AskNotesPanel } from './AskNotesPanel'

beforeAll(async () => { await i18n.changeLanguage('en') })
const source = { pageId: 'p1', spaceId: 's1', title: 'Apollo deadline', icon: null, excerpt: 'The deadline is March 20.' }

describe('AskNotesPanel', () => {
  it('asks within the current space and opens a cited source, rendering model text as plain text', async () => {
    let sent: unknown
    const select = vi.fn()
    server.use(http.post(`${NOTES_BASE}/ai/ask`, async ({ request }) => {
      sent = await request.json()
      return HttpResponse.json({ answer: '<img src=x onerror=alert(1)> March 20.', sources: [source] })
    }))
    const { container } = renderWithProviders(<AskNotesPanel spaceId="s1" onSelect={select} />)
    expect(screen.getByRole('button', { name: 'Ask' })).toBeDisabled()
    await userEvent.type(screen.getByRole('textbox', { name: 'Question for your notes' }), 'When is the Apollo deadline?')
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    expect(await screen.findByRole('region', { name: 'Answer from your notes' })).toHaveTextContent('<img src=x onerror=alert(1)> March 20.')
    expect(container.querySelector('img')).toBeNull()
    expect(sent).toEqual({ question: 'When is the Apollo deadline?', spaceId: 's1' })
    expect(screen.getByText(source.excerpt)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Apollo deadline/ }))
    expect(select).toHaveBeenCalledWith('p1')
  })

  it('widens to all spaces and clears an obsolete answer when the scope or question changes', async () => {
    const sent: unknown[] = []
    server.use(http.post(`${NOTES_BASE}/ai/ask`, async ({ request }) => {
      sent.push(await request.json())
      return HttpResponse.json({ answer: 'March 20', sources: [source] })
    }))
    renderWithProviders(<AskNotesPanel spaceId="s1" onSelect={vi.fn()} />)
    const question = screen.getByRole('textbox', { name: 'Question for your notes' })
    await userEvent.type(question, 'Apollo deadline')
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    await screen.findByText('March 20')
    await userEvent.click(screen.getByText('All spaces'))
    expect(screen.queryByText('March 20')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    await screen.findByText('March 20')
    expect(sent[1]).toEqual({ question: 'Apollo deadline', spaceId: null })
    await userEvent.type(question, ' changed')
    expect(screen.queryByText('March 20')).not.toBeInTheDocument()
  })

  it('shows insufficient information without an invented answer', async () => {
    server.use(http.post(`${NOTES_BASE}/ai/ask`, () => HttpResponse.json({ answer: null, sources: [] })))
    renderWithProviders(<AskNotesPanel spaceId={null} onSelect={vi.fn()} />)
    await userEvent.type(screen.getByRole('textbox', { name: 'Question for your notes' }), 'Apollo budget{Enter}')
    expect(await screen.findByRole('alert')).toHaveTextContent('Not enough information')
    expect(screen.queryByRole('region', { name: 'Answer from your notes' })).not.toBeInTheDocument()
  })

  it('shows localized errors and allows another attempt', async () => {
    server.use(http.post(`${NOTES_BASE}/ai/ask`, () => HttpResponse.json({ success: false, errorCode: 'Notes.InvalidAnswer', errorMessage: 'Try rephrasing your question.' }, { status: 422 })))
    renderWithProviders(<AskNotesPanel spaceId={null} onSelect={vi.fn()} />)
    await userEvent.type(screen.getByRole('textbox', { name: 'Question for your notes' }), 'Apollo deadline')
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Try rephrasing your question.')
    expect(screen.getByRole('button', { name: 'Ask' })).toBeEnabled()
  })
})
