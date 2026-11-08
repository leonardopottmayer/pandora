import type { PageSummaryDto } from '../models'

/** Pages whose title or slug matches what was typed, capped for a menu. */
export function filterPages(pages: PageSummaryDto[], query: string, limit = 10): PageSummaryDto[] {
  const term = query.trim().toLowerCase()
  const matches =
    term.length === 0
      ? pages
      : pages.filter((page) => page.title.toLowerCase().includes(term) || page.slug.toLowerCase().includes(term))
  return matches.slice(0, limit)
}
