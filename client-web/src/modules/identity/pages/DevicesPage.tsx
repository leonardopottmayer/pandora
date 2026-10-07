import { App, Button, Empty, Popconfirm, Tag } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { toErrorMessage } from '@/lib/api/envelope'
import { desktopSettingsKey, useDesktop, type DesktopSettings, type PandoraDesktop } from '@/lib/desktop'
import * as devicesService from '../services/devices.service'
import type { Device } from '../models'

const DEVICES_KEY = ['identity', 'devices']

/**
 * Paired devices: each one calls the API with its own key, limited to its scopes. Inside Pandora
 * Desktop the page also pairs (or disconnects) the computer it is running on.
 */
export function DevicesPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const desktop = useDesktop()
  const queryClient = useQueryClient()

  const devices = useQuery({ queryKey: DEVICES_KEY, queryFn: devicesService.listDevices })
  const desktopSettings = useQuery({
    queryKey: desktopSettingsKey,
    queryFn: () => desktop!.invoke<DesktopSettings>('desktop.getSettings'),
    enabled: desktop !== null,
  })
  const thisDeviceId = desktopSettings.data?.deviceId ?? null

  const revoke = useMutation({
    mutationFn: async (id: string) => {
      await devicesService.revokeDevice(id)
      // Revoking this very computer also drops the key it keeps.
      if (desktop && id === thisDeviceId) await desktop.invoke('desktop.forgetCredential')
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DEVICES_KEY })
      void queryClient.invalidateQueries({ queryKey: desktopSettingsKey })
      message.success(t('devices.revoked'))
    },
    onError: (e) => message.error(toErrorMessage(e, t('devices.revokeError'))),
  })

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeading title={t('devices.title')} description={t('devices.subtitle')} />

      {desktop && desktopSettings.data && devices.data && (
        <ThisComputer
          desktop={desktop}
          settings={desktopSettings.data}
          paired={devices.data.find((d) => d.id === thisDeviceId) ?? null}
          onDisconnect={(id) => revoke.mutate(id)}
          disconnecting={revoke.isPending}
        />
      )}

      <SettingsSection title={t('devices.groupConnected')}>
        {devices.data?.length === 0 ? (
          <Empty className="py-6" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('devices.empty')} />
        ) : (
          devices.data?.map((device) => (
            <SettingRow
              key={device.id}
              label={
                <span>
                  {device.name}{' '}
                  {device.id === thisDeviceId && <Tag color="purple">{t('devices.thisComputer')}</Tag>}
                </span>
              }
              description={describe(device, t)}
              control={
                <Popconfirm
                  title={t('devices.revokeConfirm', { name: device.name })}
                  okText={t('devices.revoke')}
                  okButtonProps={{ danger: true }}
                  onConfirm={() => revoke.mutate(device.id)}
                >
                  <Button danger>{t('devices.revoke')}</Button>
                </Popconfirm>
              }
            />
          ))
        )}
      </SettingsSection>
    </div>
  )
}

function ThisComputer({
  desktop,
  settings,
  paired,
  onDisconnect,
  disconnecting,
}: {
  desktop: PandoraDesktop
  settings: DesktopSettings
  paired: Device | null
  onDisconnect: (id: string) => void
  disconnecting: boolean
}) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()

  const connect = useMutation({
    mutationFn: async () => {
      const { device, key } = await devicesService.registerDevice({
        name: settings.machineName,
        platform: settings.platform,
        form: settings.form,
        scopes: [],
      })
      await desktop.invoke('desktop.storeCredential', { deviceId: device.id, key })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DEVICES_KEY })
      void queryClient.invalidateQueries({ queryKey: desktopSettingsKey })
      message.success(t('devices.connected'))
    },
    onError: (e) => message.error(toErrorMessage(e, t('devices.connectError'))),
  })

  return (
    <SettingsSection title={t('devices.groupThisComputer')}>
      <SettingRow
        label={paired ? t('devices.pairedAs', { name: paired.name }) : t('devices.notPaired')}
        description={paired ? t('devices.pairedDesc') : t('devices.notPairedDesc')}
        control={
          paired ? (
            <Button loading={disconnecting} onClick={() => onDisconnect(paired.id)}>
              {t('devices.disconnect')}
            </Button>
          ) : (
            <Button type="primary" loading={connect.isPending} onClick={() => connect.mutate()}>
              {t('devices.connect')}
            </Button>
          )
        }
      />
    </SettingsSection>
  )
}

function describe(device: Device, t: (key: string, options?: Record<string, unknown>) => string): string {
  const seen = device.lastSeenAt
    ? t('devices.lastSeen', { when: new Date(device.lastSeenAt).toLocaleString() })
    : t('devices.neverSeen')
  return [t(`devices.platform.${device.platform}`), seen, device.scopes.join(', ')].filter(Boolean).join(' · ')
}
