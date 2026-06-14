import { Typography } from 'antd'
import { formatMoney } from '../lib/format'
import type { FlowDirection } from '../lib/enums'

interface CurrencyAmountProps {
  amount: number
  currency: string
  /** Quando informado, colore o valor (entrada=verde, saída=vermelho) e prefixa o sinal. */
  direction?: FlowDirection
  strong?: boolean
}

const DIRECTION_COLOR: Record<FlowDirection, string | undefined> = {
  in: 'var(--ant-color-success, #52c41a)',
  out: 'var(--ant-color-error, #ff4d4f)',
  neutral: undefined,
}

/** Exibe um valor monetário formatado, opcionalmente colorido pela direção do fluxo. */
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
