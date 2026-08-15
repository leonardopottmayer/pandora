// A page's icon is a single emoji stored as text (nte001_page.icon). What arrives from an input can
// be anything, so it is normalized here before it reaches the draft.

/** A few starters for the picker, so it is usable without opening the system emoji panel. */
export const SUGGESTED_ICONS = [
  '📄', '📝', '📌', '📚', '💡', '🎯', '🚀', '🔧',
  '🐛', '💰', '🗓️', '✅', '⭐', '🔥', '🧠', '🏠',
]

/**
 * The first grapheme of what was typed, or `null` when nothing is left. A grapheme rather than a
 * char because an emoji is routinely several code points — a flag, a skin tone, a family — and
 * slicing it by index would produce a different emoji, or half of one.
 */
export function normalizeIcon(raw: string): string | null {
  const text = raw.trim()
  if (text.length === 0) return null

  if (typeof Intl !== 'undefined' && 'Segmenter' in Intl) {
    const [first] = new Intl.Segmenter().segment(text)
    return first?.segment ?? null
  }

  // Without Segmenter, code points at least keep surrogate pairs whole.
  return [...text][0] ?? null
}
