import { createPortal } from 'react-dom'
import { theme } from 'antd'
import type { MenuState } from './suggestions'

interface SuggestionMenuProps {
  menu: MenuState
  active: number
  onHover: (index: number) => void
}

/** The list a `/`, `[[` or `#` opens, under the cursor; the keyboard drives it from the editor. */
export function SuggestionMenu({ menu, active, onHover }: SuggestionMenuProps) {
  const { token } = theme.useToken()
  if (!menu.rect || menu.entries.length === 0) return null

  // Below the cursor, unless that runs off the screen: then above it.
  const below = menu.rect.bottom + 320 < window.innerHeight
  return createPortal(
    <div
      role="listbox"
      className="notes-suggestion-menu"
      style={{
        left: Math.min(menu.rect.left, window.innerWidth - 280),
        ...(below ? { top: menu.rect.bottom + 6 } : { bottom: window.innerHeight - menu.rect.top + 6 }),
        background: token.colorBgElevated,
        boxShadow: token.boxShadowSecondary,
        borderRadius: token.borderRadiusLG,
      }}
    >
      {menu.entries.map((entry, index) => (
        <div
          key={entry.key}
          role="option"
          aria-selected={index === active}
          className="notes-suggestion-item"
          style={index === active ? { background: token.controlItemBgActive } : undefined}
          onMouseEnter={() => onHover(index)}
          // Down, not click: the editor must keep its focus and selection.
          onMouseDown={(event) => {
            event.preventDefault()
            menu.pick(index)
          }}
        >
          <span className="notes-suggestion-label">
            {entry.icon && <span className="notes-suggestion-icon">{entry.icon}</span>}
            {entry.label}
          </span>
          {entry.hint && <span className="notes-suggestion-hint">{entry.hint}</span>}
        </div>
      ))}
    </div>,
    document.body,
  )
}
