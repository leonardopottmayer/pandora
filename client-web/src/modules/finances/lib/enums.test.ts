import { describe, it, expect } from 'vitest'
import i18n from '@/i18n'
import {
  ACCOUNT_TYPE_META,
  STATEMENT_STATUS_META,
  TRANSACTION_STATUS_META,
  kindDirection,
  transactionKindLabelKey,
} from './enums'
import {
  ACCOUNT_TYPES,
  STATEMENT_STATUSES,
  TRANSACTION_KINDS,
  TRANSACTION_STATUSES,
} from '../models'

describe('enum metadata coverage', () => {
  it('has meta for every account type', () => {
    for (const type of ACCOUNT_TYPES) expect(ACCOUNT_TYPE_META[type]).toBeDefined()
  })

  it('has meta for every transaction status', () => {
    for (const s of TRANSACTION_STATUSES) expect(TRANSACTION_STATUS_META[s]).toBeDefined()
  })

  it('has meta for every statement status', () => {
    for (const s of STATEMENT_STATUSES) expect(STATEMENT_STATUS_META[s]).toBeDefined()
  })
})

describe('kindDirection', () => {
  it('classifies inflow, outflow and neutral kinds', () => {
    expect(kindDirection('income')).toBe('in')
    expect(kindDirection('expense')).toBe('out')
    expect(kindDirection('opening-balance')).toBe('neutral')
    expect(kindDirection('card-statement-payment')).toBe('out')
  })
})

describe('transactionKindLabelKey', () => {
  it('produces a key that resolves in i18n for every kind', () => {
    for (const kind of TRANSACTION_KINDS) {
      const key = transactionKindLabelKey(kind)
      expect(i18n.exists(key)).toBe(true)
    }
  })
})
