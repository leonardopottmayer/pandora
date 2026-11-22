import type { TagDto } from '../models'

// Mirrors the backend's TagName, so a tag typed in the header reads as the one the save keeps.

const MAX_LENGTH = 50

/** slug → tag, to paint a tag with the color the user picked. */
export type TagIndex = Map<string, TagDto>

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

/** Tags whose name or slug matches what was typed in the tags field, capped for the menu. */
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
