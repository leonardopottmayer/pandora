import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import type { Block } from '../../lib/blocks'
import type { PageSummaryDto } from '../../models'
import { VisualEditor } from './VisualEditor'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'

vi.mock('../../services/media.service', () => ({
  fetchMediaBlob: vi.fn(async () => new Blob(['x'], { type: 'image/png' })),
  downloadMedia: vi.fn(async () => {}),
  lookupMedia: vi.fn(async () => []),
}))
vi.mock('../../hooks/useSpaces', () => ({ useSpaces: () => ({ data: [] }) }))

const MEDIA = 'aaaaaaaa-0000-0000-0000-000000000001'
const page = { id: 'p1', spaceId: 's1', parentId: null, title: 'Roadmap', slug: 'roadmap', icon: '🗺️' } as PageSummaryDto

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:fake')
  // ProseMirror maps clicks to positions with it; jsdom has no layout to answer.
  document.elementFromPoint = () => null
  // Nor any rects for a range, which it asks for to scroll the cursor into view.
  Range.prototype.getClientRects = () => [] as unknown as DOMRectList
  Range.prototype.getBoundingClientRect = () => new DOMRect()
})

function renderEditor(blocks: Block[], overrides: Partial<Parameters<typeof VisualEditor>[0]> = {}) {
  const props = {
    docId: 'd1',
    initialContent: blocks,
    pages: [page],
    onChange: vi.fn(),
    onUpload: vi.fn(),
    onOpenPage: vi.fn(),
    onCreatePage: vi.fn(async () => 'p2'),
    ...overrides,
  }
  return { ...renderWithProviders(<VisualEditor {...props} />), props }
}

const text = (id: string, value: string): Block => ({ id, type: 'paragraph', content: [{ text: value }] })

describe('VisualEditor', () => {
  it('inserts an AI block from the slash menu', async () => {
    const { container, props } = renderEditor([text('p', '')])
    const dom = container.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(dom)
    await userEvent.type(dom, '/ai')
    await userEvent.click(await screen.findByRole('option', { name: 'AI block' }))
    expect(await screen.findByRole('textbox', { name: 'AI block prompt' })).toBeInTheDocument()
    expect(vi.mocked(props.onChange).mock.lastCall?.[0]).toEqual(expect.arrayContaining([expect.objectContaining({ type: 'ai' })]))
  })

  it('previews and discards regeneration, then accepts it with the Assistant origin and server time', async () => {
    server.use(http.post(`${TEST_API_BASE}/api/v1/notes/ai/generate`, async ({ request }) => {
      expect(await request.json()).toEqual({ prompt: 'Write a checklist' })
      return HttpResponse.json({ text: 'New checklist', generatedAt: '2026-11-07T18:00:00Z' })
    }))
    const { props } = renderEditor([{ id: 'ai1', type: 'ai', props: { prompt: 'Write a checklist', output: [{ text: 'Old checklist' }] } }])
    await userEvent.click(await screen.findByRole('button', { name: 'Generate again' }))
    await screen.findByText('New checklist')
    expect(screen.getByText('Old checklist')).toBeInTheDocument()
    expect(props.onChange).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Discard' }))
    expect(screen.queryByText('New checklist')).not.toBeInTheDocument()
    expect(props.onChange).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Generate again' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Accept result' }))
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith([
      { id: 'ai1', type: 'ai', props: { prompt: 'Write a checklist', output: [{ text: 'New checklist' }], generatedAt: '2026-11-07T18:00:00Z' } },
    ], 'assistant'))
    expect(screen.queryByText('Old checklist')).not.toBeInTheDocument()
  })

  it('keeps the accepted result when the prompt changes, and clears an obsolete preview', async () => {
    server.use(http.post(`${TEST_API_BASE}/api/v1/notes/ai/generate`, () => HttpResponse.json({ text: 'Preview text', generatedAt: '2026-11-07T18:00:00Z' })))
    const { props } = renderEditor([{ id: 'ai1', type: 'ai', props: { prompt: 'Original prompt', output: [{ text: 'Accepted text' }] } }])
    await userEvent.click(await screen.findByRole('button', { name: 'Generate again' }))
    await screen.findByText('Preview text')
    fireEvent.change(screen.getByRole('textbox', { name: 'AI block prompt' }), { target: { value: 'Different prompt' } })
    await waitFor(() => expect(screen.queryByText('Preview text')).not.toBeInTheDocument())
    expect(screen.getByText('Accepted text')).toBeInTheDocument()
    expect(props.onChange).toHaveBeenLastCalledWith([{ id: 'ai1', type: 'ai', props: { prompt: 'Different prompt', output: [{ text: 'Accepted text' }] } }])
  })

  it('shows AI block errors without changing the accepted output', async () => {
    server.use(http.post(`${TEST_API_BASE}/api/v1/notes/ai/generate`, () => HttpResponse.json({ success: false, errorCode: 'Assistant.NotEnabled', errorMessage: 'Enable the Assistant first.' }, { status: 422 })))
    const { props } = renderEditor([{ id: 'ai1', type: 'ai', props: { prompt: 'Prompt', output: [{ text: 'Kept result' }] } }])
    await userEvent.click(await screen.findByRole('button', { name: 'Generate again' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Enable the Assistant first.')
    expect(screen.getByText('Kept result')).toBeInTheDocument()
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('only reads the saved AI output on public or locked pages, treating generated HTML as text', async () => {
    const { container, props } = renderEditor([{ id: 'ai1', type: 'ai', props: { prompt: 'Saved prompt', output: [{ text: '<img src=x onerror=alert(1)> Result' }] } }], { editable: false, publicView: true })
    expect(await screen.findByText('<img src=x onerror=alert(1)> Result')).toBeInTheDocument()
    expect(screen.getByText('Saved prompt')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Generate again' })).not.toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: 'AI block prompt' })).not.toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('removes AI generation controls when the open page switches to read only', async () => {
    const view = renderEditor([{ id: 'ai1', type: 'ai', props: { prompt: 'Prompt', output: [{ text: 'Saved output' }] } }])
    expect(await screen.findByRole('button', { name: 'Generate again' })).toBeInTheDocument()
    view.rerender(<VisualEditor {...view.props} editable={false} />)
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Generate again' })).not.toBeInTheDocument())
    expect(screen.getByText('Saved output')).toBeInTheDocument()
    expect(view.props.onChange).not.toHaveBeenCalled()
  })

  it('continues through the slash menu and marks only the accepted insertion as assistant', async () => {
    server.use(http.post(`${TEST_API_BASE}/api/v1/notes/ai/transform`, () => HttpResponse.json({ text: 'Generated continuation' })))
    const { container, props } = renderEditor([text('p', '')])
    const dom = container.querySelector('.ProseMirror') as HTMLElement
    await userEvent.click(dom)
    await userEvent.type(dom, 'Previous words /continue')
    await userEvent.click(await screen.findByRole('option', { name: /Continue with AI/ }))
    await userEvent.click(await screen.findByRole('button', { name: 'Generate' }))
    await screen.findByText('Generated continuation')
    expect(vi.mocked(props.onChange).mock.calls.some((call) => call[1] === 'assistant')).toBe(false)
    await userEvent.click(screen.getByRole('button', { name: 'Insert below' }))
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith(expect.arrayContaining([
      expect.objectContaining({ content: [{ text: 'Generated continuation' }] }),
    ]), 'assistant'))
  })

  it('draws the blocks: headings, page links in a box, images and callouts', async () => {
    const { container } = renderEditor([
      { id: 'h', type: 'heading', props: { level: 1 }, content: [{ text: 'Plano' }] },
      { id: 'p', type: 'paragraph', content: [{ text: 'Ver ' }, { mention: { page: 'p1' } }, { text: ' e ' }, { mention: { page: 'gone' } }] },
      { id: 'i', type: 'image', props: { mediaId: MEDIA, align: 'right' }, content: [{ text: 'Praia' }] },
      { id: 'c', type: 'callout', props: { tone: 'info', title: 'Dica' }, children: [text('cb', 'corpo')] },
    ])

    expect(await screen.findByRole('heading', { name: 'Plano' })).toBeInTheDocument()
    expect(screen.getByText('Roadmap').closest('.notes-mention')).not.toBeNull()
    expect(container.querySelector('.notes-mention-missing')).not.toBeNull()
    await waitFor(() => expect(container.querySelector('.notes-figure-right img')?.getAttribute('src')).toBe('blob:fake'))
    expect(container.querySelector('.notes-native-block.notes-callout-info')).not.toBeNull()
    expect(screen.getByRole('textbox', { name: 'Callout title' })).toHaveValue('Dica')
  })

  it('draws colours, collapsible headings, columns and page cards, each with its block id', async () => {
    const { container, props } = renderEditor([
      { id: 'sp', type: 'paragraph', props: { color: 'red', background: 'blue', align: 'center' }, content: [{ text: 'Pintado' }] },
      { id: 'sh', type: 'heading', props: { level: 2, toggle: true, open: true }, content: [{ text: 'Seção' }], children: [text('sb', 'Corpo')] },
      { id: 'cs', type: 'columns', children: [
        { id: 'k1', type: 'column', children: [text('k1p', 'Esquerda')] },
        { id: 'k2', type: 'column', children: [text('k2p', 'Direita')] },
      ] },
      { id: 'pl', type: 'page-link', props: { pageId: 'p1' } },
    ])

    const painted = (await screen.findByText('Pintado')).closest('p')!
    expect(painted).toHaveAttribute('data-color', 'red')
    expect(painted).toHaveAttribute('data-background', 'blue')
    expect(painted).toHaveStyle({ textAlign: 'center' })
    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument()
    expect(container.querySelector('[data-block-id="sh"]')).not.toBeNull()
    expect(container.querySelectorAll('[data-columns] > [data-column]')).toHaveLength(2)
    expect(container.querySelector('[data-block-id="pl"] .notes-page-link-card')).toHaveTextContent('Roadmap')

    await userEvent.click(screen.getByRole('link', { name: /Roadmap/ }))
    expect(props.onOpenPage).toHaveBeenCalledWith('p1')
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('draws decisions, sub-pages, properties and formulas', async () => {
    const child = { ...page, id: 'c1', parentId: 'd1', title: 'Filha', orderIndex: 0, isArchived: false } as PageSummaryDto
    const { container, props } = renderEditor(
      [
        { id: 'dc', type: 'decision', props: { status: 'decided' }, content: [{ text: 'Usar outbox' }] },
        { id: 'sp', type: 'subpages' },
        { id: 'pr', type: 'properties', props: { rows: [{ key: [{ text: 'Dono' }], value: [{ text: 'Ana' }] }] } },
        { id: 'mt', type: 'math', props: { latex: 'x^2' } },
      ],
      { pages: [page, child] },
    )

    expect(await screen.findByText('Usar outbox')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Change the decision status' })).toHaveTextContent('Decided')
    await userEvent.click(screen.getByRole('button', { name: /Filha/ }))
    expect(props.onOpenPage).toHaveBeenCalledWith('c1')
    expect(screen.getAllByRole('textbox', { name: 'Property' })[0]).toHaveValue('Dono')
    await waitFor(() => expect(container.querySelector('.notes-math .katex')).not.toBeNull())
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('puts a copy of the template below its button, with fresh ids', async () => {
    const { props } = renderEditor([
      { id: 'tb', type: 'template-button', props: { label: 'Nova reunião' }, children: [{ id: 'tp', type: 'paragraph', content: [{ text: 'Pauta' }] }] },
    ])
    await userEvent.click(await screen.findByRole('button', { name: /Nova reunião/ }))
    await waitFor(() => {
      const blocks = vi.mocked(props.onChange).mock.lastCall?.[0] as Block[]
      expect(blocks.map((block) => block.type)).toEqual(['template-button', 'paragraph'])
      expect(blocks[1].id).not.toBe('tp')
      expect(blocks[1].content).toEqual([{ text: 'Pauta' }])
    })
  })

  it('draws chips: a date by name, a status, and another module item that opens it', async () => {
    const today = new Date()
    const value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`
    const { container, props } = renderEditor([
      { id: 'p', type: 'paragraph', content: [
        { date: { value, reminderId: 'r1' } }, { text: ' ' },
        { status: { label: 'Bloqueado', color: 'red' } }, { text: ' ' },
        { item: { module: 'finances', kind: 'account', id: 'a1', label: 'Nubank' } },
      ] },
    ])
    expect(await screen.findByText(/Today/)).toHaveTextContent('⏰')
    expect(container.querySelector('.notes-chip-status')).toHaveAttribute('data-color', 'red')
    expect(screen.getByRole('link', { name: /Nubank/ })).toBeInTheDocument()
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('turns $x^2$ typed in the text into a formula', async () => {
    const { container, props } = renderEditor([text('p', '')])
    const dom = container.querySelector('.ProseMirror') as HTMLElement
    await waitFor(() => expect(dom).toBeTruthy())
    await userEvent.click(dom)
    await userEvent.type(dom, 'Área $x^2$')
    await waitFor(() =>
      expect(props.onChange).toHaveBeenLastCalledWith([expect.objectContaining({ content: [{ text: 'Área ' }, { math: { latex: 'x^2' } }] })]),
    )
  })

  it('opens the page a link points at', async () => {
    const { props } = renderEditor([{ id: 'p', type: 'paragraph', content: [{ mention: { page: 'p1' } }] }])

    await userEvent.click(await screen.findByText('Roadmap'))
    expect(props.onOpenPage).toHaveBeenCalledWith('p1')
  })

  it('saves the callout title right away, keeping its body and the ids', async () => {
    const { props } = renderEditor([
      { id: 'c', type: 'callout', props: { tone: 'note', title: 'Antes' }, children: [{ id: 'b', type: 'paragraph', content: [{ text: 'corpo', marks: [{ type: 'bold' }] }] }] },
    ])
    const title = await screen.findByRole('textbox', { name: 'Callout title' })
    fireEvent.change(title, { target: { value: 'Depois' } })

    await waitFor(() =>
      expect(props.onChange).toHaveBeenLastCalledWith([
        { id: 'c', type: 'callout', props: { tone: 'note', title: 'Depois' }, children: [{ id: 'b', type: 'paragraph', content: [{ text: 'corpo', marks: [{ type: 'bold' }] }] }] },
      ]),
    )
  })

  it('expands and collapses a toggle without losing its body', async () => {
    const closed: Block = { id: 't', type: 'toggle', content: [{ text: 'Título' }], children: [text('b', 'corpo')] }
    const { container, props } = renderEditor([closed])

    await userEvent.click(await screen.findByRole('button', { name: 'Expand block' }))
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith([{ ...closed, props: { open: true } }]))
    await userEvent.click(screen.getByRole('button', { name: 'Collapse block' }))
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith([closed]))
    expect(container.querySelector('.notes-native-body')).toHaveTextContent('corpo')
  })

  it('draws embeds as the site frame, sandboxed, and bookmarks as cards that open the site', async () => {
    const { container, props } = renderEditor([
      { id: 'e', type: 'embed', props: { url: 'https://youtu.be/dQw4w9WgXcQ' } },
      { id: 'm', type: 'embed', props: { url: 'https://www.google.com/maps/@-27.59,-48.54,14z' } },
      { id: 'b', type: 'bookmark', props: { url: 'https://microservices.io/outbox', title: 'Transactional outbox', siteName: 'microservices.io' } },
    ])

    const frames = await waitFor(() => {
      const found = container.querySelectorAll('.notes-rich-frame, .notes-rich-video')
      expect(found).toHaveLength(2)
      return found
    })
    expect(frames[0]).toHaveAttribute('src', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ')
    expect(frames[0].getAttribute('sandbox')).not.toContain('allow-top-navigation')
    expect(frames[1].getAttribute('src')).toContain('output=embed')
    const card = screen.getByRole('link', { name: /Transactional outbox/ })
    expect(card).toHaveAttribute('href', 'https://microservices.io/outbox')
    expect(card).toHaveAttribute('target', '_blank')
    expect(container.querySelector('[data-block-id="b"]')).not.toBeNull()
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('offers an empty gallery, audio and PDF their link or the library', async () => {
    renderEditor([
      { id: 'g', type: 'gallery' },
      { id: 'a', type: 'audio' },
      { id: 'p', type: 'pdf' },
    ])
    expect(await screen.findByRole('button', { name: /Add image/ })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /From the library/ })).toHaveLength(2)
    expect(screen.getByRole('textbox', { name: 'Paste a link to a PDF' })).toBeInTheDocument()
  })

  it('asks what a link pasted alone on a line becomes, and makes it a bookmark', async () => {
    const { container, props } = renderEditor([text('p', '')])
    const dom = container.querySelector('.ProseMirror') as HTMLElement
    await waitFor(() => expect(dom).toBeTruthy())
    fireEvent.paste(dom, { clipboardData: { getData: (type: string) => (type === 'text/plain' ? 'https://a.example/post' : ''), files: [], types: ['text/plain'] } })

    const option = await screen.findByRole('option', { name: 'Bookmark' })
    fireEvent.mouseDown(option)
    await waitFor(() =>
      expect(props.onChange).toHaveBeenLastCalledWith([expect.objectContaining({ type: 'bookmark', props: { url: 'https://a.example/post' } })]),
    )
  })

  it('draws a library file as its card, with the size, and saves it on click', async () => {
    const { lookupMedia, downloadMedia } = await import('../../services/media.service')
    vi.mocked(lookupMedia).mockResolvedValueOnce([
      { id: MEDIA, sizeBytes: 2048, contentType: 'application/pdf', originalFileName: 'r.pdf', isDeleted: false } as never,
    ])
    const { container, props } = renderEditor([{ id: 'f', type: 'file', props: { mediaId: MEDIA, name: 'Relatório' } }])

    await waitFor(() => expect(container.querySelector('.notes-file-card .notes-file-meta')).toHaveTextContent('2.0 KB'))
    expect(container.querySelector('.notes-file-glyph')).toHaveTextContent('📕')
    await userEvent.click(screen.getByText('Relatório'))
    expect(downloadMedia).toHaveBeenCalledWith(`/api/v1/notes/media/${MEDIA}`, 'r.pdf')
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('keeps a block type it does not know, untouched', async () => {
    const future: Block = { id: 'k', type: 'kanban-board', props: { columns: 3 } }
    const { props } = renderEditor([future, text('p', 'depois')])

    expect(await screen.findByText(/kanban-board/)).toBeInTheDocument()
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('reads without editing when it is not editable', async () => {
    const { container } = renderEditor([text('p', 'só leitura')], { editable: false })
    await screen.findByText('só leitura')
    expect(container.querySelector('[contenteditable="true"]')).toBeNull()
  })

  it('does not rewrite a page that was only opened', async () => {
    const { props } = renderEditor([
      { id: 'a', type: 'bulleted', content: [{ text: 'a' }] },
      { id: 'b', type: 'bulleted', content: [{ text: 'b' }] },
    ])
    await screen.findByText('a')
    expect(props.onChange).not.toHaveBeenCalled()
  })
})
