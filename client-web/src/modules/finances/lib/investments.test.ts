import { describe, expect, it } from 'vitest'
import type { TFunction } from 'i18next'
import dayjs from 'dayjs'
import type { AssetClassDto, PositionDto } from '../models'
import { assetClassLabel, isValueStale, movementKindsFor, rateLabel } from './investments'

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
    expect(movementKindsFor('application')).toEqual(['redemption', 'maturity', 'income'])
  })

  it('writes the rate of an application', () => {
    expect(rateLabel(position({ index: 'cdi', indexPercent: 110 }), t)).toBe('110% CDI')
    expect(rateLabel(position({ index: 'ipca', spreadRate: 6.5 }), t)).toBe('IPCA + 6.5%')
    expect(rateLabel(position({ index: 'pre', spreadRate: 12.5 }), t)).toBe('12.5% PRE')
    expect(rateLabel(position({}), t)).toBeNull()
  })
})
