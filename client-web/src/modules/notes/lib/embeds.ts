import type { RendererObject, Tokens } from 'marked'
import DOMPurify from 'dompurify'

// Players from a short list of sites. A YouTube or Vimeo link alone on a line draws as the player —
// the markdown is still just the link — and an <iframe> written by hand survives the sanitizer only
// when it points at one of those sites (Google Maps' "embed a map" code included). Every frame that
// survives is sandboxed and lazy, whatever attributes it was written with.

/** Where an <iframe> may point: the embed paths of YouTube, Vimeo and Google Maps. */
const ALLOWED_FRAMES: { host: string; path: string }[] = [
  { host: 'www.youtube.com', path: '/embed/' },
  { host: 'www.youtube-nocookie.com', path: '/embed/' },
  { host: 'player.vimeo.com', path: '/video/' },
  { host: 'www.google.com', path: '/maps/embed' },
]

export function isAllowedFrameSrc(src: string | null): boolean {
  if (!src) return false
  try {
    const url = new URL(src)
    return url.protocol === 'https:' && ALLOWED_FRAMES.some((f) => url.hostname === f.host && url.pathname.startsWith(f.path))
  } catch {
    return false
  }
}

/** `90`, `90s`, `1m30s`, `1h2m3s` → seconds; anything else → null. */
function seconds(value: string | null): number | null {
  const match = value?.match(/^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s?)?$/)
  if (!match || !value) return null
  const total = Number(match[1] ?? 0) * 3600 + Number(match[2] ?? 0) * 60 + Number(match[3] ?? 0)
  return total > 0 ? total : null
}

/** The player URL for a YouTube or Vimeo watch link, or null for any other URL. */
export function videoEmbedUrl(link: string): string | null {
  let url: URL
  try {
    url = new URL(link)
  } catch {
    return null
  }
  const host = url.hostname.replace(/^(www|m)\./, '')

  let youtubeId: string | null = null
  if (host === 'youtu.be') youtubeId = url.pathname.slice(1)
  else if (host === 'youtube.com' || host === 'youtube-nocookie.com') {
    youtubeId = url.pathname === '/watch' ? url.searchParams.get('v') : (url.pathname.match(/^\/(?:shorts|embed|live)\/([^/]+)/)?.[1] ?? null)
  }
  if (youtubeId && /^[\w-]{11}$/.test(youtubeId)) {
    const start = seconds(url.searchParams.get('t') ?? url.searchParams.get('start'))
    return `https://www.youtube-nocookie.com/embed/${youtubeId}${start ? `?start=${start}` : ''}`
  }

  if (host === 'vimeo.com') {
    // An unlisted video carries its hash as a second segment: vimeo.com/{id}/{hash}.
    const match = url.pathname.match(/^\/(\d+)(?:\/([0-9a-f]+))?\/?$/)
    if (match) return `https://player.vimeo.com/video/${match[1]}${match[2] ? `?h=${match[2]}` : ''}`
  }
  return null
}

/** A paragraph that is nothing but a video link becomes the player; any other falls through to marked. */
export const embedRenderer: RendererObject = {
  paragraph({ text }: Tokens.Paragraph) {
    const link = text.trim().replace(/^<(.*)>$/, '$1')
    const player = /\s/.test(link) ? null : videoEmbedUrl(link)
    if (!player) return false
    return `<div class="notes-video"><iframe src="${player}" title="${link.replace(/"/g, '&quot;')}"></iframe></div>\n`
  },
}

/** The attributes every frame of a note carries, whatever it was written with. */
export function lockFrame(frame: Element) {
  // Scripts and same-origin are what the players need to run; the frame is another origin, so
  // together they still give it no reach into this page. Popups let "watch on YouTube" open a tab.
  frame.setAttribute('sandbox', 'allow-scripts allow-same-origin allow-presentation allow-popups allow-popups-to-escape-sandbox')
  frame.setAttribute('allow', 'encrypted-media; picture-in-picture; fullscreen')
  frame.setAttribute('allowfullscreen', '')
  // YouTube refuses to play for a page that sends no referrer.
  frame.setAttribute('referrerpolicy', 'strict-origin-when-cross-origin')
  frame.setAttribute('loading', 'lazy')
}

// A purifier of its own: the hook below must not reach the other places that sanitize HTML (email).
const purifier = DOMPurify(window)
purifier.addHook('afterSanitizeAttributes', (node) => {
  if (node.nodeName !== 'IFRAME') return
  if (!isAllowedFrameSrc(node.getAttribute('src'))) {
    node.remove()
    return
  }
  lockFrame(node)
})

/** Sanitizes a note's rendered HTML, keeping only the frames the allowlist accepts. */
export function sanitizeNoteHtml(html: string): string {
  return purifier.sanitize(html, { ADD_TAGS: ['iframe'] })
}
