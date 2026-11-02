import { describe, expect, it } from 'vitest'
import { Marked } from 'marked'
import { embedRenderer, sanitizeNoteHtml, videoEmbedUrl } from './embeds'

const render = (md: string) => sanitizeNoteHtml(new Marked({ renderer: embedRenderer }).parse(md, { async: false }))

describe('videoEmbedUrl', () => {
  it.each([
    ['https://www.youtube.com/watch?v=dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ'],
    ['https://youtu.be/dQw4w9WgXcQ?t=90', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=90'],
    ['https://m.youtube.com/watch?v=dQw4w9WgXcQ&t=1m30s', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=90'],
    ['https://www.youtube.com/shorts/dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ'],
    ['https://vimeo.com/76979871', 'https://player.vimeo.com/video/76979871'],
    ['https://vimeo.com/76979871/abc123', 'https://player.vimeo.com/video/76979871?h=abc123'],
  ])('turns %s into its player', (link, player) => {
    expect(videoEmbedUrl(link)).toBe(player)
  })

  it.each(['https://www.youtube.com/', 'https://example.com/watch?v=dQw4w9WgXcQ', 'not a url'])('ignores %s', (link) => {
    expect(videoEmbedUrl(link)).toBeNull()
  })
})

describe('embeds in a note', () => {
  it('draws a video link alone on a line as a sandboxed player', () => {
    const html = render('Before\n\nhttps://youtu.be/dQw4w9WgXcQ\n\nAfter')
    expect(html).toContain('<div class="notes-video"><iframe src="https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ"')
    expect(html).toContain('sandbox="allow-scripts allow-same-origin')
    expect(html).toContain('referrerpolicy="strict-origin-when-cross-origin"')
  })

  it('leaves a video link inside a sentence as a link', () => {
    const html = render('Watch https://youtu.be/dQw4w9WgXcQ later')
    expect(html).not.toContain('<iframe')
    expect(html).toContain('<a href="https://youtu.be/dQw4w9WgXcQ">')
  })

  it('keeps a hand-written map frame, sandboxed, whatever it asked for', () => {
    const html = render('<iframe src="https://www.google.com/maps/embed?pb=abc" height="300" allow="camera" srcdoc="<b>x</b>"></iframe>')
    expect(html).toContain('src="https://www.google.com/maps/embed?pb=abc"')
    expect(html).toContain('height="300"')
    expect(html).toContain('sandbox=')
    expect(html).not.toContain('camera')
    expect(html).not.toContain('srcdoc')
  })

  it.each([
    '<iframe src="https://evil.example.com/embed/x"></iframe>',
    '<iframe src="http://www.youtube.com/embed/dQw4w9WgXcQ"></iframe>',
    '<iframe src="https://www.google.com/search?q=x"></iframe>',
    '<iframe srcdoc="<script>alert(1)</script>"></iframe>',
  ])('drops a frame from anywhere else: %s', (md) => {
    expect(render(md)).not.toContain('<iframe')
  })
})
