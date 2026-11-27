import type { TFunction } from 'i18next'
import dayjs, { type Dayjs } from 'dayjs'
import type {
  AssetBehavior,
  AssetClassDto,
  PositionDto,
  PositionMovementDto,
  PositionMovementKind,
  PositionValuationDto,
} from '../models'

/** A value older than this is shown as out of date. */
export const STALE_AFTER_DAYS = 30

/** System classes are translated by code; the user's own show their name. */
export function assetClassLabel(assetClass: AssetClassDto | undefined, t: TFunction): string {
  if (!assetClass) return ''
  return assetClass.code ? t(`finances.investments.classes.${assetClass.code}`, assetClass.name) : assetClass.name
}

/** True when the position has no reported value, or its latest one is older than STALE_AFTER_DAYS. */
export function isValueStale(position: PositionDto, today: Dayjs): boolean {
  return !position.valuedOn || today.diff(dayjs(position.valuedOn), 'day') > STALE_AFTER_DAYS
}

/** The movements a position can take after its first one. */
export function movementKindsFor(behavior: AssetBehavior): PositionMovementKind[] {
  return behavior === 'unit'
    ? ['buy', 'sell', 'income', 'split', 'come-cotas']
    : ['buy', 'redemption', 'maturity', 'income']
}

/** A movement kind's name; a buy on an application after it opened is a contribution ("Aporte"). */
export function movementKindLabel(kind: PositionMovementKind, behavior: AssetBehavior, t: TFunction): string {
  return t(`finances.investments.movementKinds.${kind === 'buy' && behavior === 'application' ? 'contribution' : kind}`)
}

/** "110% CDI", "IPCA + 6.5%", "12.5% pre": the rate of a fixed-income application. */
export function rateLabel(position: PositionDto, t: TFunction): string | null {
  if (!position.index) return null
  const index = t(`finances.investments.indexes.${position.index}`)
  const percent = position.indexPercent != null ? `${position.indexPercent}% ` : ''
  const spread = position.spreadRate != null ? ` + ${position.spreadRate}%` : ''
  if (position.index === 'pre') return position.spreadRate != null ? `${position.spreadRate}% ${index}` : index
  return `${percent}${index}${spread}`
}

/** What an open position gained over its cost: current value minus cost basis. Null when there is nothing to compare. */
export function positionGain(position: PositionDto): { amount: number; ratio: number } | null {
  if (position.status !== 'open' || !position.valuedOn || position.costBasis <= 0) return null
  const amount = position.currentValue - position.costBasis
  return { amount, ratio: amount / position.costBasis }
}

/**
 * What each reported value gained since the one before it, without the money that went in or out in between
 * (buys add, sells and redemptions take away their gross). The oldest value is compared with the cost when
 * the opening is the only movement; otherwise it has no change (null).
 */
export function valuationChanges(
  valuations: PositionValuationDto[],
  movements: PositionMovementDto[],
  costBasis: number,
): Map<string, number | null> {
  const oldestFirst = [...valuations].sort((a, b) => a.valuedOn.localeCompare(b.valuedOn))
  const changes = new Map<string, number | null>()
  oldestFirst.forEach((v, i) => {
    const prev = oldestFirst[i - 1]
    if (!prev) {
      changes.set(v.id, movements.length === 1 ? v.value - costBasis : null)
      return
    }
    const flows = movements
      .filter((m) => m.occurredOn > prev.valuedOn && m.occurredOn <= v.valuedOn)
      .reduce((sum, m) => {
        if (m.kind === 'buy') return sum + (m.grossAmount ?? 0)
        if (m.kind === 'sell' || m.kind === 'redemption') return sum - (m.grossAmount ?? 0)
        return sum
      }, 0)
    changes.set(v.id, v.value - prev.value - flows)
  })
  return changes
}
