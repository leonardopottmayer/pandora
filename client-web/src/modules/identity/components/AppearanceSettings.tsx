import { ColorPicker, Typography, theme as antdTheme } from 'antd'
import { CheckOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { SettingRow } from '@/components/settings/SettingRow'
import { ACCENT_PRESETS, onAccent, PALETTES } from '@/app/theme'
import { usePreferences } from '../context/preferences-context'
import type { AppPalette } from '../models'

const PALETTE_KEYS = Object.keys(PALETTES) as AppPalette[]

/** A palette as a small picture of itself, in the mode in use. */
function PalettePicker() {
  const { t } = useTranslation()
  const { token } = antdTheme.useToken()
  const { palette, setPalette, isDark } = usePreferences()

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
      {PALETTE_KEYS.map((key) => {
        const colors = PALETTES[key][isDark ? 'dark' : 'light']
        const accent = PALETTES[key].accent
        const selected = palette === key
        return (
          <button
            key={key}
            type="button"
            aria-pressed={selected}
            onClick={() => setPalette(key)}
            className="flex cursor-pointer flex-col gap-2 rounded-lg bg-transparent p-2 text-left"
            style={{
              border: `${selected ? 2 : 1}px solid ${selected ? token.colorPrimary : token.colorBorderSecondary}`,
              color: token.colorText,
            }}
          >
            <span className="flex h-16 flex-col gap-1.5 rounded-md p-2" style={{ background: colors.layout }} aria-hidden>
              <span className="flex flex-1 items-center gap-1.5 rounded px-1.5" style={{ background: colors.container }}>
                <span className="h-1.5 flex-1 rounded-full" style={{ background: colors.textSecondary }} />
                <span className="h-3 w-6 rounded-sm" style={{ background: accent }} />
              </span>
              <span className="h-1.5 w-2/3 rounded-full" style={{ background: colors.border }} />
            </span>
            <span className="text-sm font-medium">{t(`settings.palette.${key}`)}</span>
          </button>
        )
      })}
    </div>
  )
}

function Swatch({ color, label, selected, onClick }: { color: string; label: string; selected: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={selected}
      title={label}
      onClick={onClick}
      className="flex h-8 w-8 cursor-pointer items-center justify-center rounded-full border-0"
      style={{ background: color, color: onAccent(color) }}
    >
      {selected && <CheckOutlined aria-hidden />}
    </button>
  )
}

function AccentPicker() {
  const { t } = useTranslation()
  const { palette, accentColor, setAccentColor } = usePreferences()
  const presets: readonly string[] = ACCENT_PRESETS.map((p) => p.color)
  const isCustom = accentColor !== null && !presets.includes(accentColor)

  return (
    <div className="flex flex-wrap items-center gap-3">
      <Swatch
        color={PALETTES[palette].accent}
        label={t('settings.accent.palette')}
        selected={accentColor === null}
        onClick={() => setAccentColor(null)}
      />
      <Typography.Text type="secondary" className="text-xs">
        {t('settings.accent.palette')}
      </Typography.Text>
      {ACCENT_PRESETS.map((preset) => (
        <Swatch
          key={preset.key}
          color={preset.color}
          label={t(`settings.accent.${preset.key}`)}
          selected={accentColor === preset.color}
          onClick={() => setAccentColor(preset.color)}
        />
      ))}
      <ColorPicker
        disabledAlpha
        format="hex"
        value={isCustom ? accentColor : PALETTES[palette].accent}
        onChangeComplete={(color) => setAccentColor(color.toHexString())}
        showText={() => t('settings.accent.custom')}
      />
    </div>
  )
}

export function AppearanceSettings() {
  const { t } = useTranslation()
  return (
    <>
      <SettingRow vertical label={t('settings.palette.label')} description={t('settings.palette.desc')} control={<PalettePicker />} />
      <SettingRow vertical label={t('settings.accent.label')} description={t('settings.accent.desc')} control={<AccentPicker />} />
    </>
  )
}
