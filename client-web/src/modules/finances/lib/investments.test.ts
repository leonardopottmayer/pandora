import { describe, expect, it } from 'vitest'
import type { TFunction } from 'i18next'
import dayjs from 'dayjs'
import type { AssetClassDto, PositionDto, PositionMovementDto, PositionValuationDto } from '../models'
import { assetClassLabel, isValueStale, movementKindLabel, movementKindsFor, positionGain, rateLabel, valuationChanges } from './investments'

const t = ((key: string, fallback?: string) => (key.startsWith('finances.investments.indexes.') ? key.split('.').pop()!.toUpperCase() : fallback ?? key)) as unknown as TFunction

const position = (overrides: Partial<PositionDto>): PositionDto => ({
  id: 'p1', accountId: 'a1', assetId: 's1', assetName: 'CDB', assetCode: null, assetClassId: 'c1', behavior: 'application',
  status: 'open', currency: 'BRL', quantity: null, averageCost: null, investedAmount: 1000, appliedOn: '2026-01-01',
  index: null, indexPercent: null, spreadRate: null, maturityOn: null, costBasis: 1000, currentValue: 1000, valuedOn: null,
  closedAt: null, ...overrides,
})

describe('investments', () => {
  it('labels system classes by code and own classes by name', () => {
    const system = { code: 'cdb', name: 'CDB' } as AssetClassDto
    const own = { code: null, name: 'Crowdfunding' } as AssetClassDto
    expect(assetClassLabel(own, t)).toBe('Crowdfunding')
    expect(assetClassLabel(system, t)).toBe('CDB')
    expect(assetClassLabel(undefined, t)).toBe('')
  })

  it('marks a value stale when missing or older than 30 days', () => {
    const today = dayjs('2026-11-23')
    expect(isValueStale(position({ valuedOn: null }), today)).toBe(true)
    expect(isValueStale(position({ valuedOn: '2026-10-24' }), today)).toBe(false)
    expect(isValueStale(position({ valuedOn: '2026-10-23' }), today)).toBe(true)
  })

  it('offers the movements of each behavior', () => {
    expect(movementKindsFor('unit')).toContain('sell')
    expect(movementKindsFor('application')).toEqual(['buy', 'redemption', 'maturity', 'income'])
  })

  it('names a buy on an application a contribution', () => {
    expect(movementKindLabel('buy', 'application', t)).toBe('finances.investments.movementKinds.contribution')
    expect(movementKindLabel('buy', 'unit', t)).toBe('finances.investments.movementKinds.buy')
  })

  it('writes the rate of an application', () => {
    expect(rateLabel(position({ index: 'cdi', indexPercent: 110 }), t)).toBe('110% CDI')
    expect(rateLabel(position({ index: 'ipca', spreadRate: 6.5 }), t)).toBe('IPCA + 6.5%')
    expect(rateLabel(position({ index: 'pre', spreadRate: 12.5 }), t)).toBe('12.5% PRE')
    expect(rateLabel(position({}), t)).toBeNull()
  })

  it('gives the gain over cost only for an open position with a reported value', () => {
    expect(positionGain(position({ valuedOn: '2026-11-20', currentValue: 1050 }))).toEqual({ amount: 50, ratio: 0.05 })
    expect(positionGain(position({ valuedOn: null }))).toBeNull()
    expect(positionGain(position({ valuedOn: '2026-11-20', status: 'closed' }))).toBeNull()
  })

  it('changes between reported values leave out the money that went in or out', () => {
    const valuation = (id: string, valuedOn: string, value: number) => ({ id, positionId: 'p1', valuedOn, value, source: 'manual' }) as PositionValuationDto
    const movement = (kind: PositionMovementDto['kind'], occurredOn: string, grossAmount: number | null) =>
      ({ id: `${kind}-${occurredOn}`, kind, occurredOn, grossAmount }) as PositionMovementDto
    const valuations = [valuation('v3', '2026-11-15', 1580), valuation('v2', '2026-11-08', 1020), valuation('v1', '2026-11-01', 1010)]

    // Opening only: the oldest value is compared with the cost.
    expect(valuationChanges(valuations.slice(1), [movement('opening', '2026-10-25', null)], 1000))
      .toEqual(new Map([['v1', 10], ['v2', 10]]))

    // A 500 buy and a 100 redemption between v2 and v3: 1580 − 1020 − 500 + 100 = 160.
    const changes = valuationChanges(valuations, [
      movement('opening', '2026-10-25', null),
      movement('buy', '2026-11-10', 500),
      movement('redemption', '2026-11-12', 100),
    ], 1400)
    expect(changes.get('v1')).toBeNull()
    expect(changes.get('v3')).toBe(160)
  })
})
