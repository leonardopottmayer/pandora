import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Checkbox, Empty, Popconfirm, Spin, Tag, Typography } from 'antd'
import { GoogleOutlined, SyncOutlined } from '@ant-design/icons'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { AccountSelect } from '@/modules/integrations/components/AccountSelect'
import { formatDateTime } from '../../lib/datetime'
import type { CalendarBindingStatus, TaskListBindingDto } from '../../models'
import {
  useBindGoogleTaskLists,
  useGoogleTaskLists,
  useSyncTaskListBinding,
  useTaskListBindings,
  useUnlinkTaskListBinding,
} from '../../hooks/useGoogleCalendar'

/** Google Tasks needs its own scope; accounts connected before it was asked for have to reconnect. */
export const GOOGLE_TASKS_SCOPE = 'https://www.googleapis.com/auth/tasks'

const STATUS_COLOR: Record<CalendarBindingStatus, string> = {
  ok: 'green',
  'needs-reconnect': 'orange',
  error: 'red',
}

/**
 * Google Tasks ↔ Pandora: pick a Google account, tick its task lists, and each one becomes a Pandora list
 * synced both ways every two minutes. Conflicts are listed with the calendar ones.
 */
export function GoogleTasksSection() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [accountId, setAccountId] = useState<string | null>(null)
  const [picked, setPicked] = useState<string[]>([])

  const { data: bindings, error: bindingsError } = useTaskListBindings()
  const { data: googleLists, isFetching: loadingLists, error: listsError } = useGoogleTaskLists(accountId)
  const bind = useBindGoogleTaskLists()
  const sync = useSyncTaskListBinding()

  async function bringPicked() {
    if (accountId === null || picked.length === 0) return
    try {
      const created = await bind.mutateAsync({ accountId, listIds: picked })
      setPicked([])
      message.success(t('agenda.googleTasks.bound', { count: created.length }))
      // The first sync brings the tasks now rather than on the next job cycle.
      for (const binding of created) await sync.mutateAsync(binding.id).catch(() => undefined)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.googleTasks.bindError')))
    }
  }

  const unbound = (googleLists ?? []).filter((l) => !l.bindingId)

  return (
    <SettingsSection title={t('agenda.googleTasks.title')}>
      <Typography.Paragraph type="secondary" className="!mb-0">
        {t('agenda.googleTasks.intro')}
      </Typography.Paragraph>

      {bindingsError && <Alert type="error" showIcon title={bindingsError.message} />}
      {(bindings ?? []).map((binding) => (
        <TaskListBindingRow key={binding.id} binding={binding} />
      ))}

      <SettingRow
        vertical
        label={t('agenda.googleTasks.addLabel')}
        description={t('agenda.googleTasks.addDesc')}
        control={
          <div className="flex flex-col gap-3">
            <AccountSelect
              className="w-full sm:w-80"
              providers={['google']}
              authKind="oauth"
              requiredScopes={[GOOGLE_TASKS_SCOPE]}
              value={accountId}
              onChange={(id) => {
                setAccountId(id)
                setPicked([])
              }}
              redirectAfter="/agenda/settings"
            />
            {accountId !== null && listsError && (
              <Alert type="error" showIcon title={toErrorMessage(listsError, t('agenda.googleTasks.listError'))} />
            )}
            {accountId !== null && loadingLists && <Spin size="small" />}
            {accountId !== null && googleLists && unbound.length === 0 && (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('agenda.googleTasks.allBound')} />
            )}
            {unbound.length > 0 && (
              <>
                <Checkbox.Group
                  className="flex flex-col gap-2"
                  value={picked}
                  onChange={(values) => setPicked(values as string[])}
                  options={unbound.map((l) => ({ value: l.id, label: <span className="break-words">{l.name}</span> }))}
                />
                <Button
                  type="primary"
                  className="w-fit"
                  icon={<GoogleOutlined />}
                  disabled={picked.length === 0}
                  loading={bind.isPending || sync.isPending}
                  onClick={bringPicked}
                >
                  {t('agenda.googleTasks.bring', { count: picked.length })}
                </Button>
              </>
            )}
          </div>
        }
      />
    </SettingsSection>
  )
}

function TaskListBindingRow({ binding }: { binding: TaskListBindingDto }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { timeZone } = usePreferences()
  const sync = useSyncTaskListBinding()
  const unlink = useUnlinkTaskListBinding()

  async function syncNow() {
    try {
      const result = await sync.mutateAsync(binding.id)
      if (result.status === 'ok') message.success(t('agenda.google.synced'))
      else message.warning(result.lastError ?? t(`agenda.google.status.${result.status}`))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.google.syncError')))
    }
  }

  async function unlinkNow() {
    try {
      await unlink.mutateAsync(binding.id)
      message.success(t('agenda.googleTasks.unlinked'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.googleTasks.unlinkError')))
    }
  }

  const description = [
    binding.lastSyncedAt
      ? t('agenda.google.lastSynced', { when: formatDateTime(binding.lastSyncedAt, timeZone) })
      : t('agenda.google.neverSynced'),
    binding.status !== 'ok' ? binding.lastError : null,
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <SettingRow
      label={
        <span className="inline-flex min-w-0 flex-wrap items-center gap-2">
          <span className="break-words">{binding.listName}</span>
          <Tag color={STATUS_COLOR[binding.status]} className="!m-0">
            {t(`agenda.google.status.${binding.status}`)}
          </Tag>
        </span>
      }
      description={description}
      control={
        <div className="flex flex-wrap gap-2">
          <Button icon={<SyncOutlined />} loading={sync.isPending} onClick={syncNow}>
            {t('agenda.google.syncNow')}
          </Button>
          <Popconfirm
            title={t('agenda.googleTasks.unlinkTitle')}
            description={t('agenda.googleTasks.unlinkDesc')}
            okText={t('agenda.google.unlink')}
            cancelText={t('common.cancel')}
            onConfirm={unlinkNow}
          >
            <Button loading={unlink.isPending}>{t('agenda.google.unlink')}</Button>
          </Popconfirm>
        </div>
      }
    />
  )
}
