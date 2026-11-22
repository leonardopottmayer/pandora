import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { PreferencesContext, type PreferencesContextValue } from '../context/preferences-context'
import { AppearanceSettings } from './AppearanceSettings'

function renderSettings(overrides: Partial<PreferencesContextValue> = {}) {
  const setPalette = vi.fn()
  const setAccentColor = vi.fn()
  const value = { palette: 'default', accentColor: null, isDark: true, setPalette, setAccentColor, ...overrides }
  renderWithProviders(
    <PreferencesContext.Provider value={value as PreferencesContextValue}>
      <AppearanceSettings />
    </PreferencesContext.Provider>,
  )
  return { setPalette, setAccentColor }
}

describe('AppearanceSettings', () => {
  it('offers the three palettes and marks the one in use', async () => {
    const { setPalette } = renderSettings({ palette: 'claude' })

    expect(screen.getByRole('button', { name: 'Claude' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Default' })).toHaveAttribute('aria-pressed', 'false')

    await userEvent.click(screen.getByRole('button', { name: 'Reddit' }))
    expect(setPalette).toHaveBeenCalledWith('reddit')
  })

  it('picks a ready-made accent, and goes back to the color of the palette', async () => {
    const { setAccentColor } = renderSettings({ accentColor: '#2dcd88' })

    expect(screen.getByRole('button', { name: 'Green' })).toHaveAttribute('aria-pressed', 'true')

    await userEvent.click(screen.getByRole('button', { name: 'Blue' }))
    expect(setAccentColor).toHaveBeenCalledWith('#1677ff')

    await userEvent.click(screen.getByRole('button', { name: 'Theme color' }))
    expect(setAccentColor).toHaveBeenCalledWith(null)
  })
})
