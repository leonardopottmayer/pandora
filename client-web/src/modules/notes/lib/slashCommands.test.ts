import { describe, it, expect } from 'vitest'
import { filterCommands, SLASH_COMMANDS, SLASH_GROUPS, slashLabel } from './slashCommands'

describe('filterCommands', () => {
  const label = (command: { id: string }) => `label for ${command.id}`

  it('offers the Finance block in the data group', () => {
    expect(SLASH_COMMANDS.find((command) => command.id === 'finance-summary')).toMatchObject({ group: 'data', labelKey: 'notes.slash.finance-summary' })
    expect(filterCommands(SLASH_COMMANDS, 'finance', label).map((command) => command.id)).toContain('finance-summary')
  })

  it('offers the live Agenda block in the data group', () => {
    expect(SLASH_COMMANDS.find((command) => command.id === 'agenda-list')).toMatchObject({ group: 'data', labelKey: 'notes.slash.agenda-list' })
    expect(filterCommands(SLASH_COMMANDS, 'agenda', label).map((command) => command.id)).toContain('agenda-list')
  })

  it('lists the blocks, not the colours or "turn into", for an empty query', () => {
    const ids = filterCommands(SLASH_COMMANDS, '', label).map((c) => c.id)
    expect(ids).toContain('h1')
    expect(ids).toContain('columns3')
    expect(ids.some((id) => id.startsWith('color-') || id.startsWith('turn-'))).toBe(false)
  })

  it('keeps the categories in menu order', () => {
    const groups = filterCommands(SLASH_COMMANDS, '', label).map((c) => c.group)
    const order = groups.map((group) => SLASH_GROUPS.indexOf(group))
    expect(order).toEqual([...order].sort((a, b) => a - b))
  })

  it('matches by id, and finds colours and "turn into" once something is typed', () => {
    expect(filterCommands(SLASH_COMMANDS, 'h2', label).map((c) => c.id)).toEqual(['h2', 'toggleH2', 'turn-h2'])
    expect(filterCommands(SLASH_COMMANDS, 'background-red', label).map((c) => c.id)).toEqual(['background-red'])
  })

  it('wants every word, in the label or the category', () => {
    const named = (command: { id: string; labelValues?: Record<string, string> }) =>
      command.id === 'background-blue' ? 'Fundo azul' : command.id === 'color-blue' ? 'Texto azul' : command.id
    expect(filterCommands(SLASH_COMMANDS, 'fundo azul', named).map((c) => c.id)).toEqual(['background-blue'])
    expect(filterCommands(SLASH_COMMANDS, 'azul', named).map((c) => c.id)).toEqual(['color-blue', 'background-blue'])
  })

  it('is case-insensitive', () => {
    expect(filterCommands(SLASH_COMMANDS, 'CALLOUT-WARNING', label).map((c) => c.id)).toEqual(['callout-warning'])
  })
})

describe('slashLabel', () => {
  const t = (key: string, values?: Record<string, string>) => (values ? `${key}(${values.color}${values.kind})` : key)

  it('puts the colour or the kind into the label', () => {
    const red = SLASH_COMMANDS.find((c) => c.id === 'background-red')!
    expect(slashLabel(t, red)).toBe('notes.slash.backgroundColor(notes.colors.red)')
    const turn = SLASH_COMMANDS.find((c) => c.id === 'turn-h1')!
    expect(slashLabel(t, turn)).toBe('notes.slash.turnInto(notes.slash.h1)')
  })
})

describe('SLASH_COMMANDS', () => {
  it('asks for a size before inserting a table', () => {
    expect(SLASH_COMMANDS.find((command) => command.id === 'table')?.prompt).toBe('table')
  })

  it('has unique ids', () => {
    expect(new Set(SLASH_COMMANDS.map((command) => command.id)).size).toBe(SLASH_COMMANDS.length)
  })
})
