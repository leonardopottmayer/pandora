import { describe, expect, it } from 'vitest'
import { isMediaUrl, mediaFileName } from './media'

const PATH = '/api/v1/notes/media/aaaaaaaa-0000-0000-0000-000000000001'

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
