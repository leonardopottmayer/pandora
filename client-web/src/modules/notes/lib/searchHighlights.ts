export interface SearchFragment {
  text: string
  matched: boolean
}

/** Literal word-prefix matches, matching the search's Unicode words and punctuation rules. */
export function searchHighlights(text: string, term: string): SearchFragment[] {
  const prefixes = (term.match(/[\p{L}\p{N}]+/gu) ?? []).map((word) => word.toLowerCase())
  if (prefixes.length === 0) return [{ text, matched: false }]

  const parts: SearchFragment[] = []
  let end = 0
  for (const word of text.matchAll(/[\p{L}\p{N}]+/gu)) {
    if (!prefixes.some((prefix) => word[0].toLowerCase().startsWith(prefix))) continue
    if (word.index > end) parts.push({ text: text.slice(end, word.index), matched: false })
    parts.push({ text: word[0], matched: true })
    end = word.index + word[0].length
  }
  if (end < text.length) parts.push({ text: text.slice(end), matched: false })
  return parts
}
