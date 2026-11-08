import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { TEST_API_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import type { AiTextTarget } from '../../lib/aiText'
import { AiTextPreview } from './AiTextPreview'

const URL = `${TEST_API_BASE}/api/v1/notes/ai/transform`
const target = { action: 'improve', text: 'Original words' } as AiTextTarget
beforeAll(async () => { await i18n.changeLanguage('en') })

describe('AI text preview', () => {
  it('generates a saved-page summary through its route and offers only insertion at the top', async () => {
    const apply = vi.fn()
    server.use(http.post(`${TEST_API_BASE}/api/v1/notes/pages/p1/ai/summary`, () => HttpResponse.json({ text: 'Page summary' })))
    renderWithProviders(<AiTextPreview target={{ ...target, text: '', pageSummary: true }} pageId="p1" onApply={apply} onClose={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: 'Generate' }))
    await screen.findByText('Page summary')
    expect(apply).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Replace selection' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Insert summary at the top' }))
    expect(apply).toHaveBeenCalledWith('Page summary', 'below')
  })

  it('waits for acceptance and can discard generated text without touching the document', async () => {
    const apply = vi.fn()
    const close = vi.fn()
    server.use(http.post(URL, () => HttpResponse.json({ text: 'Better words' })))
    renderWithProviders(<AiTextPreview target={target} onApply={apply} onClose={close} />)
    await userEvent.click(screen.getByRole('button', { name: 'Generate' }))
    expect(await screen.findByText('Better words')).toBeInTheDocument()
    expect(apply).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Discard' }))
    expect(close).toHaveBeenCalledOnce()
    expect(apply).not.toHaveBeenCalled()
  })

  it.each(['Replace selection', 'Insert below'])('applies the preview through %s', async (label) => {
    const apply = vi.fn()
    server.use(http.post(URL, () => HttpResponse.json({ text: 'Better words' })))
    renderWithProviders(<AiTextPreview target={target} onApply={apply} onClose={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: 'Generate' }))
    await userEvent.click(await screen.findByRole('button', { name: label }))
    expect(apply).toHaveBeenCalledWith('Better words', label === 'Insert below' ? 'below' : 'replace')
  })

  it('sends the chosen translation language and resets the result if it changes', async () => {
    let sent: unknown
    server.use(http.post(URL, async ({ request }) => { sent = await request.json(); return HttpResponse.json({ text: 'Translated' }) }))
    renderWithProviders(<AiTextPreview target={{ ...target, action: 'translate' }} onApply={vi.fn()} onClose={vi.fn()} />)
    const language = screen.getByRole('textbox', { name: 'Translate into' })
    await userEvent.clear(language)
    await userEvent.type(language, 'French')
    await userEvent.click(screen.getByRole('button', { name: 'Generate' }))
    await screen.findByText('Translated')
    expect(sent).toEqual({ action: 'translate', text: 'Original words', language: 'French' })
    await userEvent.type(language, ' Canadian')
    expect(screen.queryByText('Translated')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Replace selection' })).not.toBeInTheDocument()
  })

  it('shows a friendly provider error and leaves the document alone', async () => {
    const apply = vi.fn()
    server.use(http.post(URL, () => HttpResponse.json({ success: false, errorCode: 'Assistant.NotEnabled', errorMessage: 'Enable the Assistant first.' }, { status: 422 })))
    renderWithProviders(<AiTextPreview target={target} onApply={apply} onClose={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: 'Generate' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Enable the Assistant first.')
    expect(apply).not.toHaveBeenCalled()
  })

  it('does not send oversized selections', () => {
    renderWithProviders(<AiTextPreview target={{ ...target, text: 'x'.repeat(8001) }} onApply={vi.fn()} onClose={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Generate' })).toBeDisabled()
  })
})
