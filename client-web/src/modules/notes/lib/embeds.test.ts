import { describe, expect, it } from 'vitest'
import { embedFrame, mediaFileKind, videoEmbedUrl } from './embeds'

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

describe('embedFrame', () => {
  it.each([
    ['https://www.loom.com/share/0281766fa2d04bb788eaf19e65135184', 'https://www.loom.com/embed/0281766fa2d04bb788eaf19e65135184'],
    ['https://www.figma.com/design/AbC123/Tela', 'https://www.figma.com/embed?embed_host=pandora&url=https%3A%2F%2Fwww.figma.com%2Fdesign%2FAbC123%2FTela'],
    ['https://www.google.com/maps/@-27.59,-48.54,14z', 'https://maps.google.com/maps?q=-27.59%2C-48.54&z=14&output=embed'],
    ['https://www.google.com/maps/place/Florian%C3%B3polis/@-27.59,-48.54,12z', 'https://maps.google.com/maps?q=Florian%C3%B3polis&z=12&output=embed'],
    ['https://drive.google.com/file/d/1AbC-dEf/view?usp=sharing', 'https://drive.google.com/file/d/1AbC-dEf/preview'],
    ['https://docs.google.com/document/d/1AbC/edit', 'https://docs.google.com/document/d/1AbC/preview'],
    ['https://miro.com/app/board/uXjVabc=/', 'https://miro.com/app/live-embed/uXjVabc=/'],
    ['https://codepen.io/ana/pen/xYzAbc', 'https://codepen.io/ana/embed/xYzAbc?default-tab=result'],
    ['https://youtu.be/dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ'],
  ])('draws %s from the site frame', (link, src) => {
    expect(embedFrame(link)?.src).toBe(src)
  })

  it('writes the frame of a gist, the site gives none', () => {
    expect(embedFrame('https://gist.github.com/ana/0123abcd')?.html).toContain('https://gist.github.com/ana/0123abcd.js')
  })

  it.each(['https://github.com/ana/repo', 'https://x.com/ana/status/1', 'javascript:alert(1)', 'nada'])('leaves %s to the preview', (link) => {
    expect(embedFrame(link)).toBeNull()
  })

  it('knows a video or an audio file by its extension', () => {
    expect([mediaFileKind('https://a.example/v.mp4'), mediaFileKind('https://a.example/a.mp3?x=1'), mediaFileKind('https://a.example/p.html')]).toEqual(['video', 'audio', null])
  })
})
