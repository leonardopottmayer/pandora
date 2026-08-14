import { describe, it, expect } from 'vitest'
import {
  cellAt,
  cellOffset,
  findTableAt,
  formatTable,
  nextCell,
  splitRow,
  withEmptyRow,
} from './markdownTables'

const doc = [
  'intro',
  '| Name | Qty |',
  '| --- | ---: |',
  '| Apple | 3 |',
  '| Pear | 12 |',
  'outro',
]

describe('findTableAt', () => {
  it('finds the run of pipe lines around the cursor', () => {
    const table = findTableAt(doc, 3)
    expect(table).not.toBeNull()
    expect(table!.startLine).toBe(1)
    expect(table!.endLine).toBe(4)
    expect(table!.separatorRow).toBe(1)
  })

  it.each([0, 5])('returns null outside a table (line %i)', (line) => {
    expect(findTableAt(doc, line)).toBeNull()
  })

  it('reports no separator for a table that has none yet', () => {
    expect(findTableAt(['| a | b |'], 0)!.separatorRow).toBe(-1)
  })
})

describe('splitRow', () => {
  it('drops the outer pipes and trims the cells', () => {
    expect(splitRow('|  Name |Qty  |')).toEqual(['Name', 'Qty'])
  })
})

describe('formatTable', () => {
  it('pads every column to the same width', () => {
    expect(formatTable(findTableAt(doc, 1)!)).toEqual([
      '| Name  | Qty |',
      '| ----- | --: |',
      '| Apple | 3   |',
      '| Pear  | 12  |',
    ])
  })

  it('pads ragged rows out to the widest one', () => {
    const table = findTableAt(['| a | b |', '| --- | --- |', '| x |'], 0)!
    expect(formatTable(table)[2]).toBe('| x   |     |')
  })

  it('is idempotent', () => {
    const once = formatTable(findTableAt(doc, 1)!)
    expect(formatTable(findTableAt(once, 0)!)).toEqual(once)
  })
})

describe('cellAt', () => {
  it.each([
    [0, 0],
    [3, 0],
    [9, 1],
  ])('offset %i is in cell %i', (ch, expected) => {
    expect(cellAt('| Name | Qty |', ch)).toBe(expected)
  })
})

describe('nextCell', () => {
  const table = findTableAt(doc, 1)!

  it('moves to the next cell on the same row', () => {
    expect(nextCell(table, { line: 1, column: 0 }, 1)).toEqual({ line: 1, column: 1 })
  })

  it('wraps to the next row, skipping the separator', () => {
    expect(nextCell(table, { line: 1, column: 1 }, 1)).toEqual({ line: 3, column: 0 })
  })

  it('walks backwards over the separator too', () => {
    expect(nextCell(table, { line: 3, column: 0 }, -1)).toEqual({ line: 1, column: 1 })
  })

  it('returns null past the last cell', () => {
    expect(nextCell(table, { line: 4, column: 1 }, 1)).toBeNull()
  })

  it('returns null before the first cell', () => {
    expect(nextCell(table, { line: 1, column: 0 }, -1)).toBeNull()
  })
})

describe('cellOffset', () => {
  it('lands on the content of the requested cell', () => {
    const line = '| Name  | Qty  |'
    expect(line.slice(cellOffset(line, 1))).toBe('Qty  |')
  })
})

describe('withEmptyRow', () => {
  it('appends a blank row matching the table width', () => {
    const grown = withEmptyRow(findTableAt(doc, 1)!)
    expect(grown.endLine).toBe(5)
    expect(formatTable(grown).at(-1)).toBe('|       |     |')
  })
})
