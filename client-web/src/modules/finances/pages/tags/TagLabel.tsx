import { theme } from 'antd'

/** Keep the name readable while showing the user-selected color as a bordered swatch. */
export function TagLabel({ name, color }: { name: string; color?: string | null }) {
  const { token } = theme.useToken()
  return (
    <span className="inline-flex max-w-full min-w-0 items-center gap-2 align-middle">
      <span aria-hidden className="size-3 shrink-0 rounded-full" style={{ background: color ?? token.colorFillSecondary, border: `1px solid ${token.colorBorder}` }} />
      <span className="truncate" title={name}>{name}</span>
    </span>
  )
}
