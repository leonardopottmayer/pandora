import { CALLOUT_TYPES } from './callouts'

// The `/` menu inserts plain markdown — every block below is something the user could have typed by
// hand, which is what keeps the document round-trippable.

export interface SlashCommand {
  id: string
  /** i18n key of the label shown in the menu. */
  labelKey: string
  group: 'block' | 'callout'
  /** Markdown that replaces the typed `/query`. */
  text: string
  /** Where the cursor lands afterwards, as an offset into `text`. */
  cursor: number
}

/** `| ` + three spaces + ` |`, the same padding `formatTable` produces. */
const TABLE_SNIPPET = ['|     |     |', '| --- | --- |', '|     |     |', ''].join('\n')

function block(id: string, text: string, cursor = text.length): SlashCommand {
  return { id, labelKey: `notes.slash.${id}`, group: 'block', text, cursor }
}

function callout(type: string): SlashCommand {
  return {
    id: `callout-${type}`,
    labelKey: `notes.callout.${type}`,
    group: 'callout',
    text: `> [!${type}] `,
    cursor: `> [!${type}] `.length,
  }
}

export const SLASH_COMMANDS: SlashCommand[] = [
  block('h1', '# '),
  block('h2', '## '),
  block('h3', '### '),
  block('bulletList', '- '),
  block('numberedList', '1. '),
  block('todo', '- [ ] '),
  block('quote', '> '),
  block('codeBlock', '```\n\n```\n', 4),
  block('divider', '---\n\n'),
  block('table', TABLE_SNIPPET, 2),
  block('wikilink', '[[]]', 2),
  ...CALLOUT_TYPES.map((type) => callout(type)),
]

export interface SlashTrigger {
  /** Offset in the line where the `/` sits — the replacement starts here. */
  from: number
  /** What was typed after the slash, used to filter the menu. */
  query: string
}

/**
 * The `/query` being typed at `ch`, or `null` when the cursor isn't in one. A slash only opens the
 * menu at the start of a line or after whitespace, so a path like `src/lib` never triggers it.
 */
export function slashTriggerAt(line: string, ch: number): SlashTrigger | null {
  const before = line.slice(0, ch)
  const slash = before.lastIndexOf('/')
  if (slash === -1) return null

  const preceding = before[slash - 1]
  if (preceding !== undefined && !/\s/.test(preceding)) return null

  const query = before.slice(slash + 1)
  // A space closes the menu: the user moved on to writing prose.
  if (/\s/.test(query)) return null

  return { from: slash, query }
}

/** Commands whose (already translated) label or id matches what was typed after the slash. */
export function filterCommands(
  commands: SlashCommand[],
  query: string,
  label: (command: SlashCommand) => string,
): SlashCommand[] {
  const term = query.trim().toLowerCase()
  if (term.length === 0) return commands
  return commands.filter(
    (command) =>
      command.id.toLowerCase().includes(term) || label(command).toLowerCase().includes(term),
  )
}
