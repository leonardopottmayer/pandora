import type { TagDto } from '../models'

// Mirrors the backend's Notes tag handling (TagParser + TagName, phase 08) so the preview shows the
// same tags the save is about to materialize.

const MAX_LENGTH = 50

/** slug → tag, for the preview to paint a `#tag` with the color the user picked. */
export type TagIndex = Map<string, TagDto>

/** Characters a tag may be made of: letters, digits, and the `- _ /` separators. */
const TAG_CHARS = String.raw`[\p{L}\p{N}_/-]`

/**
 * The lookup key of a tag: lower-cased, accents stripped, separators kept (`#projeto/pandora` is one
 * tag). Empty when nothing usable is left — a tag needs at least one letter, so `#123` is a number.
 */
export function tagSlug(raw: string): string {
  const slug = raw
    .trim()
    .replace(/^#/, '')
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(new RegExp(String.raw`[^\p{L}\p{N}_/-]`, 'gu'), '')
    .replace(/^[-_/]+|[-_/]+$/g, '')
    .slice(0, MAX_LENGTH)
    .replace(/^[-_/]+|[-_/]+$/g, '')

  return /\p{L}/u.test(slug) ? slug : ''
}

export function buildTagIndex(tags: TagDto[]): TagIndex {
  return new Map(tags.map((tag) => [tag.slug, tag]))
}

// One pass over the text: a code span (fenced or inline) is matched first and passed through
// untouched, so a `#comment` inside a shell block is never read as a tag — same rule as the backend.
// Written with quoted strings rather than a template: under the `u` flag an escaped backtick is an
// invalid escape, and a raw template would keep the backslash.
// A fence is written twice — closed, then running to the end of the text. `$` could not stand for
// the second case: the `m` flag this needs for the tag's line-start rule makes it an end of *line*.
const CODE_SPAN =
  '```[\\s\\S]*?```|```[\\s\\S]*|~~~[\\s\\S]*?~~~|~~~[\\s\\S]*|`[^`\\n]*`'

const SEGMENT_PATTERN = new RegExp(`(${CODE_SPAN})|(?<=^|\\s)#(${TAG_CHARS}+)`, 'gmu')

/** Every tag in the text, in order of first appearance, one per slug. */
export function parseTags(markdown: string): string[] {
  const slugs: string[] = []
  for (const match of markdown.matchAll(SEGMENT_PATTERN)) {
    if (match[1] !== undefined) continue
    const slug = tagSlug(match[2])
    if (slug.length > 0 && !slugs.includes(slug)) slugs.push(slug)
  }
  return slugs
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

/**
 * Rewrites `#tag` into a chip before the markdown is parsed, painted with the tag's color when it
 * has one. The chip carries its slug, which the preview's click handler turns into a filter.
 */
export function renderTags(markdown: string, index: TagIndex): string {
  return markdown.replace(SEGMENT_PATTERN, (match, code?: string, raw?: string) => {
    if (code !== undefined || raw === undefined) return match

    const slug = tagSlug(raw)
    if (slug.length === 0) return match

    // Standard properties rather than a custom one: the HTML goes through DOMPurify, whose CSS
    // filter is the reason not to depend on anything exotic surviving in a style attribute.
    const color = index.get(slug)?.color
    const style = color ? ` style="color: ${escapeHtml(color)}; border-color: ${escapeHtml(color)}"` : ''
    return `<span class="notes-tag" data-tag-slug="${escapeHtml(slug)}"${style}>#${escapeHtml(raw)}</span>`
  })
}

export interface TagTrigger {
  /** Offset in the line right after the `#` — the replacement starts here. */
  from: number
  /** What was typed after it, used to filter the tag list. */
  query: string
}

/**
 * The `#tag` being typed at `ch`, or `null` when the cursor isn't in one. Same rule as the parser:
 * the `#` has to start the line or follow a space, so a URL fragment never opens the menu.
 */
export function tagTriggerAt(line: string, ch: number): TagTrigger | null {
  const before = line.slice(0, ch)
  const hash = before.lastIndexOf('#')
  if (hash === -1) return null
  if (hash > 0 && !/\s/.test(before[hash - 1])) return null

  const query = before.slice(hash + 1)
  // A space ends the tag; the menu closes with it.
  if (/\s/.test(query)) return null

  return { from: hash + 1, query }
}

/** Tags whose name or slug matches what was typed after `#`, capped for the menu. */
export function filterTags(tags: TagDto[], query: string, limit = 10): TagDto[] {
  const term = tagSlug(query) || query.trim().toLowerCase()
  const matches =
    term.length === 0
      ? tags
      : tags.filter(
          (tag) => tag.slug.includes(term) || tag.name.toLowerCase().includes(term),
        )
  return matches.slice(0, limit)
}
