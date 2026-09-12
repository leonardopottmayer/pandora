import type { ReactNode } from 'react'
import { Typography, theme } from 'antd'

/**
 * A titled group of settings. The title sits above a bordered "panel"; children
 * are usually <SettingRow> items (which draw their own dividers) or a table.
 */
export function SettingsSection({
  title,
  extra,
  children,
  bodyClassName,
}: {
  title?: ReactNode
  extra?: ReactNode
  children: ReactNode
  bodyClassName?: string
}) {
  const { token } = theme.useToken()
  return (
    <section className="flex flex-col gap-2.5">
      {(title || extra) && (
        <div className="flex min-h-8 items-center justify-between px-0.5">
          {title ? (
            <Typography.Text strong type="secondary" className="text-xs uppercase tracking-wider">
              {title}
            </Typography.Text>
          ) : (
            <span />
          )}
          {extra}
        </div>
      )}
      <div
        className={`overflow-hidden rounded-xl ${bodyClassName ?? ''}`}
        style={{
          border: `1px solid ${token.colorBorderSecondary}`,
          background: token.colorBgContainer,
        }}
      >
        {children}
      </div>
    </section>
  )
}
