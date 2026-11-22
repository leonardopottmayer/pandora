import DOMPurify from 'dompurify'

/** What a blocked remote image is replaced with: a transparent pixel, so the layout holds. */
export const BLOCKED_IMAGE = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7'

export interface PreparedMail {
  html: string
  /** Remote images replaced by the placeholder; "Show images" is offered when above zero. */
  blockedImages: number
}

export interface PrepareMailOptions {
  /** Load remote images (the sender learns the message was opened, and from where). */
  allowImages: boolean
  /** Inline images by Content-ID (without `<>`), as `data:` URLs. */
  inlineImages: ReadonlyMap<string, string>
}

const CSS_URL = /url\(\s*(['"]?)(.*?)\1\s*\)/gi

/**
 * Turns a received HTML body into something safe to show. DOMPurify removes scripts, handlers and
 * dangerous URLs; then every URL that would make the browser fetch something is rewritten — remote
 * images become a placeholder until the user allows them (a tracking pixel is a read receipt), and
 * `cid:` images point at the attachment's bytes. Links open in a new tab, without a referrer.
 *
 * This is the screen's security boundary, together with the iframe's sandbox and CSP (`mailSrcDoc`).
 */
export function prepareMailHtml(html: string, { allowImages, inlineImages }: PrepareMailOptions): PreparedMail {
  let blockedImages = 0

  /** A URL a resource loads from: `data:` stays, `cid:` resolves, the rest is remote. */
  function resolve(url: string): string {
    const value = url.trim()
    if (/^data:/i.test(value)) return value
    if (/^cid:/i.test(value)) {
      const id = safeDecode(value.slice(4)).replace(/^<|>$/g, '')
      return inlineImages.get(id) ?? BLOCKED_IMAGE
    }
    if (allowImages) return value
    blockedImages++
    return BLOCKED_IMAGE
  }

  const rewriteCss = (css: string) => css.replace(CSS_URL, (_, _quote, url: string) => `url("${resolve(url)}")`)

  // FORCE_BODY keeps the <style> blocks a full document carries in its <head>.
  const body = DOMPurify.sanitize(html, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['audio', 'video', 'source', 'track', 'link', 'meta', 'base', 'form'],
    FORCE_BODY: true,
    RETURN_DOM: true,
  }) as HTMLElement

  for (const el of Array.from(body.querySelectorAll('*'))) {
    const tag = el.tagName.toLowerCase()

    if (tag === 'style') {
      el.textContent = rewriteCss(el.textContent ?? '')
      continue
    }
    if (tag === 'a') {
      el.setAttribute('target', '_blank')
      el.setAttribute('rel', 'noopener noreferrer')
    }
    if (tag === 'img') {
      const src = el.getAttribute('src')
      if (src !== null) el.setAttribute('src', resolve(src))
      if (el.hasAttribute('srcset') && !allowImages) {
        el.removeAttribute('srcset')
        if (src === null) blockedImages++
      }
    }
    const background = el.getAttribute('background')
    if (background !== null) el.setAttribute('background', resolve(background))

    const style = el.getAttribute('style')
    if (style !== null && /url\(/i.test(style)) el.setAttribute('style', rewriteCss(style))
  }

  return { html: body.innerHTML, blockedImages }
}

/**
 * The iframe's document. The CSP is the second wall behind the rewrite: no scripts, no remote styles
 * or fonts, and images only from `data:` until the user allows remote ones.
 */
export function mailSrcDoc(html: string, allowImages: boolean): string {
  const csp = [
    "default-src 'none'",
    `img-src data:${allowImages ? ' https: http:' : ''}`,
    "style-src 'unsafe-inline'",
  ].join('; ')

  return `<!doctype html><html><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="${csp}">
<style>body{margin:16px;font-family:system-ui,sans-serif;font-size:14px;color:#1f1f1f;background:#fff;overflow-wrap:anywhere}img{max-width:100%;height:auto}</style>
</head><body>${html}</body></html>`
}

function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value)
  } catch {
    return value
  }
}
