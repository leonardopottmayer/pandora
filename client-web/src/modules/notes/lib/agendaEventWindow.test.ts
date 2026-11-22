import { describe, expect, it } from 'vitest'
import { agendaEventWindow } from './agendaEventWindow'

describe('agendaEventWindow', () => {
  it('uses the account day even when UTC has entered tomorrow', () => {
    expect(agendaEventWindow(7, 'America/Sao_Paulo', new Date('2026-12-01T01:30:00Z'))).toEqual({
      from: '2026-11-30T03:00:00.000Z', to: '2026-12-07T03:00:00.000Z',
    })
  })

  it.each([
    ['2026-03-07T15:00:00Z', '2026-03-07T05:00:00.000Z', '2026-03-14T04:00:00.000Z'],
    ['2026-10-31T15:00:00Z', '2026-10-31T04:00:00.000Z', '2026-11-07T05:00:00.000Z'],
  ])('resolves each midnight independently across DST: %s', (now, from, to) => {
    expect(agendaEventWindow(7, 'America/New_York', new Date(now))).toEqual({ from, to })
  })

  it('crosses the year boundary using thirty calendar days', () => {
    expect(agendaEventWindow(30, 'Pacific/Auckland', new Date('2026-12-31T15:00:00Z'))).toEqual({
      from: '2026-12-31T11:00:00.000Z', to: '2027-01-30T11:00:00.000Z',
    })
  })
})
