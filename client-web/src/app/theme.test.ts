import { describe, expect, it } from 'vitest'
import type { AppPalette } from '@/modules/identity/models'
import { ACCENT_PRESETS, appTheme, contrast, onAccent, PALETTES } from './theme'

const palettes = Object.keys(PALETTES) as AppPalette[]
const accents = [null, ...ACCENT_PRESETS.map((p) => p.color), '#ffeb3b', '#101010', '#7c3aed', '#ffffff']

const combinations = palettes.flatMap((palette) =>
  [true, false].flatMap((isDark) => accents.map((accentColor) => ({ palette, isDark, accentColor }))),
)

describe('appTheme', () => {
  it.each(combinations)('reads in $palette, dark $isDark, accent $accentColor', ({ palette, isDark, accentColor }) => {
    const { token, components } = appTheme({ palette, isDark, accentColor })
    const surfaces = [token!.colorBgLayout!, token!.colorBgContainer!, token!.colorBgElevated!]

    for (const surface of surfaces) {
      expect(contrast(token!.colorText!, surface)).toBeGreaterThanOrEqual(4.5)
      expect(contrast(token!.colorTextSecondary!, surface)).toBeGreaterThanOrEqual(4.5)
      expect(contrast(token!.colorLink!, surface)).toBeGreaterThanOrEqual(4.5)
    }

    const menu = components!.Menu!
    expect(contrast(menu.itemSelectedColor!, menu.itemSelectedBg!)).toBeGreaterThanOrEqual(4.5)
    expect(contrast(components!.Button!.primaryColor!, token!.colorPrimary!)).toBeGreaterThanOrEqual(3)
  })

  it('uses the palette accent until one is chosen', () => {
    expect(appTheme({ palette: 'reddit', isDark: true, accentColor: null }).token!.colorPrimary).toBe('#d93900')
    expect(appTheme({ palette: 'reddit', isDark: true, accentColor: '#2dcd88' }).token!.colorPrimary).toBe('#2dcd88')
  })

  it("gives the default palette Ant Design's blue, and the green its own hover", () => {
    expect(appTheme({ palette: 'default', isDark: true, accentColor: null }).token!.colorPrimary).toBe('#1677ff')
    expect(appTheme({ palette: 'default', isDark: true, accentColor: '#2dcd88' }).token!.colorPrimaryHover).toBe('#2dbc88')
    expect(appTheme({ palette: 'default', isDark: true, accentColor: '#1677ff' }).token!.colorPrimaryHover).toBeUndefined()
  })
})

describe('onAccent', () => {
  it('keeps white on the blue, and puts the navy on the green', () => {
    expect(onAccent('#1677ff')).toBe('#ffffff')
    expect(onAccent('#2dcd88')).toBe('#181b3d')
  })
})
