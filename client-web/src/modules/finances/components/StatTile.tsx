import type { ReactNode } from 'react'
import { Typography, theme } from 'antd'

/** One figure in a bordered box: a small label above, the value large, an optional note under it. */
export function StatTile({ label, value, note }: { label: ReactNode; value: ReactNode; note?: ReactNode }) {
  const { token } = theme.useToken()
  return (
    <div
      className="flex min-w-48 flex-col gap-0.5 rounded-xl px-4 py-3"
      style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
    >
      <Typography.Text type="secondary" className="text-xs uppercase tracking-wider">
        {label}
      </Typography.Text>
      <span className="text-2xl font-semibold">{value}</span>
      {note && (
        <Typography.Text type="secondary" className="text-xs tabular-nums">
          {note}
        </Typography.Text>
      )}
    </div>
  )
}
