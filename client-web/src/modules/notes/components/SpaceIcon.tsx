import { theme } from 'antd'
import type { SpaceDto } from '../models'

/** A space's emoji on a faint fill of its color; with no emoji, the first letter of its name. */
export function SpaceIcon({ space, size = 32 }: { space: Pick<SpaceDto, 'name' | 'icon' | 'color'>; size?: number }) {
  const { token } = theme.useToken()
  const color = space.color ?? token.colorPrimary
  return (
    <span
      aria-hidden
      className="inline-flex shrink-0 items-center justify-center rounded-lg font-semibold"
      style={{
        width: size,
        height: size,
        fontSize: size * 0.5,
        // The fill is faint so the text color stays the theme's, readable on any space color.
        background: `${color.slice(0, 7)}29`,
        color: token.colorText,
      }}
    >
      {space.icon ?? space.name.trim().charAt(0).toUpperCase()}
    </span>
  )
}
