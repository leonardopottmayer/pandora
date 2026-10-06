import { Button, Switch, Typography } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useDesktop, type DesktopSettings, type PandoraDesktop } from '@/lib/desktop'
import { SettingsSection } from './SettingsSection'
import { SettingRow } from './SettingRow'

const settingsKey = ['desktop', 'settings'] as const

/** The app's own settings (start with Windows, server, version). Renders nothing outside Pandora Desktop. */
export function DesktopSettingsSection() {
  const desktop = useDesktop()
  if (!desktop) return null
  return <DesktopSettingsRows desktop={desktop} />
}

function DesktopSettingsRows({ desktop }: { desktop: PandoraDesktop }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data } = useQuery({
    queryKey: settingsKey,
    queryFn: () => desktop.invoke<DesktopSettings>('desktop.getSettings'),
  })

  const autostart = useMutation({
    mutationFn: (enabled: boolean) => desktop.invoke<boolean>('desktop.setAutostart', { enabled }),
    onSuccess: (enabled) =>
      queryClient.setQueryData<DesktopSettings>(settingsKey, (old) => (old ? { ...old, autostart: enabled } : old)),
  })

  return (
    <SettingsSection title={t('settings.groupDesktop')}>
      <SettingRow
        label={t('settings.desktopAutostartLabel')}
        description={t('settings.desktopAutostartDesc')}
        control={
          <Switch
            checked={data?.autostart ?? false}
            loading={!data || autostart.isPending}
            onChange={(checked) => autostart.mutate(checked)}
          />
        }
      />
      <SettingRow
        label={t('settings.desktopServerLabel')}
        description={data?.serverUrl ?? ''}
        control={
          <Button onClick={() => void desktop.invoke('desktop.changeServer')}>
            {t('settings.desktopServerChange')}
          </Button>
        }
      />
      <SettingRow
        label={t('settings.desktopVersionLabel')}
        control={<Typography.Text type="secondary">{desktop.version}</Typography.Text>}
      />
    </SettingsSection>
  )
}
