// What an embed block draws for a link: a site's own player or viewer from a short list, by
// rewriting the link into the site's embed address. Every frame is sandboxed and lazy. A site off the
// list goes through the link preview (oEmbed), or shows as a card.

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

/** How an embed block draws a link: a frame of the site, its height (or a video's 16:9), or a frame we write. */
export interface EmbedFrame {
  provider: string
  /** The site's embed address; absent when `html` is used. */
  src?: string
  /** Markup for a frame we write (a gist's script), drawn sandboxed without our origin. */
  html?: string
  /** Fixed height in pixels; absent for a 16:9 video. */
  height?: number
}

function parse(link: string): URL | null {
  try {
    const url = new URL(link.trim())
    return url.protocol === 'https:' || url.protocol === 'http:' ? url : null
  } catch {
    return null
  }
}

/** The frame for a link from one of the known sites, or null. */
export function embedFrame(link: string): EmbedFrame | null {
  const video = videoEmbedUrl(link)
  if (video) return { provider: video.includes('vimeo') ? 'Vimeo' : 'YouTube', src: video }
  const url = parse(link)
  if (!url) return null
  const host = url.hostname.replace(/^www\./, '')
  const path = url.pathname

  if (host === 'loom.com') {
    const id = path.match(/^\/(?:share|embed)\/([0-9a-f]+)/)?.[1]
    if (id) return { provider: 'Loom', src: `https://www.loom.com/embed/${id}` }
  }
  if (host === 'figma.com' && /^\/(file|design|proto|board|slides)\//.test(path)) {
    return { provider: 'Figma', src: `https://www.figma.com/embed?embed_host=pandora&url=${encodeURIComponent(url.href)}`, height: 450 }
  }
  if ((host === 'google.com' || host === 'maps.google.com') && (path.startsWith('/maps') || host === 'maps.google.com')) {
    const at = path.match(/@(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)(?:,(\d+(?:\.\d+)?)z)?/)
    const place = path.match(/\/place\/([^/]+)/)?.[1]
    const q = url.searchParams.get('q') ?? (place ? decodeURIComponent(place.replace(/\+/g, ' ')) : at ? `${at[1]},${at[2]}` : null)
    if (q) {
      const zoom = at?.[3] ? `&z=${Math.round(Number(at[3]))}` : ''
      return { provider: 'Google Maps', src: `https://maps.google.com/maps?q=${encodeURIComponent(q)}${zoom}&output=embed`, height: 400 }
    }
  }
  if (host === 'drive.google.com') {
    const file = path.match(/^\/file\/d\/([\w-]+)/)?.[1]
    if (file) return { provider: 'Google Drive', src: `https://drive.google.com/file/d/${file}/preview`, height: 520 }
    const folder = path.match(/^\/drive\/(?:u\/\d+\/)?folders\/([\w-]+)/)?.[1]
    if (folder) return { provider: 'Google Drive', src: `https://drive.google.com/embeddedfolderview?id=${folder}#list`, height: 420 }
  }
  if (host === 'docs.google.com') {
    const doc = path.match(/^\/(document|spreadsheets|presentation|forms)\/d\/(e\/)?([\w-]+)/)
    if (doc) {
      const tail = doc[1] === 'forms' ? 'viewform?embedded=true' : 'preview'
      return { provider: 'Google Docs', src: `https://docs.google.com/${doc[1]}/d/${doc[2] ?? ''}${doc[3]}/${tail}`, height: 600 }
    }
  }
  if (host === 'miro.com') {
    const board = path.match(/^\/app\/board\/([^/]+)/)?.[1]
    if (board) return { provider: 'Miro', src: `https://miro.com/app/live-embed/${board}/`, height: 450 }
  }
  if (host === 'codepen.io') {
    const pen = path.match(/^\/([\w-]+)\/(?:pen|full|details)\/(\w+)/)
    if (pen) return { provider: 'CodePen', src: `https://codepen.io/${pen[1]}/embed/${pen[2]}?default-tab=result`, height: 420 }
  }
  if (host === 'gist.github.com') {
    const gist = path.match(/^\/([\w-]+)\/([0-9a-f]+)\/?$/)
    if (gist) return { provider: 'GitHub Gist', html: `<script src="https://gist.github.com/${gist[1]}/${gist[2]}.js"></script>` }
  }
  return null
}

/** A direct link to a video or an audio file, which the browser plays on its own. */
export function mediaFileKind(link: string): 'video' | 'audio' | null {
  const path = parse(link)?.pathname.toLowerCase() ?? ''
  if (/\.(mp4|webm|mov|m4v|ogv)$/.test(path)) return 'video'
  if (/\.(mp3|m4a|aac|wav|ogg|oga|opus|flac)$/.test(path)) return 'audio'
  return null
}

/**
 * A page for a frame we write ourselves (an oEmbed's HTML, a gist): the site's markup and scripts,
 * and a line that tells this page how tall it grew. The frame runs without our origin, so whatever
 * the markup does, it cannot reach the app.
 */
export function frameDocument(html: string): string {
  const resize = `<script>new ResizeObserver(function(){parent.postMessage({pandoraFrameHeight:document.documentElement.scrollHeight},'*')}).observe(document.documentElement)</script>`
  return `<!doctype html><html><head><meta charset="utf-8"><base target="_blank"><style>body{margin:0;font-family:system-ui,sans-serif}</style></head><body>${html}${resize}</body></html>`
}

/** What a frame we wrote may do: run its scripts and open links in a tab — never act as our origin. */
export const WRITTEN_FRAME_SANDBOX = 'allow-scripts allow-popups allow-popups-to-escape-sandbox'
