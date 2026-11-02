import { describe, expect, it } from 'vitest'
import { groupVersions } from './pageHistory'
import type { PageVersionSummaryDto } from '../models'

describe('history grouping', () => {
  it('keeps every intermediate save and separates pauses, restores and assistant edits', () => {
    const version = (number: number, minute: number, source = 'edit'): PageVersionSummaryDto => ({
      number, createdAt: new Date(Date.UTC(2026, 10, 2, 12, minute)).toISOString(), source, restoredFrom: null,
    })
    const versions = [version(6, 20), version(5, 19), version(4, 18, 'restore'), version(3, 17), version(2, 10), version(1, 9, 'assistant')]
    const groups = groupVersions(versions)
    expect(groups.map((group) => group.map((v) => v.number))).toEqual([[6, 5], [4], [3], [2], [1]])
    expect(groups.flat()).toEqual(versions)
  })
})
