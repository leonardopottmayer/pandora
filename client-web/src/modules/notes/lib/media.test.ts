import { describe, expect, it } from 'vitest'
import {
  mediaFileName,
  isMediaUrl,
  parseMediaImagePaths,
  withMediaUrls,
} from './media'

const PATH = '/api/v1/notes/media/aaaaaaaa-0000-0000-0000-000000000001'
const OTHER = '/api/v1/notes/media/bbbbbbbb-0000-0000-0000-000000000002'

describe('parseMediaImagePaths', () => {
  it('finds embedded images', () => {
    expect(parseMediaImagePaths(`![foto](${PATH})`)).toEqual([PATH])
  })

  it('deduplicates the same image embedded twice', () => {
    expect(parseMediaImagePaths(`![a](${PATH}) e ![b](${PATH})`)).toEqual([PATH])
  })

  it('ignores plain links, which are only fetched when clicked', () => {
    expect(parseMediaImagePaths(`[manual.pdf](${PATH})`)).toEqual([])
  })

  it('ignores images that are not attachments', () => {
    expect(parseMediaImagePaths('![externa](https://exemplo.com/a.png)')).toEqual([])
  })
})

describe('withMediaUrls', () => {
  const urls = new Map([[PATH, 'blob:fake-1']])

  it('points an image at its object url', () => {
    expect(withMediaUrls(`<img src="${PATH}">`, urls)).toBe('<img src="blob:fake-1">')
  })

  it('leaves a link href alone, since the click handler needs the api path', () => {
    const html = `<a href="${PATH}">manual.pdf</a>`
    expect(withMediaUrls(html, urls)).toBe(html)
  })

  it('leaves an image with no url yet pointing at its path', () => {
    expect(withMediaUrls(`<img src="${OTHER}">`, urls)).toBe(`<img src="${OTHER}">`)
  })
})

describe('mediaFileName', () => {
  it('prefers the markdown label, which is the original file name', () => {
    expect(mediaFileName(PATH, 'manual.pdf')).toBe('manual.pdf')
  })

  it('falls back to the id when there is no label', () => {
    expect(mediaFileName(PATH, '  ')).toBe('aaaaaaaa-0000-0000-0000-000000000001')
  })
})

describe('isMediaUrl', () => {
  it('recognizes the endpoint', () => {
    expect(isMediaUrl(PATH)).toBe(true)
    expect(isMediaUrl('https://exemplo.com/a.pdf')).toBe(false)
  })
})
