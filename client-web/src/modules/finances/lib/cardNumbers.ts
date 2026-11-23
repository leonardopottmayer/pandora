import type { TFunction } from 'i18next'
import type { CardDto, CardNumberDto } from '../models'

/** "Uber · Virtual ····1234": the nickname when there is one, the kind and the last four. */
export function cardNumberLabel(number: CardNumberDto, t: TFunction): string {
  const kind = t(`finances.cards.numberKinds.${number.kind}`)
  return [number.nickname, `${kind} ····${number.lastFour}`].filter(Boolean).join(' · ')
}

/**
 * The numbers a transaction on the card can say it used: the active ones, plus `keepId` when it is
 * archived, so editing an old transaction keeps its value.
 */
export function selectableCardNumbers(card: CardDto | undefined, keepId?: string | null): CardNumberDto[] {
  return (card?.numbers ?? []).filter((number) => !number.archivedAt || number.id === keepId)
}
