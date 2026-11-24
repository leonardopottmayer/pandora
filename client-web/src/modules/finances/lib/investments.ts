import type { TFunction } from 'i18next'
import dayjs, { type Dayjs } from 'dayjs'
import type { AssetBehavior, AssetClassDto, PositionDto, PositionMovementKind } from '../models'

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
    : ['redemption', 'maturity', 'income']
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
