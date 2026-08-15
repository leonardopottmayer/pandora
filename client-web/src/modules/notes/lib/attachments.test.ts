import { describe, expect, it } from 'vitest'
import {
  attachmentFileName,
  isAttachmentUrl,
  parseAttachmentImagePaths,
  withAttachmentUrls,
} from './attachments'

const PATH = '/api/v1/notes/attachments/aaaaaaaa-0000-0000-0000-000000000001'
const OTHER = '/api/v1/notes/attachments/bbbbbbbb-0000-0000-0000-000000000002'

describe('parseAttachmentImagePaths', () => {
  it('finds embedded images', () => {
    expect(parseAttachmentImagePaths(`![foto](${PATH})`)).toEqual([PATH])
  })

  it('deduplicates the same image embedded twice', () => {
    expect(parseAttachmentImagePaths(`![a](${PATH}) e ![b](${PATH})`)).toEqual([PATH])
  })

  it('ignores plain links, which are only fetched when clicked', () => {
    expect(parseAttachmentImagePaths(`[manual.pdf](${PATH})`)).toEqual([])
  })

  it('ignores images that are not attachments', () => {
    expect(parseAttachmentImagePaths('![externa](https://exemplo.com/a.png)')).toEqual([])
  })
})

describe('withAttachmentUrls', () => {
  const urls = new Map([[PATH, 'blob:fake-1']])

  it('points an image at its object url', () => {
    expect(withAttachmentUrls(`<img src="${PATH}">`, urls)).toBe('<img src="blob:fake-1">')
  })

  it('leaves a link href alone, since the click handler needs the api path', () => {
    const html = `<a href="${PATH}">manual.pdf</a>`
    expect(withAttachmentUrls(html, urls)).toBe(html)
  })

  it('leaves an image with no url yet pointing at its path', () => {
    expect(withAttachmentUrls(`<img src="${OTHER}">`, urls)).toBe(`<img src="${OTHER}">`)
  })
})

describe('attachmentFileName', () => {
  it('prefers the markdown label, which is the original file name', () => {
    expect(attachmentFileName(PATH, 'manual.pdf')).toBe('manual.pdf')
  })

  it('falls back to the id when there is no label', () => {
    expect(attachmentFileName(PATH, '  ')).toBe('aaaaaaaa-0000-0000-0000-000000000001')
  })
})

describe('isAttachmentUrl', () => {
  it('recognizes the endpoint', () => {
    expect(isAttachmentUrl(PATH)).toBe(true)
    expect(isAttachmentUrl('https://exemplo.com/a.pdf')).toBe(false)
  })
})
