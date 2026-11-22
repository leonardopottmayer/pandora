import { describe, expect, it } from 'vitest'
import { BLOCKED_IMAGE, mailSrcDoc, prepareMailHtml } from './prepareMailHtml'

const none = new Map<string, string>()
const blocked = { allowImages: false, inlineImages: none }

function parse(html: string) {
  return new DOMParser().parseFromString(html, 'text/html')
}

describe('prepareMailHtml', () => {
  it('blocks remote images until they are allowed', () => {
    const html = `<img src="https://t.example/pixel.gif"><img srcset="https://t.example/a.png 2x">
      <table><tr><td background="http://t.example/bg.png"></td></tr></table><div style="background:url('https://t.example/x.png')"></div>`

    const off = prepareMailHtml(html, blocked)
    expect(off.blockedImages).toBe(4)
    expect(off.html).not.toContain('t.example')
    expect(parse(off.html).querySelector('img')!.getAttribute('src')).toBe(BLOCKED_IMAGE)

    const on = prepareMailHtml(html, { allowImages: true, inlineImages: none })
    expect(on.blockedImages).toBe(0)
    expect(on.html).toContain('https://t.example/pixel.gif')
    expect(on.html).toContain('https://t.example/a.png 2x')
  })

  it('rewrites url() inside style blocks, keeping the styles of a full document', () => {
    const html = `<html><head><style>.hero{background:url(https://t.example/h.png)}</style></head><body><p class="hero">Hi</p></body></html>`

    const out = prepareMailHtml(html, blocked)
    expect(out.html).toContain('<style>')
    expect(out.html).toContain(`url("${BLOCKED_IMAGE}")`)
    expect(out.html).not.toContain('t.example')
    expect(out.blockedImages).toBe(1)
  })

  it('removes scripts and event handlers', () => {
    const out = prepareMailHtml(
      `<script>alert(1)</script><img src="data:image/png;base64,AA==" onerror="alert(2)"><a href="javascript:alert(3)">x</a>`,
      blocked,
    )
    expect(out.html).not.toMatch(/script|onerror|javascript:/i)
  })

  it('points cid: images at the inline attachment', () => {
    const inline = new Map([['logo@mail', 'data:image/png;base64,TE9HTw==']])

    const out = prepareMailHtml(`<img src="cid:logo@mail"><img src="cid:missing@mail">`, {
      allowImages: false,
      inlineImages: inline,
    })

    const [found, missing] = Array.from(parse(out.html).querySelectorAll('img'))
    expect(found.getAttribute('src')).toBe('data:image/png;base64,TE9HTw==')
    expect(missing.getAttribute('src')).toBe(BLOCKED_IMAGE)
    expect(out.blockedImages).toBe(0)
  })

  it('opens links in a new tab without a referrer', () => {
    const a = parse(prepareMailHtml(`<a href="https://example.com">site</a>`, blocked).html).querySelector('a')!
    expect(a.getAttribute('target')).toBe('_blank')
    expect(a.getAttribute('rel')).toBe('noopener noreferrer')
  })
})

describe('mailSrcDoc', () => {
  function csp(doc: string) {
    return parse(doc).querySelector('meta[http-equiv="Content-Security-Policy"]')!.getAttribute('content')!
  }

  it('allows images only from data: until remote ones are allowed', () => {
    expect(csp(mailSrcDoc('<p>x</p>', false))).toContain("default-src 'none'; img-src data:;")
    expect(csp(mailSrcDoc('<p>x</p>', true))).toContain('img-src data: https: http:')
  })
})
