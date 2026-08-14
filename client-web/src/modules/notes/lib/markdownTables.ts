// Assisted editing for GFM pipe tables. Everything here works on the raw markdown text — the
// document the user edits stays markdown, the table is just realigned in place.

export interface TableBlock {
  /** Index of the table's first line in the document. */
  startLine: number
  /** Index of the table's last line in the document. */
  endLine: number
  /** One row per line, already split into trimmed cells. */
  rows: string[][]
  /** Index within `rows` of the `---|---` separator, or -1 when the table has none yet. */
  separatorRow: number
}

const SEPARATOR_CELL = /^:?-{1,}:?$/

function isTableLine(line: string): boolean {
  return line.trim().startsWith('|')
}

/** `| a | b |` → `['a', 'b']`. The outer pipes are structure, not cells. */
export function splitRow(line: string): string[] {
  const trimmed = line.trim().replace(/^\|/, '').replace(/\|$/, '')
  return trimmed.split('|').map((cell) => cell.trim())
}

function isSeparatorRow(cells: string[]): boolean {
  return cells.length > 0 && cells.every((cell) => SEPARATOR_CELL.test(cell))
}

/** The contiguous run of pipe lines around `line`, or `null` when the cursor isn't in a table. */
export function findTableAt(lines: string[], line: number): TableBlock | null {
  if (line < 0 || line >= lines.length || !isTableLine(lines[line])) return null

  let startLine = line
  while (startLine > 0 && isTableLine(lines[startLine - 1])) startLine--

  let endLine = line
  while (endLine < lines.length - 1 && isTableLine(lines[endLine + 1])) endLine++

  const rows = lines.slice(startLine, endLine + 1).map(splitRow)
  return { startLine, endLine, rows, separatorRow: rows.findIndex(isSeparatorRow) }
}

/** Visible width of a cell — the separator stretches to the column, it doesn't set its width. */
function columnWidths(rows: string[][], separatorRow: number): number[] {
  const widths: number[] = []
  rows.forEach((cells, rowIndex) => {
    cells.forEach((cell, columnIndex) => {
      const width = rowIndex === separatorRow ? 3 : cell.length
      widths[columnIndex] = Math.max(widths[columnIndex] ?? 3, width)
    })
  })
  return widths
}

/**
 * Rewrites a table's lines with every column padded to the same width, so the markdown source is
 * readable as a table too. Ragged rows are padded out to the widest one.
 */
export function formatTable(table: TableBlock): string[] {
  const widths = columnWidths(table.rows, table.separatorRow)

  return table.rows.map((cells, rowIndex) => {
    const padded = widths.map((width, columnIndex) => {
      const cell = cells[columnIndex] ?? ''
      if (rowIndex !== table.separatorRow) return cell.padEnd(width)
      // Keep the alignment colons the user typed, stretching the dashes between them.
      const left = cell.startsWith(':') ? ':' : '-'
      const right = cell.endsWith(':') ? ':' : '-'
      return left + '-'.repeat(Math.max(1, width - 2)) + right
    })
    return `| ${padded.join(' | ')} |`
  })
}

export interface CellPosition {
  /** Line index in the document. */
  line: number
  /** Cell index within that line's row. */
  column: number
}

/** Which cell of `line` the character offset `ch` sits in. */
export function cellAt(line: string, ch: number): number {
  const before = line.slice(0, ch)
  const pipes = (before.match(/\|/g) ?? []).length
  // Before the leading pipe still counts as the first cell.
  return Math.max(0, pipes - 1)
}

/**
 * Where Tab (or Shift+Tab) lands from `position`, skipping the separator row. Walking off the last
 * cell returns `null` — the caller appends a fresh row and lands on its first cell.
 */
export function nextCell(
  table: TableBlock,
  position: CellPosition,
  direction: 1 | -1,
): CellPosition | null {
  const columns = Math.max(...table.rows.map((row) => row.length))
  let row = position.line - table.startLine
  let column = position.column + direction

  if (column >= columns) {
    column = 0
    row++
  } else if (column < 0) {
    column = columns - 1
    row--
  }

  // The separator holds no content: step over it in the direction of travel.
  if (row === table.separatorRow) row += direction

  if (row < 0 || row >= table.rows.length) return null
  return { line: table.startLine + row, column }
}

/** Character offset of the start of a cell's content, on an already formatted line. */
export function cellOffset(line: string, column: number): number {
  let offset = 0
  for (let seen = 0; seen <= column; seen++) {
    const pipe = line.indexOf('|', offset)
    if (pipe === -1) return line.length
    offset = pipe + 1
  }
  // Land after the single space that padding puts between the pipe and the content.
  return line[offset] === ' ' ? offset + 1 : offset
}

/** The same table with a blank row appended — where Tab lands when it runs off the last cell. */
export function withEmptyRow(table: TableBlock): TableBlock {
  const columns = Math.max(...table.rows.map((row) => row.length))
  return {
    ...table,
    rows: [...table.rows, Array<string>(columns).fill('')],
    endLine: table.endLine + 1,
  }
}
