import { describe, it, expect } from 'vitest'
import { filterCommands, SLASH_COMMANDS, slashTriggerAt } from './slashCommands'

describe('slashTriggerAt', () => {
  it('triggers at the start of a line', () => {
    expect(slashTriggerAt('/tab', 4)).toEqual({ from: 0, query: 'tab' })
  })

  it('triggers after whitespace', () => {
    expect(slashTriggerAt('text /h1', 8)).toEqual({ from: 5, query: 'h1' })
  })

  it('triggers on the bare slash, before anything is typed', () => {
    expect(slashTriggerAt('/', 1)).toEqual({ from: 0, query: '' })
  })

  it('ignores a slash inside a word, such as a path', () => {
    expect(slashTriggerAt('src/lib', 7)).toBeNull()
  })

  it('closes once a space is typed', () => {
    expect(slashTriggerAt('/table wide', 11)).toBeNull()
  })

  it('returns null with no slash before the cursor', () => {
    expect(slashTriggerAt('plain text', 10)).toBeNull()
  })

  it('ignores a slash that comes after the cursor', () => {
    expect(slashTriggerAt('ab /h1', 2)).toBeNull()
  })
})

describe('filterCommands', () => {
  const label = (command: { id: string }) => `label for ${command.id}`

  it('returns everything for an empty query', () => {
    expect(filterCommands(SLASH_COMMANDS, '', label)).toHaveLength(SLASH_COMMANDS.length)
  })

  it('matches by id', () => {
    expect(filterCommands(SLASH_COMMANDS, 'h2', label).map((c) => c.id)).toEqual(['h2'])
  })

  it('matches by translated label', () => {
    const commands = filterCommands(SLASH_COMMANDS, 'for table', label)
    expect(commands.map((c) => c.id)).toEqual(['table'])
  })

  it('is case-insensitive', () => {
    expect(filterCommands(SLASH_COMMANDS, 'CALLOUT-TIP', label).map((c) => c.id)).toEqual([
      'callout-tip',
    ])
  })
})

describe('SLASH_COMMANDS', () => {
  it('places the cursor inside the inserted text', () => {
    for (const command of SLASH_COMMANDS) {
      if (command.kind !== 'insert') continue
      expect(command.cursor).toBeGreaterThanOrEqual(0)
      expect(command.cursor).toBeLessThanOrEqual(command.text.length)
    }
  })

  it('asks for a size before inserting a table', () => {
    const table = SLASH_COMMANDS.find((command) => command.id === 'table')
    expect(table).toMatchObject({ kind: 'prompt', prompt: 'table' })
  })

  it('has unique ids', () => {
    const ids = SLASH_COMMANDS.map((c) => c.id)
    expect(new Set(ids).size).toBe(ids.length)
  })
})
