import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { buildPageIndex } from '../../lib/wikilinks'
import type { PageSummaryDto } from '../../models'
import { VisualEditor } from './VisualEditor'

vi.mock('../../services/media.service', () => ({
  fetchMediaBlob: vi.fn(async () => new Blob(['x'], { type: 'image/png' })),
  downloadMedia: vi.fn(async () => {}),
  lookupMedia: vi.fn(async () => []),
}))
vi.mock('../../hooks/useSpaces', () => ({ useSpaces: () => ({ data: [] }) }))

const PATH = '/api/v1/notes/media/aaaaaaaa-0000-0000-0000-000000000001'
const page = { id: 'p1', spaceId: 's1', parentId: null, title: 'Roadmap', slug: 'roadmap', icon: null } as PageSummaryDto

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:fake')
  // ProseMirror maps clicks to positions with it; jsdom has no layout to answer.
  document.elementFromPoint = () => null
})

function renderEditor(markdown: string, overrides: Partial<Parameters<typeof VisualEditor>[0]> = {}) {
  const props = {
    docId: 'd1',
    initialValue: markdown,
    pages: [page],
    tags: [],
    pageIndex: buildPageIndex([page]),
    tagIndex: new Map(),
    onChange: vi.fn(),
    onUpload: vi.fn(),
    onOpenPage: vi.fn(),
    onCreatePage: vi.fn(),
    onSelectTag: vi.fn(),
    ...overrides,
  }
  return { ...renderWithProviders(<VisualEditor {...props} />), props }
}

describe('VisualEditor', () => {
  it('draws the page the way the preview does', async () => {
    const { container } = renderEditor(
      `# Plano\n\nVer [[Roadmap]] e [[Nada]].\n\n![Praia|300|right](${PATH})\n\n> [!tip] Dica\n> corpo`,
    )

    expect(await screen.findByRole('heading', { name: 'Plano' })).toBeInTheDocument()
    expect(screen.getByText('Roadmap')).toHaveClass('notes-wikilink')
    expect(screen.getByText('Nada')).toHaveClass('notes-wikilink-missing')
    await waitFor(() => expect(container.querySelector('.notes-figure-right img')?.getAttribute('src')).toBe('blob:fake'))
    expect(container.querySelector('.notes-native-block.notes-callout-tip')).not.toBeNull()
    expect(screen.getByRole('textbox', { name: 'Callout title' })).toHaveValue('Dica')
    expect(container.querySelector('.notes-raw-source')).toBeNull()
  })

  it('opens the page a wikilink points at, or creates the missing one', async () => {
    const { props } = renderEditor('[[Roadmap]] [[Nada]]')

    await userEvent.click(await screen.findByText('Roadmap'))
    expect(props.onOpenPage).toHaveBeenCalledWith('p1')
    await userEvent.click(screen.getByText('Nada'))
    expect(props.onCreatePage).toHaveBeenCalledWith('Nada')
  })

  it('writes a raw block back as markdown once its source is edited', async () => {
    const { container, props } = renderEditor('<div>Antes</div>')

    await waitFor(() => expect(container.querySelector('.notes-raw-edit')).not.toBeNull())
    await userEvent.click(container.querySelector('.notes-raw-edit')!)
    const source = container.querySelector<HTMLTextAreaElement>('.notes-raw-source')!
    fireEvent.change(source, { target: { value: '<div>Depois</div>\n' } })
    // React's onBlur listens to focusout.
    fireEvent.focusOut(source)

    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith('<div>Depois</div>'))
  })

  it('saves the title immediately while preserving the formatted editable body', async () => {
    const { container, props } = renderEditor('> [!note] Antes\n> **body**')
    const title = await screen.findByRole('textbox', { name: 'Callout title' })
    fireEvent.change(title, { target: { value: 'Depois' } })
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith('> [!note] Depois\n> **body**'))
    expect(container.querySelector('.notes-native-body strong')).toHaveTextContent('body')
    expect(container.querySelector('.notes-raw-source')).toBeNull()
  })

  it('undoes and redoes title changes through the editor history', async () => {
    const { props } = renderEditor('> [!note] Antes\n> body')
    const title = await screen.findByRole('textbox', { name: 'Callout title' })
    fireEvent.change(title, { target: { value: 'Depois' } })
    await waitFor(() => expect(title).toHaveValue('Depois'))
    fireEvent.keyDown(title, { key: 'z', ctrlKey: true })
    await waitFor(() => expect(title).toHaveValue('Antes'))
    expect(props.onChange).toHaveBeenLastCalledWith('> [!note] Antes\n> body')
    fireEvent.keyDown(title, { key: 'y', ctrlKey: true })
    await waitFor(() => expect(title).toHaveValue('Depois'))
    expect(props.onChange).toHaveBeenLastCalledWith('> [!note] Depois\n> body')
  })

  it.each([
    ['> [!tip]- Title\n> body', '> [!tip]+ Title\n> body'],
    ['<details>\n<summary>Title</summary>\n\nbody\n\n</details>', '<details open>\n<summary>Title</summary>\n\nbody\n\n</details>'],
  ])('expands and collapses without discarding the body: %s', async (closed, open) => {
    const { container, props } = renderEditor(closed)
    await userEvent.click(await screen.findByRole('button', { name: 'Expand block' }))
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith(open))
    expect(container.querySelector('.notes-native-block')).toHaveAttribute('data-collapsed', 'false')
    await userEvent.click(screen.getByRole('button', { name: 'Collapse block' }))
    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith(closed))
    expect(container.querySelector('.notes-native-body')).toHaveTextContent('body')
  })

  it('does not save when opening native blocks', async () => {
    const { props } = renderEditor('> [!note] Title\n> body\n\n<details>\n<summary>S</summary>\n\nx\n\n</details>')
    await screen.findByRole('textbox', { name: 'Callout title' })
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('draws the player under a video link alone on its line, and only there', async () => {
    const { container, props } = renderEditor('https://youtu.be/dQw4w9WgXcQ\n\nVer https://youtu.be/dQw4w9WgXcQ depois')
    await screen.findByText('depois', { exact: false })

    const frames = container.querySelectorAll('.notes-video iframe')
    expect(frames).toHaveLength(1)
    expect(frames[0]).toHaveAttribute('src', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ')
    expect(frames[0].getAttribute('sandbox')).toContain('allow-scripts')
    expect(screen.getAllByRole('link', { name: 'https://youtu.be/dQw4w9WgXcQ' })).toHaveLength(2)
    expect(props.onChange).not.toHaveBeenCalled()
  })

  it('does not rewrite a page that was only opened', async () => {
    const { props } = renderEditor('* a\n* b')
    await screen.findByText('a')
    expect(props.onChange).not.toHaveBeenCalled()
  })
})
