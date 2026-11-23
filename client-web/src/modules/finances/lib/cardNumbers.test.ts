import { describe, expect, it } from 'vitest'
import type { TFunction } from 'i18next'
import type { CardDto, CardNumberDto } from '../models'
import { cardNumberLabel, selectableCardNumbers } from './cardNumbers'

const t = ((key: string) => key.split('.').pop()) as unknown as TFunction

const number = (id: string, archivedAt: string | null = null, nickname: string | null = null): CardNumberDto =>
  ({ id, cardId: 'c1', kind: 'virtual', lastFour: '1234', nickname, archivedAt })

describe('cardNumbers', () => {
  it('labels a number by nickname, kind and last four', () => {
    expect(cardNumberLabel(number('n1', null, 'Uber'), t)).toBe('Uber · virtual ····1234')
    expect(cardNumberLabel(number('n1'), t)).toBe('virtual ····1234')
  })

  it('offers the active numbers, plus the archived one already chosen', () => {
    const card = { numbers: [number('active'), number('old', '2026-11-01T00:00:00Z'), number('gone', '2026-11-01T00:00:00Z')] } as CardDto

    expect(selectableCardNumbers(card).map((n) => n.id)).toEqual(['active'])
    expect(selectableCardNumbers(card, 'old').map((n) => n.id)).toEqual(['active', 'old'])
    expect(selectableCardNumbers(undefined)).toEqual([])
  })
})
