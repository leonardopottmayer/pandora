import type { ReactNode } from 'react'
import { Typography, theme } from 'antd'

/**
 * A single setting: label + description on the left, the control on the right.
 * Stacks vertically on narrow screens and splits into two columns from `sm` up.
 * Pass `vertical` to keep the control under the label at every width — use it when
 * the control is wide and would otherwise crush the label in a narrow container.
 * Draws a bottom divider; the last row in a section drops it (last:border-b-0).
 */
export function SettingRow({
  label,
  description,
  control,
  align = 'center',
  vertical = false,
}: {
  label: ReactNode
  description?: ReactNode
  control: ReactNode
  align?: 'center' | 'start'
  vertical?: boolean
}) {
  const { token } = theme.useToken()
  const splitClasses = vertical
    ? ''
    : `sm:flex-row sm:justify-between sm:gap-7 ${align === 'start' ? 'sm:items-start' : 'sm:items-center'}`
  return (
    <div
      className={`flex flex-col gap-3 border-b border-solid px-4 py-4 last:border-b-0 ${splitClasses}`}
      style={{ borderColor: token.colorBorderSecondary }}
    >
      <div className="flex min-w-0 flex-col gap-0.5">
        <Typography.Text strong className="text-sm">
          {label}
        </Typography.Text>
        {description && (
          <Typography.Text type="secondary" className="text-xs">
            {description}
          </Typography.Text>
        )}
      </div>
      <div className={vertical ? '' : 'shrink-0'}>{control}</div>
    </div>
  )
}
