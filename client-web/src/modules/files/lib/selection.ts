import type { Mark, SelectionMode } from '../models'

// The selection model of product-plan §4.3, as the checkbox tree edits it: a folder takes the mode of
// its deepest marked ancestor (or its own mark), and is included when nothing is marked above it.

const ROOT = '/'

function same(a: string, b: string, caseSensitive: boolean): boolean {
  return caseSensitive ? a === b : a.toLowerCase() === b.toLowerCase()
}

/** Whether `path` is `ancestor` or lies below it. */
export function isWithin(path: string, ancestor: string, caseSensitive: boolean): boolean {
  if (ancestor === ROOT) return true
  const p = caseSensitive ? path : path.toLowerCase()
  const a = caseSensitive ? ancestor : ancestor.toLowerCase()
  return p === a || p.startsWith(a + '/')
}

/** The mode a folder ends up with. */
export function modeAt(marks: Mark[], path: string, caseSensitive: boolean): SelectionMode {
  let deepest: Mark | null = null
  for (const mark of marks) {
    if (isWithin(path, mark.path, caseSensitive) && (!deepest || mark.path.length > deepest.path.length)) deepest = mark
  }
  return deepest?.mode ?? 'include'
}

/** Whether something below the folder is marked the other way — drawn as a half-checked box. */
export function mixedBelow(marks: Mark[], path: string, caseSensitive: boolean): boolean {
  const mode = modeAt(marks, path, caseSensitive)
  return marks.some(
    (m) => m.mode !== mode && !same(m.path, path, caseSensitive) && isWithin(m.path, path, caseSensitive),
  )
}

/**
 * Flips the folder: it gets the other mode, and its own mark is kept only when that differs from what it
 * would inherit, so the set never carries a redundant mark. Marks below it stay (a re-included subfolder).
 */
export function toggle(marks: Mark[], path: string, caseSensitive: boolean): Mark[] {
  const flipped: SelectionMode = modeAt(marks, path, caseSensitive) === 'include' ? 'exclude' : 'include'
  const others = marks.filter((m) => !same(m.path, path, caseSensitive))
  return modeAt(others, path, caseSensitive) === flipped ? others : [...others, { path, mode: flipped }]
}
