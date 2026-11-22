import { describe, it, expect } from 'vitest'
import type { Mark } from '../models'
import { mixedBelow, modeAt, toggle } from './selection'

describe('selection', () => {
  it('takes the deepest marked ancestor, and includes what nothing marks', () => {
    const marks: Mark[] = [
      { path: '/C', mode: 'exclude' },
      { path: '/C/Sub', mode: 'include' },
    ]
    expect(modeAt(marks, '/A', true)).toBe('include')
    expect(modeAt(marks, '/C/Other', true)).toBe('exclude')
    expect(modeAt(marks, '/C/Sub/deep', true)).toBe('include')
    // A sibling whose name starts like a marked folder is not below it.
    expect(modeAt(marks, '/Cx', true)).toBe('include')
  })

  it('builds "only Sub inside C" with two clicks, and undoes it without leftovers', () => {
    let marks = toggle([], '/C', true)
    marks = toggle(marks, '/C/Sub', true)
    expect(marks).toEqual([
      { path: '/C', mode: 'exclude' },
      { path: '/C/Sub', mode: 'include' },
    ])
    expect(mixedBelow(marks, '/C', true)).toBe(true)

    marks = toggle(marks, '/C/Sub', true)
    expect(marks).toEqual([{ path: '/C', mode: 'exclude' }])
    expect(toggle(marks, '/C', true)).toEqual([])
  })

  it('excludes the root and re-includes folders below it', () => {
    let marks = toggle([], '/', true)
    marks = toggle(marks, '/A', true)
    expect(modeAt(marks, '/A/x', true)).toBe('include')
    expect(modeAt(marks, '/B', true)).toBe('exclude')
  })

  it('compares paths without case on a case-insensitive root', () => {
    const marks: Mark[] = [{ path: '/Movies', mode: 'exclude' }]
    expect(modeAt(marks, '/movies/a', false)).toBe('exclude')
    expect(modeAt(marks, '/movies/a', true)).toBe('include')
    expect(toggle(marks, '/MOVIES', false)).toEqual([])
  })
})
