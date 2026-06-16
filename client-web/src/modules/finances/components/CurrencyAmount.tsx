import { Typography } from 'antd'
import { formatMoney } from '../lib/format'
import type { FlowDirection } from '../lib/enums'

interface CurrencyAmountProps {
  amount: number
  currency: string
  /** When set, colours the amount (in=green, out=red) and prefixes the sign. */
  direction?: FlowDirection
  strong?: boolean
}

const DIRECTION_COLOR: Record<FlowDirection, string | undefined> = {
  in: 'var(--ant-color-success, #52c41a)',
  out: 'var(--ant-color-error, #ff4d4f)',
  neutral: undefined,
}

/** Displays a formatted monetary amount, optionally coloured by flow direction. */
export function CurrencyAmount({ amount, currency, direction, strong }: CurrencyAmountProps) {
  const sign = direction === 'in' ? '+' : direction === 'out' ? '−' : ''
  const color = direction ? DIRECTION_COLOR[direction] : undefined
  return (
    <Typography.Text strong={strong} style={{ color, whiteSpace: 'nowrap' }}>
      {sign}
      {formatMoney(amount, currency)}
    </Typography.Text>
  )
}
