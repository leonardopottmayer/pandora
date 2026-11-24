import { useTranslation } from 'react-i18next'
import { Typography, theme } from 'antd'
import type { Dayjs } from 'dayjs'
import type { AssetClassDto, PositionDto } from '../models'
import { formatDate, formatMoney } from '../lib/format'
import { assetClassLabel, isValueStale, rateLabel } from '../lib/investments'

interface PositionRowProps {
  position: PositionDto
  assetClass: AssetClassDto | undefined
  today: Dayjs
  first: boolean
  onOpen: (id: string) => void
}

/** One position: asset and what is held on the left, current value and its date on the right. */
export function PositionRow({ position: p, assetClass, today, first, onOpen }: PositionRowProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const stale = isValueStale(p, today)

  const held = p.behavior === 'unit'
    ? t('finances.investments.heldUnits', {
        quantity: p.quantity?.toLocaleString(),
        cost: formatMoney(p.averageCost ?? 0, p.currency),
      })
    : t('finances.investments.heldInvested', { amount: formatMoney(p.investedAmount ?? 0, p.currency) })
  const details = [
    assetClassLabel(assetClass, t),
    held,
    rateLabel(p, t),
    p.maturityOn && t('finances.investments.maturesOn', { date: formatDate(p.maturityOn) }),
  ].filter(Boolean)

  return (
    <button
      type="button"
      onClick={() => onOpen(p.id)}
      className="flex w-full cursor-pointer items-center gap-3 border-0 bg-transparent px-4 py-2.5 text-left"
      style={{ borderTop: first ? undefined : `1px solid ${token.colorBorderSecondary}`, color: token.colorText }}
    >
      <span className="flex min-w-0 flex-1 flex-col">
        <Typography.Text strong className="truncate">
          {p.assetCode ? `${p.assetCode} · ${p.assetName}` : p.assetName}
        </Typography.Text>
        <Typography.Text type="secondary" className="truncate text-xs">
          {details.join(' · ')}
        </Typography.Text>
      </span>
      <span className="flex shrink-0 flex-col items-end">
        <span className="tabular-nums">{formatMoney(p.currentValue, p.currency)}</span>
        <span className="text-xs" style={{ color: stale ? token.colorWarning : token.colorTextTertiary }}>
          {p.valuedOn
            ? t(stale ? 'finances.investments.valueStale' : 'finances.investments.valueOn', { date: formatDate(p.valuedOn) })
            : t('finances.investments.noValue')}
        </span>
      </span>
    </button>
  )
}
