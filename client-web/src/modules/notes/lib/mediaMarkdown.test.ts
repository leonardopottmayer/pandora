import { describe, expect, it } from 'vitest'
import { Marked } from 'marked'
import type { MediaItemDto } from '../models'
import { formatImageAlt, mediaRenderer, parseImageAlt, parseMediaFileIds } from './mediaMarkdown'

const ID = '0193f000-0000-7000-8000-00000000a001'
const PATH = `/api/v1/notes/media/${ID}`

const pdf = { id: ID, contentType: 'application/pdf', sizeBytes: 2048, originalFileName: 'receipt.pdf', isDeleted: true } as MediaItemDto

const render = (md: string, files = new Map<string, MediaItemDto>()) =>
  new Marked({ renderer: mediaRenderer(files, 'removed from the library') }).parse(md, { async: false })

describe('parseImageAlt', () => {
  it('reads a width after the bar and keeps the text as a caption', () => {
    expect(parseImageAlt('Jurerê beach|400')).toEqual({ text: 'Jurerê beach', width: 400, align: null, caption: 'Jurerê beach' })
  })

  it('shows no caption for a bare file name, the way pasting writes it', () => {
    expect(parseImageAlt('IMG_2034.jpg')).toEqual({ text: 'IMG_2034.jpg', width: null, align: null, caption: null })
    expect(parseImageAlt('IMG_2034.jpg|300').caption).toBeNull()
  })

  it('reads an alignment in either language, before or after the width', () => {
    expect(parseImageAlt('Beach|400|direita')).toMatchObject({ text: 'Beach', width: 400, align: 'right' })
    expect(parseImageAlt('Beach|center|300')).toMatchObject({ text: 'Beach', width: 300, align: 'center' })
    expect(parseImageAlt('Beach|esquerda')).toMatchObject({ text: 'Beach', width: null, align: 'left' })
  })

  it('keeps a bar that is part of the text', () => {
    expect(parseImageAlt('before | after|200')).toMatchObject({ text: 'before | after', width: 200, align: null })
  })
})

describe('formatImageAlt', () => {
  it('writes back what parseImageAlt reads, even with no text', () => {
    for (const [text, width, align] of [['Beach', 300, 'right'], ['', 300, null], ['', null, 'center'], ['Beach', null, null]] as const) {
      expect(parseImageAlt(formatImageAlt(text, width, align))).toMatchObject({ text, width, align })
    }
  })
})

describe('mediaRenderer', () => {
  it('draws a library image at its width with a caption', () => {
    const html = render(`![Beach|400](${PATH})`)
    expect(html).toContain('<span class="notes-figure">')
    expect(html).toContain(`<img src="${PATH}" alt="Beach" loading="lazy" width="400">`)
    expect(html).toContain('<span class="notes-caption">Beach</span>')
  })

  it('marks an aligned image with its side', () => {
    expect(render(`![Beach|300|direita](${PATH})`)).toContain('<span class="notes-figure notes-figure-right">')
  })

  it('leaves images from elsewhere to marked', () => {
    expect(render('![x](https://example.com/a.png)')).toContain('<img src="https://example.com/a.png" alt="x">')
  })

  it('draws a file link as a card with its size, saying when it left the library', () => {
    const html = render(`[receipt.pdf](${PATH})`, new Map([[ID, pdf]]))
    expect(html).toContain('class="notes-file-card"')
    expect(html).toContain('📕')
    expect(html).toContain('2.0 KB · removed from the library')
    expect(html).toContain('data-file-name="receipt.pdf"')
  })

  it('finds the file links, not the images', () => {
    expect(parseMediaFileIds(`![a](${PATH}) [b](${PATH}) [c](/api/v1/notes/media/0193f000-0000-7000-8000-00000000a002)`)).toEqual([
      ID,
      '0193f000-0000-7000-8000-00000000a002',
    ])
  })
})
