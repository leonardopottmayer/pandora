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
    expect(container.querySelector('.notes-raw-block .notes-callout-tip')).not.toBeNull()
  })

  it('opens the page a wikilink points at, or creates the missing one', async () => {
    const { props } = renderEditor('[[Roadmap]] [[Nada]]')

    await userEvent.click(await screen.findByText('Roadmap'))
    expect(props.onOpenPage).toHaveBeenCalledWith('p1')
    await userEvent.click(screen.getByText('Nada'))
    expect(props.onCreatePage).toHaveBeenCalledWith('Nada')
  })

  it('writes a raw block back as markdown once its source is edited', async () => {
    const { container, props } = renderEditor('> [!note] Antes\n> x')

    await waitFor(() => expect(container.querySelector('.notes-raw-edit')).not.toBeNull())
    await userEvent.click(container.querySelector('.notes-raw-edit')!)
    const source = container.querySelector<HTMLTextAreaElement>('.notes-raw-source')!
    fireEvent.change(source, { target: { value: '> [!note] Depois\n' } })
    // React's onBlur listens to focusout.
    fireEvent.focusOut(source)

    await waitFor(() => expect(props.onChange).toHaveBeenLastCalledWith('> [!note] Depois'))
  })

  it('does not rewrite a page that was only opened', async () => {
    const { props } = renderEditor('* a\n* b')
    await screen.findByText('a')
    expect(props.onChange).not.toHaveBeenCalled()
  })
})
