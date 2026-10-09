import { Alert, App, Switch } from 'antd'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { toErrorMessage } from '@/lib/api/envelope'
import { useDesktop, type PandoraDesktop } from '@/lib/desktop'
import { filesKeys, useFilesAgent, useFilesPreferences } from '../hooks/useFiles'
import * as filesService from '../services/files.service'

/** The two switches of Files: the account's ("I use Files") and, inside Pandora Desktop, this PC's ("this PC scans"). */
export function FilesSettingsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const desktop = useDesktop()
  const { data, isLoading } = useFilesPreferences()

  const save = useMutation({
    mutationFn: filesService.savePreferences,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: filesKeys.preferences() }),
    onError: (e) => message.error(toErrorMessage(e, t('files.settings.saveError'))),
  })

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeading title={t('files.settings.title')} description={t('files.settings.intro')} />

      <SettingsSection>
        <SettingRow
          label={t('files.settings.enabledLabel')}
          description={t('files.settings.enabledDesc')}
          control={
            <Switch
              checked={data?.isEnabled ?? false}
              loading={isLoading || save.isPending}
              onChange={(checked) => save.mutate(checked)}
            />
          }
        />
      </SettingsSection>

      {desktop && <ThisComputer desktop={desktop} />}
    </div>
  )
}

interface DesktopModule {
  name: string
  enabled: boolean
}

function ThisComputer({ desktop }: { desktop: PandoraDesktop }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { deviceId, status } = useFilesAgent()

  const modules = useQuery({
    queryKey: ['desktop', 'modules'],
    queryFn: () => desktop.invoke<DesktopModule[]>('desktop.getModules'),
  })
  const enabled = modules.data?.find((m) => m.name === 'files')?.enabled ?? false

  // Turning a desktop module on or off restarts the app, which reloads this page.
  const setModule = useMutation({
    mutationFn: (on: boolean) => desktop.invoke<{ restarting: boolean }>('desktop.setModule', { name: 'files', enabled: on }),
    onSuccess: ({ restarting }) => restarting && message.info(t('files.settings.restarting')),
    onError: (e) => message.error(toErrorMessage(e, t('files.settings.saveError'))),
  })

  return (
    <>
      <SettingsSection title={t('files.settings.groupThisComputer')}>
        <SettingRow
          label={t('files.settings.deviceLabel')}
          description={t('files.settings.deviceDesc')}
          control={
            <Switch
              checked={enabled}
              loading={modules.isLoading || setModule.isPending || setModule.data?.restarting === true}
              onChange={(checked) => setModule.mutate(checked)}
            />
          }
        />
      </SettingsSection>
      {enabled && (!deviceId || status?.paired === false) && (
        <Alert
          type="warning"
          showIcon
          title={t('files.settings.notPaired')}
          description={<Link to="/account/devices">{t('files.settings.pairAction')}</Link>}
        />
      )}
    </>
  )
}
