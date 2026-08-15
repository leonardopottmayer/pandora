import { beforeEach, describe, expect, it, vi } from 'vitest'
import { waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { MarkdownPreview } from './MarkdownPreview'

vi.mock('../services/attachments.service', () => ({
  fetchAttachmentBlob: vi.fn(async () => new Blob(['x'], { type: 'image/png' })),
  downloadAttachment: vi.fn(async () => {}),
}))

const { downloadAttachment } = await import('../services/attachments.service')

const PATH = '/api/v1/notes/attachments/aaaaaaaa-0000-0000-0000-000000000001'

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
    const { getByText } = renderWithProviders(
      <MarkdownPreview markdown={`[manual.pdf](${PATH})`} />,
    )

    await user.click(getByText('manual.pdf'))

    expect(downloadAttachment).toHaveBeenCalledWith(PATH, 'manual.pdf')
  })

  it('leaves an ordinary link alone', async () => {
    const user = userEvent.setup()
    const { getByText } = renderWithProviders(
      <MarkdownPreview markdown="[site](https://exemplo.com)" />,
    )

    await user.click(getByText('site'))

    expect(downloadAttachment).not.toHaveBeenCalled()
  })
})
