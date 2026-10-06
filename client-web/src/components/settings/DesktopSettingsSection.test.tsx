import { describe, it, expect, beforeAll, afterEach, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import type { PandoraDesktop } from '@/lib/desktop'
import { DesktopSettingsSection } from './DesktopSettingsSection'

function fakeDesktop(autostart: boolean) {
  const invoke = vi.fn(async (method: string, args?: unknown) => {
    if (method === 'desktop.getSettings') return { version: '0.1.0', serverUrl: 'http://homelab:8730/', autostart }
    if (method === 'desktop.setAutostart') return (args as { enabled: boolean }).enabled
    return null
  })
  window.pandoraDesktop = {
    version: '0.1.0',
    capabilities: async () => [],
    invoke: invoke as PandoraDesktop['invoke'],
    on: () => () => {},
  }
  return invoke
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

afterEach(() => {
  delete window.pandoraDesktop
})

describe('DesktopSettingsSection', () => {
  it('renders nothing in a browser', () => {
    renderWithProviders(<DesktopSettingsSection />)
    expect(screen.queryByText('Pandora Desktop')).not.toBeInTheDocument()
  })

  it('shows the app settings and toggles start with Windows through the bridge', async () => {
    const invoke = fakeDesktop(false)
    renderWithProviders(<DesktopSettingsSection />)

    expect(await screen.findByText('http://homelab:8730/')).toBeInTheDocument()
    expect(screen.getByText('0.1.0')).toBeInTheDocument()

    const toggle = screen.getByRole('switch')
    await waitFor(() => expect(toggle).not.toHaveClass('ant-switch-loading'))
    await userEvent.click(toggle)

    expect(invoke).toHaveBeenCalledWith('desktop.setAutostart', { enabled: true })
    await waitFor(() => expect(toggle).toBeChecked())
  })
})
