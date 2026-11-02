import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { MarkdownPreview } from './MarkdownPreview'

vi.mock('../services/media.service', () => ({
  fetchMediaBlob: vi.fn(async () => new Blob(['x'], { type: 'image/png' })),
  downloadMedia: vi.fn(async () => {}),
  lookupMedia: vi.fn(async () => [
    {
      id: 'aaaaaaaa-0000-0000-0000-000000000001',
      contentType: 'application/pdf',
      sizeBytes: 3 * 1024 * 1024,
      originalFileName: 'manual-original.pdf',
      isDeleted: false,
    },
  ]),
}))

const { downloadMedia } = await import('../services/media.service')

const PATH = '/api/v1/notes/media/aaaaaaaa-0000-0000-0000-000000000001'

let created = 0

beforeEach(() => {
  vi.clearAllMocks()
  created = 0
  URL.createObjectURL = vi.fn(() => `blob:fake-${++created}`)
  URL.revokeObjectURL = vi.fn()
})

function image(container: HTMLElement) {
  return container.querySelector('img')!
}

describe('MarkdownPreview — embedded images', () => {
  it('renders an attachment image through an object url', async () => {
    const { container } = renderWithProviders(<MarkdownPreview markdown={`![foto](${PATH})`} />)

    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))
  })

  // Regression: the object url used to be written straight onto the DOM node, so any re-render that
  // did not change the markdown restored the API path and the image broke — which is what made it
  // vanish on a theme switch, a language switch, or any refetch that rebuilt a prop identity.
  it('keeps the image across a re-render that does not change the markdown', async () => {
    const markdown = `![foto](${PATH})`
    const { container, rerender } = renderWithProviders(
      <MarkdownPreview markdown={markdown} tagIndex={new Map()} pageIndex={new Map()} />,
    )
    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))

    // New Map identities: exactly what a refetch of the pages or of the tags produces.
    rerender(<MarkdownPreview markdown={markdown} tagIndex={new Map()} pageIndex={new Map()} />)

    expect(image(container).getAttribute('src')).toMatch(/^blob:/)
  })

  it('keeps the image while the text around it is edited', async () => {
    const markdown = `![foto](${PATH})`
    const { container, rerender } = renderWithProviders(<MarkdownPreview markdown={markdown} />)
    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))

    rerender(<MarkdownPreview markdown={`escrevendo\n\n${markdown}`} />)

    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))
  })
})

describe('MarkdownPreview — attachment links', () => {
  it('downloads instead of navigating, keeping the original file name', async () => {
    const user = userEvent.setup()
    const { container } = renderWithProviders(<MarkdownPreview markdown={`[manual.pdf](${PATH})`} />)

    // The card redraws once the size arrives; click the one that stays.
    await waitFor(() => expect(container.querySelector('.notes-file-meta')?.textContent).toBe('3.0 MB'))
    await user.click(screen.getByText('manual.pdf'))

    expect(downloadMedia).toHaveBeenCalledWith(PATH, 'manual-original.pdf')
  })

  it('leaves an ordinary link alone', async () => {
    const user = userEvent.setup()
    const { getByText } = renderWithProviders(
      <MarkdownPreview markdown="[site](https://exemplo.com)" />,
    )

    await user.click(getByText('site'))

    expect(downloadMedia).not.toHaveBeenCalled()
  })
})

describe('MarkdownPreview — library media', () => {
  it('draws an image at the width its alt text asks for, with the text as a caption', async () => {
    const { container } = renderWithProviders(<MarkdownPreview markdown={`![Jurerê beach|400](${PATH})`} />)

    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))
    expect(image(container).getAttribute('width')).toBe('400')
    expect(screen.getByText('Jurerê beach')).toHaveClass('notes-caption')
  })

  it('shows no caption for an image named after its file', async () => {
    const { container } = renderWithProviders(<MarkdownPreview markdown={`![IMG_2034.jpg](${PATH})`} />)

    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))
    expect(container.querySelector('.notes-caption')).toBeNull()
  })

  it('opens an image full screen on a click', async () => {
    const user = userEvent.setup()
    const { container } = renderWithProviders(<MarkdownPreview markdown={`![foto](${PATH})`} />)
    await waitFor(() => expect(image(container).getAttribute('src')).toMatch(/^blob:/))

    await user.click(image(container))

    await waitFor(() => expect(document.querySelector('.ant-image-preview')).not.toBeNull())
  })

  it('draws a file link as a card with its size, saving under its original name', async () => {
    const user = userEvent.setup()
    const { container } = renderWithProviders(<MarkdownPreview markdown={`[Manual](${PATH})`} />)

    await waitFor(() => expect(container.querySelector('.notes-file-meta')?.textContent).toBe('3.0 MB'))
    await user.click(screen.getByText('Manual'))

    expect(downloadMedia).toHaveBeenCalledWith(PATH, 'manual-original.pdf')
  })
})
