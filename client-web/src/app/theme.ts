import { theme as antdTheme, type ThemeConfig } from 'antd'
import type { AppPalette } from '@/modules/identity/models'

/** See DESIGN.md. A screen takes its colors from these tokens, never from a hex of its own. */
export interface Surfaces {
  layout: string
  container: string
  elevated: string
  border: string
  divider: string
  text: string
  textSecondary: string
  textTertiary: string
  menuItem: string
  tableHeader: string
  rowHover: string
}

interface Palette {
  accent: string
  borderRadius: number
  dark: Surfaces
  light: Surfaces
}

export const PALETTES: Record<AppPalette, Palette> = {
  // Ant Design's own colors, spelled out because the accent is worked out against them.
  default: {
    accent: '#1677ff',
    borderRadius: 6,
    dark: {
      layout: '#000000',
      container: '#141414',
      elevated: '#1f1f1f',
      border: '#424242',
      divider: '#303030',
      text: '#d9d9d9',
      textSecondary: '#a6a6a6',
      textTertiary: '#737373',
      menuItem: '#d9d9d9',
      tableHeader: '#1d1d1d',
      rowHover: '#262626',
    },
    light: {
      layout: '#f5f5f5',
      container: '#ffffff',
      elevated: '#ffffff',
      border: '#d9d9d9',
      divider: '#f0f0f0',
      text: '#1f1f1f',
      textSecondary: '#595959',
      textTertiary: '#8c8c8c',
      menuItem: '#1f1f1f',
      tableHeader: '#fafafa',
      rowHover: '#fafafa',
    },
  },
  claude: {
    accent: '#c96442',
    borderRadius: 8,
    dark: {
      layout: '#1f1e1d',
      container: '#262624',
      elevated: '#30302e',
      border: '#4a4945',
      divider: '#3a3936',
      text: '#faf9f5',
      textSecondary: '#a8a69c',
      textTertiary: '#8f8d84',
      menuItem: '#c2c0b6',
      tableHeader: '#2c2c2a',
      rowHover: '#2c2c2a',
    },
    light: {
      layout: '#f5f4ef',
      container: '#faf9f5',
      elevated: '#ffffff',
      border: '#d6d4ca',
      divider: '#e8e6dc',
      text: '#1f1e1d',
      textSecondary: '#5e5d59',
      textTertiary: '#73726c',
      menuItem: '#3d3d3a',
      tableHeader: '#f0eee6',
      rowHover: '#f0eee6',
    },
  },
  reddit: {
    accent: '#d93900',
    borderRadius: 8,
    dark: {
      layout: '#0e1113',
      container: '#181c1f',
      elevated: '#1f2529',
      border: '#3e4c52',
      divider: '#2a3236',
      text: '#eef1f3',
      textSecondary: '#b7cad4',
      textTertiary: '#82959b',
      menuItem: '#b7cad4',
      tableHeader: '#1f2529',
      rowHover: '#1f2529',
    },
    light: {
      layout: '#f6f8f9',
      container: '#ffffff',
      elevated: '#ffffff',
      border: '#ccd6db',
      divider: '#e5ebee',
      text: '#181c1f',
      textSecondary: '#576f76',
      textTertiary: '#667f86',
      menuItem: '#2a3c42',
      tableHeader: '#eef2f4',
      rowHover: '#f6f8f9',
    },
  },
}

/** The ready-made accents. A hover is only here when a color has one of its own; otherwise antd works it out. */
export const ACCENT_PRESETS = [
  { key: 'blue', color: '#1677ff' },
  { key: 'green', color: '#2dcd88', hover: '#2dbc88' },
] as const

/** Text on an accent white cannot be read on: a yellow, or the green above. */
const ON_LIGHT_ACCENT = '#181b3d'
const WHITE = '#ffffff'
const BLACK = '#000000'

/** At 3:1, WCAG's bar for controls, so that antd's own blue (4.1:1 with white) keeps its white text. */
const ON_ACCENT_CONTRAST = 3
const TEXT_CONTRAST = 4.5

function channels(hex: string): number[] {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16))
}

function luminance(hex: string): number {
  const [r, g, b] = channels(hex)
    .map((c) => c / 255)
    .map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

export function contrast(a: string, b: string): number {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (high + 0.05) / (low + 0.05)
}

/** `amount` of `color`, the rest of `base`. */
function mix(color: string, base: string, amount: number): string {
  const [from, to] = [channels(color), channels(base)]
  const parts = from.map((c, i) => Math.round(c * amount + to[i] * (1 - amount)).toString(16).padStart(2, '0'))
  return `#${parts.join('')}`
}

/** What goes on top of an accent: buttons, a checkbox's tick, the logo. */
export function onAccent(accent: string): string {
  return contrast(WHITE, accent) >= ON_ACCENT_CONTRAST ? WHITE : ON_LIGHT_ACCENT
}

/** The accent as text: moved toward white or black until it reads on all of `surfaces`. */
function readableAccent(accent: string, surfaces: string[], isDark: boolean): string {
  const target = isDark ? WHITE : BLACK
  for (let amount = 1; amount > 0; amount -= 0.05) {
    const candidate = mix(accent, target, amount)
    if (surfaces.every((surface) => contrast(candidate, surface) >= TEXT_CONTRAST)) return candidate
  }
  return target
}

export function appTheme({
  isDark,
  palette,
  accentColor,
}: {
  isDark: boolean
  palette: AppPalette
  accentColor: string | null
}): ThemeConfig {
  const { accent: paletteAccent, borderRadius, ...modes } = PALETTES[palette]
  const surfaces = isDark ? modes.dark : modes.light
  const accent = accentColor ?? paletteAccent
  const preset = ACCENT_PRESETS.find((p) => p.color === accent)

  const selectedBg = mix(accent, surfaces.container, isDark ? 0.2 : 0.12)
  const textAccent = readableAccent(accent, [surfaces.layout, surfaces.container, surfaces.elevated, selectedBg], isDark)
  const onPrimary = onAccent(accent)

  return {
    algorithm: isDark ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
    token: {
      borderRadius,
      colorPrimary: accent,
      ...(preset && 'hover' in preset ? { colorPrimaryHover: preset.hover } : {}),
      colorLink: textAccent,
      colorLinkHover: mix(textAccent, isDark ? WHITE : BLACK, 0.8),
      colorPrimaryText: textAccent,
      colorBgLayout: surfaces.layout,
      colorBgContainer: surfaces.container,
      colorBgElevated: surfaces.elevated,
      colorBorder: surfaces.border,
      colorBorderSecondary: surfaces.divider,
      colorText: surfaces.text,
      colorTextSecondary: surfaces.textSecondary,
      colorTextTertiary: surfaces.textTertiary,
    },
    components: {
      // Only where the accent is the background: colorTextLightSolid is also a tooltip's text and a red button's.
      Button: { primaryColor: onPrimary },
      Checkbox: { colorWhite: onPrimary },
      Radio: { buttonSolidCheckedColor: onPrimary },
      DatePicker: { colorTextLightSolid: onPrimary },
      Calendar: { colorTextLightSolid: onPrimary },
      Steps: { colorTextLightSolid: onPrimary },
      Menu: {
        itemBg: 'transparent',
        subMenuItemBg: 'transparent',
        itemColor: surfaces.menuItem,
        itemSelectedBg: selectedBg,
        itemSelectedColor: textAccent,
      },
      Table: {
        headerBg: surfaces.tableHeader,
        headerColor: surfaces.textSecondary,
        headerSplitColor: 'transparent',
        bodySortBg: surfaces.container,
        headerSortActiveBg: surfaces.tableHeader,
        fixedHeaderSortActiveBg: surfaces.tableHeader,
        headerSortHoverBg: surfaces.rowHover,
        rowHoverBg: surfaces.rowHover,
      },
    },
  }
}
