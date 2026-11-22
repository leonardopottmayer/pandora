import { CALLOUT_TYPES } from './callouts'
import { BLOCK_COLORS, TURN_INTO } from './blockStyle'

// The `/` menu: each command is a block the editor inserts, a dialog it opens first, or a change to
// the block the slash was typed in (a colour, another kind).

export const SLASH_GROUPS = ['basic', 'media', 'advanced', 'data', 'inline', 'embed', 'color', 'turnInto'] as const
export type SlashGroup = (typeof SLASH_GROUPS)[number]

export interface SlashCommand {
  id: string
  /** i18n key of the label shown in the menu. */
  labelKey: string
  /** Values for the label's placeholders (the colour in "Red background"). */
  labelValues?: Record<string, string>
  group: SlashGroup
  /** `image` and `file` open the media library, `page` the page picker, `table` its size. */
  prompt?: 'table' | 'image' | 'file' | 'page'
  /** Listed only once something is typed: colours and "turn into" would bury the blocks. */
  searchOnly?: boolean
}

const command = (group: SlashGroup, id: string, prompt?: SlashCommand['prompt']): SlashCommand => ({ id, labelKey: `notes.slash.${id}`, group, prompt })

export const SLASH_COMMANDS: SlashCommand[] = [
  command('basic', 'paragraph'),
  command('basic', 'h1'),
  command('basic', 'h2'),
  command('basic', 'h3'),
  command('basic', 'bulletList'),
  command('basic', 'numberedList'),
  command('basic', 'todo'),
  command('basic', 'toggle'),
  command('basic', 'toggleH1'),
  command('basic', 'toggleH2'),
  command('basic', 'toggleH3'),
  command('basic', 'quote'),
  command('basic', 'divider'),
  ...CALLOUT_TYPES.map((type): SlashCommand => ({ id: `callout-${type}`, labelKey: `notes.callout.${type}`, group: 'basic' })),
  command('media', 'image', 'image'),
  command('media', 'gallery'),
  command('media', 'video'),
  command('media', 'audio'),
  command('media', 'file', 'file'),
  command('media', 'pdf'),
  command('advanced', 'codeBlock'),
  command('advanced', 'aiContinue'),
  command('advanced', 'ai'),
  command('advanced', 'mermaid'),
  command('advanced', 'math'),
  command('advanced', 'synced'),
  command('advanced', 'templateButton'),
  command('advanced', 'decision'),
  command('advanced', 'breadcrumb'),
  command('advanced', 'subpages'),
  command('advanced', 'tag-pages'),
  command('advanced', 'properties'),
  command('advanced', 'columns2'),
  command('advanced', 'columns3'),
  command('advanced', 'columns4'),
  command('data', 'table', 'table'),
  command('data', 'agenda-list'),
  command('data', 'finance-summary'),
  command('inline', 'wikilink', 'page'),
  command('inline', 'date'),
  command('inline', 'status'),
  command('inline', 'inlineMath'),
  command('embed', 'pageCard', 'page'),
  command('embed', 'pagePreview', 'page'),
  command('embed', 'bookmark'),
  command('embed', 'embed'),
  ...['default', ...BLOCK_COLORS].flatMap((color): SlashCommand[] => [
    { id: `color-${color}`, labelKey: 'notes.slash.textColor', labelValues: { color }, group: 'color', searchOnly: true },
    { id: `background-${color}`, labelKey: 'notes.slash.backgroundColor', labelValues: { color }, group: 'color', searchOnly: true },
  ]),
  ...TURN_INTO.map((kind): SlashCommand => ({ id: `turn-${kind}`, labelKey: 'notes.slash.turnInto', labelValues: { kind }, group: 'turnInto', searchOnly: true })),
]

/**
 * Commands whose (already translated) label, group or id matches what was typed after the slash,
 * in menu order: by group, then as listed.
 */
export function filterCommands(
  commands: SlashCommand[],
  query: string,
  label: (command: SlashCommand) => string,
  groupLabel: (group: SlashGroup) => string = (group) => group,
): SlashCommand[] {
  const term = query.trim().toLowerCase()
  const found = term.length === 0
    ? commands.filter((entry) => !entry.searchOnly)
    : commands.filter((entry) =>
        // Each word somewhere: "fundo azul" finds "Fundo azul", "turn h1" finds "Turn into Heading 1".
        term.split(/\s+/).every((word) =>
          [entry.id, label(entry), groupLabel(entry.group)].some((text) => text.toLowerCase().includes(word)),
        ),
      )
  return SLASH_GROUPS.flatMap((group) => found.filter((entry) => entry.group === group))
}

/** A command's label, its colour or kind put in ("Red background", "Turn into Heading 1"). */
export function slashLabel(t: (key: string, values?: Record<string, string>) => string, entry: SlashCommand): string {
  const values = entry.labelValues
  if (!values) return t(entry.labelKey)
  return t(entry.labelKey, {
    color: values.color ? t(`notes.colors.${values.color}`) : '',
    kind: values.kind ? t(`notes.slash.${values.kind}`) : '',
  })
}
