import { describe, expect, it } from 'vitest'
import type { ImportRowDto } from '../../models'
import { formatImportPayload, importRowPreview } from './importRowPreview'

const row = (parsedPayload: string | null) => ({ rawData: 'Original bank row', parsedPayload }) as ImportRowDto

describe('import row preview', () => {
  it('reads the parser snapshot without losing zero amounts or expense direction', () => {
    expect(importRowPreview(row(JSON.stringify({ Description: 'Groceries', OccurredOn: '2026-06-12', Amount: 0, Currency: 'BRL', IsCredit: false })))).toEqual({
      description: 'Groceries', occurredOn: '2026-06-12', amount: 0, currency: 'BRL', direction: 'out',
    })
  })

  it('retains original rows when snapshots are missing, invalid or not objects', () => {
    for (const payload of [null, 'not JSON', 'null', '[]', '123']) {
      expect(importRowPreview(row(payload))).toEqual({ description: 'Original bank row' })
    }
    expect(formatImportPayload('not JSON')).toBe('not JSON')
  })

  it('does not infer a direction or amount absent from the parsed data', () => {
    const preview = importRowPreview(row('{"Description":"Refund"}'))
    expect(preview.description).toBe('Refund')
    expect(preview.amount).toBeUndefined()
    expect(preview.direction).toBeUndefined()
    expect(formatImportPayload('{"Description":"Refund"}')).toBe('{\n  "Description": "Refund"\n}')
  })
})
