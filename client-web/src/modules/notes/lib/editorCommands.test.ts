import { describe, it, expect, afterEach } from 'vitest'
import { EditorState } from '@codemirror/state'
import { EditorView } from '@codemirror/view'
import { CompletionContext } from '@codemirror/autocomplete'
import { moveTableCell, slashSource, wikilinkSource } from './editorCommands'
import type { SlashCommand } from './slashCommands'
import type { PageSummaryDto } from '../models'

let view: EditorView | null = null

afterEach(() => {
  view?.destroy()
  view = null
})

/** An editor over `doc` with the cursor at `anchor`, mounted so commands can dispatch. */
function editor(doc: string, anchor: number): EditorView {
  view = new EditorView({
    parent: document.body,
    state: EditorState.create({ doc, selection: { anchor } }),
  })
  return view
}

/** Offset of the character right after `needle` — where a test wants the cursor. */
function after(doc: string, needle: string): number {
  return doc.indexOf(needle) + needle.length
}

const table = ['| Name | Qty |', '| --- | --- |', '| Apple | 3 |'].join('\n')

describe('moveTableCell', () => {
  it('realigns the table and moves to the next cell', () => {
    const view = editor(table, after(table, '| Name'))
    expect(moveTableCell(1)(view)).toBe(true)

    expect(view.state.doc.toString()).toBe(
      ['| Name  | Qty |', '| ----- | --- |', '| Apple | 3   |'].join('\n'),
    )
    const line = view.state.doc.lineAt(view.state.selection.main.head)
    expect(line.number).toBe(1)
    expect(line.text.slice(view.state.selection.main.head - line.from)).toBe('Qty |')
  })

  it('skips the separator row on its way to the body', () => {
    const view = editor(table, after(table, '| Name | Qty'))
    moveTableCell(1)(view)
    expect(view.state.doc.lineAt(view.state.selection.main.head).number).toBe(3)
  })

  it('appends a row when tabbing off the last cell', () => {
    const view = editor(table, after(table, '| Apple | 3'))
    expect(moveTableCell(1)(view)).toBe(true)

    const lines = view.state.doc.toString().split('\n')
    expect(lines).toHaveLength(4)
    expect(lines[3]).toBe('|       |     |')
    expect(view.state.doc.lineAt(view.state.selection.main.head).number).toBe(4)
  })

  it('walks backwards with Shift+Tab', () => {
    const view = editor(table, after(table, '| Apple'))
    expect(moveTableCell(-1)(view)).toBe(true)
    expect(view.state.doc.lineAt(view.state.selection.main.head).number).toBe(1)
  })

  it('declines before the first cell, leaving the document untouched', () => {
    const view = editor(table, after(table, '| Name'))
    expect(moveTableCell(-1)(view)).toBe(false)
    expect(view.state.doc.toString()).toBe(table)
  })

  it('declines outside a table', () => {
    const view = editor('just a paragraph', 4)
    expect(moveTableCell(1)(view)).toBe(false)
  })

  it('declines on a lone pipe line that is not a table yet', () => {
    const view = editor('| a | b |', 4)
    expect(moveTableCell(1)(view)).toBe(false)
  })

  it('declines when text is selected', () => {
    view = new EditorView({
      parent: document.body,
      state: EditorState.create({ doc: table, selection: { anchor: 2, head: 6 } }),
    })
    expect(moveTableCell(1)(view)).toBe(false)
  })
})

const label = (command: SlashCommand) => `label:${command.id}`
const source = slashSource(label)

/** Runs the completion source with the cursor at the end of `doc`. */
function complete(doc: string) {
  const state = EditorState.create({ doc, selection: { anchor: doc.length } })
  return source(new CompletionContext(state, doc.length, false))
}

describe('slashSource', () => {
  it('offers every command on a bare slash', () => {
    const result = complete('/')
    expect(result?.options.length).toBeGreaterThan(10)
    expect(result?.from).toBe(0)
  })

  it('narrows the list as the query is typed', () => {
    expect(complete('/h1')?.options.map((o) => o.label)).toEqual(['/h1'])
  })

  it('shows the translated label', () => {
    expect(complete('/table')?.options[0].displayLabel).toBe('label:table')
  })

  it('returns null when nothing matches', () => {
    expect(complete('/zzz')).toBeNull()
  })

  it('stays quiet on a slash inside a word', () => {
    expect(complete('src/lib')).toBeNull()
  })

  it('replaces the typed query with the block markdown', () => {
    const view = editor('text /h2', 8)
    const result = complete('text /h2')!
    const option = result.options[0]

    expect(typeof option.apply).toBe('function')
    ;(option.apply as (v: EditorView, c: unknown, f: number, t: number) => void)(
      view,
      option,
      result.from,
      8,
    )

    expect(view.state.doc.toString()).toBe('text ## ')
    expect(view.state.selection.main.head).toBe(8)
  })

  it('inserts a callout with the cursor on the title', () => {
    const view = editor('/callout-warning', 16)
    const result = complete('/callout-warning')!
    const option = result.options[0]
    ;(option.apply as (v: EditorView, c: unknown, f: number, t: number) => void)(
      view,
      option,
      result.from,
      16,
    )

    expect(view.state.doc.toString()).toBe('> [!warning] ')
    expect(view.state.selection.main.head).toBe(13)
  })
})

const pages: PageSummaryDto[] = [
  {
    id: 'id-notes',
    parentId: null,
    title: 'Meeting Notes',
    slug: 'meeting-notes',
    icon: null,
    orderIndex: 0,
    isFavorite: false,
    isArchived: false,
  },
]
const wikilinks = wikilinkSource(() => pages)

/** Runs the wikilink source with the cursor at `pos` (end of `doc` by default). */
function completeLink(doc: string, pos = doc.length) {
  const state = EditorState.create({ doc, selection: { anchor: pos } })
  return wikilinks(new CompletionContext(state, pos, false))
}

/** Applies an option the way CodeMirror would, from the source's `from` to the cursor. */
function applyOption(
  view: EditorView,
  result: { from: number; options: readonly { apply?: unknown }[] },
  to: number,
) {
  const option = result.options[0]
  ;(option.apply as (v: EditorView, c: unknown, f: number, t: number) => void)(
    view,
    option,
    result.from,
    to,
  )
}

describe('wikilinkSource', () => {
  it('offers every page on the bare brackets', () => {
    const result = completeLink('see [[')
    expect(result?.options.map((o) => o.label)).toEqual(['Meeting Notes'])
    expect(result?.from).toBe(6)
  })

  it('finds a page by its slug and offers the title', () => {
    expect(completeLink('[[meeting-')?.options[0].label).toBe('Meeting Notes')
  })

  it('stays quiet outside a link', () => {
    expect(completeLink('plain text')).toBeNull()
  })

  it('returns null when nothing matches', () => {
    expect(completeLink('[[zzz')).toBeNull()
  })

  it('writes the title and closes the link', () => {
    const doc = 'see [[meet'
    const view = editor(doc, doc.length)
    applyOption(view, completeLink(doc)!, doc.length)

    expect(view.state.doc.toString()).toBe('see [[Meeting Notes]]')
    expect(view.state.selection.main.head).toBe(21)
  })

  it('reuses brackets that are already closed instead of doubling them', () => {
    // What `/wikilink` leaves behind: `[[]]` with the cursor between the pairs.
    const doc = '[[]]'
    const view = editor(doc, 2)
    applyOption(view, completeLink(doc, 2)!, 2)

    expect(view.state.doc.toString()).toBe('[[Meeting Notes]]')
    expect(view.state.selection.main.head).toBe(17)
  })
})
