import type { EditorView } from '@codemirror/view'
import type { CompletionContext, CompletionResult } from '@codemirror/autocomplete'
import { cellAt, cellOffset, findTableAt, formatTable, nextCell, withEmptyRow } from './markdownTables'
import { filterCommands, SLASH_COMMANDS, slashTriggerAt, type SlashCommand } from './slashCommands'
import { filterPages, wikilinkTriggerAt } from './wikilinks'
import type { PageSummaryDto } from '../models'

// The CodeMirror side of the rich blocks: what `/` and Tab do to the document.

/**
 * Tab (Shift+Tab backwards) inside a pipe table: realign the whole table and land on the next cell,
 * appending a row when it runs off the end. Outside a table the command declines, leaving Tab with
 * whatever meaning the rest of the keymap gives it.
 */
export function moveTableCell(direction: 1 | -1) {
  return (view: EditorView): boolean => {
    const { state } = view
    const range = state.selection.main
    if (!range.empty) return false

    const cursorLine = state.doc.lineAt(range.head)
    const lines = state.doc.toString().split('\n')
    const table = findTableAt(lines, cursorLine.number - 1)
    // A single pipe line isn't a table yet — Tab there would reformat a line the user is still
    // writing.
    if (!table || table.rows.length < 2) return false

    const current = {
      line: cursorLine.number - 1,
      column: cellAt(cursorLine.text, range.head - cursorLine.from),
    }

    let target = nextCell(table, current, direction)
    let block = table
    if (!target) {
      // Backwards off the first cell just leaves the table alone.
      if (direction === -1) return false
      block = withEmptyRow(table)
      target = { line: block.endLine, column: 0 }
    }

    const formatted = formatTable(block)
    const targetRow = target.line - block.startLine
    const offsetInBlock = formatted
      .slice(0, targetRow)
      .reduce((total, line) => total + line.length + 1, 0)

    const from = state.doc.line(table.startLine + 1).from
    const to = state.doc.line(table.endLine + 1).to

    view.dispatch({
      changes: { from, to, insert: formatted.join('\n') },
      selection: { anchor: from + offsetInBlock + cellOffset(formatted[targetRow], target.column) },
      scrollIntoView: true,
    })
    return true
  }
}

/** Replaces the typed `/query` with the command's markdown and puts the cursor where it belongs. */
function applyCommand(view: EditorView, command: SlashCommand, from: number, to: number) {
  view.dispatch({
    changes: { from, to, insert: command.text },
    selection: { anchor: from + command.cursor },
    scrollIntoView: true,
  })
}

/**
 * The `/` menu. `label` translates a command for display, so the list also filters in the language
 * on screen.
 */
export function slashSource(label: (command: SlashCommand) => string) {
  return (context: CompletionContext): CompletionResult | null => {
    const line = context.state.doc.lineAt(context.pos)
    const trigger = slashTriggerAt(line.text, context.pos - line.from)
    if (!trigger) return null

    const commands = filterCommands(SLASH_COMMANDS, trigger.query, label)
    if (commands.length === 0) return null

    return {
      from: line.from + trigger.from,
      // `filterCommands` already decided what matches; CodeMirror must not filter again, or the
      // options that matched on their translated label would be dropped.
      filter: false,
      options: commands.map((command) => ({
        label: `/${command.id}`,
        displayLabel: label(command),
        apply: (view: EditorView, _completion: unknown, from: number, to: number) =>
          applyCommand(view, command, from, to),
      })),
    }
  }
}

/** Writes the chosen title and closes the link, reusing a `]]` that is already sitting there. */
function applyWikilink(view: EditorView, title: string, from: number, to: number) {
  const closed = view.state.doc.sliceString(to, to + 2) === ']]'
  const insert = `${title}]]`
  view.dispatch({
    changes: { from, to: closed ? to + 2 : to, insert },
    selection: { anchor: from + insert.length },
    scrollIntoView: true,
  })
}

/**
 * The `[[` menu. `pages` is read at completion time (a function, not an array) so the list stays
 * current without rebuilding the editor.
 */
export function wikilinkSource(pages: () => PageSummaryDto[]) {
  return (context: CompletionContext): CompletionResult | null => {
    const line = context.state.doc.lineAt(context.pos)
    const trigger = wikilinkTriggerAt(line.text, context.pos - line.from)
    if (!trigger) return null

    const matches = filterPages(pages(), trigger.query)
    if (matches.length === 0) return null

    return {
      from: line.from + trigger.from,
      // Same reason as the slash menu: `filterPages` already decided, and the titles it matched by
      // slug would not survive CodeMirror's own filter.
      filter: false,
      options: matches.map((page) => ({
        label: page.title,
        detail: page.slug,
        apply: (view: EditorView, _completion: unknown, from: number, to: number) =>
          applyWikilink(view, page.title, from, to),
      })),
    }
  }
}
