import type { PageSummaryDto } from '../models'

// Mirrors the backend's Notes wikilink handling (WikilinkParser + Slugger, phase 04) so the preview
// resolves a link exactly the way the save that materializes the PageLink edge will.

const SLUG_MAX_LENGTH = 80

/** Title/slug → page, both keys lower-cased. Built once per page list. */
export type PageIndex = Map<string, PageSummaryDto>

/** Lower-cased, accents stripped, non-alphanumerics collapsed to single hyphens. */
export function slugify(title: string): string {
  const slug = title
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^\p{L}\p{N}]+/gu, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, SLUG_MAX_LENGTH)
    .replace(/^-+|-+$/g, '')

  return slug.length === 0 ? 'untitled' : slug
}

export function buildPageIndex(pages: PageSummaryDto[]): PageIndex {
  const index: PageIndex = new Map()
  // Slugs are unique; titles are not, so the first page wins — same as the backend's resolution.
  for (const page of pages) if (!index.has(page.slug)) index.set(page.slug, page)
  for (const page of pages) {
    const title = page.title.trim().toLowerCase()
    if (!index.has(title)) index.set(title, page)
  }
  return index
}

/** The page a `[[target]]` points at, or `undefined` when the link is broken. */
export function resolveWikilink(target: string, index: PageIndex): PageSummaryDto | undefined {
  return index.get(target.trim().toLowerCase()) ?? index.get(slugify(target))
}

export interface WikilinkTrigger {
  /** Offset in the line right after the `[[` — the replacement starts here. */
  from: number
  /** What was typed after the brackets, used to filter the page list. */
  query: string
}

/**
 * The `[[target` being typed at `ch`, or `null` when the cursor isn't in one. `![[` triggers too:
 * an embed's target is written the same way.
 */
export function wikilinkTriggerAt(line: string, ch: number): WikilinkTrigger | null {
  const before = line.slice(0, ch)
  const open = before.lastIndexOf('[[')
  if (open === -1) return null

  const query = before.slice(open + 2)
  // `]` closes the link and `|` starts the alias — past either, the target is no longer being typed.
  if (/[[\]|]/.test(query)) return null

  return { from: open + 2, query }
}

/** Pages whose title or slug matches what was typed after `[[`, capped for the menu. */
export function filterPages(
  pages: PageSummaryDto[],
  query: string,
  limit = 10,
): PageSummaryDto[] {
  const term = query.trim().toLowerCase()
  const matches =
    term.length === 0
      ? pages
      : pages.filter(
          (page) =>
            page.title.toLowerCase().includes(term) || page.slug.toLowerCase().includes(term),
        )
  return matches.slice(0, limit)
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

// Kept in step with the backend regex: the target stops at the alias pipe, `!` marks an embed.
const WIKILINK_PATTERN = /(!)?\[\[([^[\]|]*)(?:\|([^[\]]*))?\]\]/g

/**
 * Rewrites `[[target]]` / `[[target|alias]]` / `![[target]]` into anchors before the markdown is
 * parsed. Resolved links carry the target's id; broken ones carry the text that was typed, which is
 * what the "create on click" flow needs. Both are plain anchors — the preview's click handler turns
 * them into navigation.
 */
export function renderWikilinks(markdown: string, index: PageIndex): string {
  return markdown.replace(WIKILINK_PATTERN, (match, _bang, rawTarget: string, alias?: string) => {
    const target = (rawTarget ?? '').trim()
    if (target.length === 0) return match

    const page = resolveWikilink(target, index)
    const label = escapeHtml((alias ?? '').trim() || (page ? page.title : target))

    return page
      ? `<a class="notes-wikilink" data-page-id="${escapeHtml(page.id)}" href="#">${label}</a>`
      : `<a class="notes-wikilink notes-wikilink-missing" data-page-title="${escapeHtml(target)}" href="#">${label}</a>`
  })
}
