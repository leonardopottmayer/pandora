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
import type { CalendarBindingDto, CalendarBindingStatus, SyncConflictDto } from '../../models'
import {
  useBindGoogleCalendars,
  useCalendarBindings,
  useGoogleCalendars,
  useSyncBinding,
  useSyncConflicts,
  useUnlinkBinding,
} from '../../hooks/useGoogleCalendar'

/** Reading calendars needs a calendar scope; the default Google consent already asks for this one. */
export const GOOGLE_CALENDAR_SCOPE = 'https://www.googleapis.com/auth/calendar'

const STATUS_COLOR: Record<CalendarBindingStatus, string> = {
  ok: 'green',
  'needs-reconnect': 'orange',
  error: 'red',
}

/**
 * Google Calendar ↔ Pandora: pick a Google account, tick its calendars, and each one becomes a Pandora
 * calendar that syncs every two minutes, both ways where the account can write. Below the links, the
 * events both sides changed at once, with the version that lost.
 */
export function GoogleCalendarSection() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [accountId, setAccountId] = useState<string | null>(null)
  const [picked, setPicked] = useState<string[]>([])

  const { data: bindings, error: bindingsError } = useCalendarBindings()
  const { data: googleCalendars, isFetching: loadingCalendars, error: calendarsError } = useGoogleCalendars(accountId)
  const bind = useBindGoogleCalendars()
  const sync = useSyncBinding()

  async function bringPicked() {
    if (accountId === null || picked.length === 0) return
    try {
      const created = await bind.mutateAsync({ accountId, calendarIds: picked })
      setPicked([])
      message.success(t('agenda.google.bound', { count: created.length }))
      // The first sync brings the events now rather than on the next job cycle.
      for (const binding of created) await sync.mutateAsync(binding.id).catch(() => undefined)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.google.bindError')))
    }
  }

  const unbound = (googleCalendars ?? []).filter((c) => !c.bindingId)

  return (
    <SettingsSection title={t('agenda.google.title')}>
      <Typography.Paragraph type="secondary" className="!mb-0">
        {t('agenda.google.intro')}
      </Typography.Paragraph>

      {bindingsError && <Alert type="error" showIcon title={bindingsError.message} />}
      {(bindings ?? []).map((binding) => (
        <BindingRow key={binding.id} binding={binding} />
      ))}
      <ConflictList />

      <SettingRow
        vertical
        label={t('agenda.google.addLabel')}
        description={t('agenda.google.addDesc')}
        control={
          <div className="flex flex-col gap-3">
            <AccountSelect
              className="w-full sm:w-80"
              providers={['google']}
              authKind="oauth"
              requiredScopes={[GOOGLE_CALENDAR_SCOPE]}
              value={accountId}
              onChange={(id) => {
                setAccountId(id)
                setPicked([])
              }}
              redirectAfter="/agenda/settings"
            />
            {accountId !== null && calendarsError && (
              <Alert type="error" showIcon title={toErrorMessage(calendarsError, t('agenda.google.listError'))} />
            )}
            {accountId !== null && loadingCalendars && <Spin size="small" />}
            {accountId !== null && googleCalendars && unbound.length === 0 && (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('agenda.google.allBound')} />
            )}
            {unbound.length > 0 && (
              <>
                <Checkbox.Group
                  className="flex flex-col gap-2"
                  value={picked}
                  onChange={(values) => setPicked(values as string[])}
                  options={unbound.map((c) => ({
                    value: c.id,
                    label: (
                      <span className="inline-flex min-w-0 items-center gap-2">
                        <span className="size-3 shrink-0 rounded-sm" style={{ background: c.color ?? undefined }} />
                        <span className="break-words">{c.name}</span>
                        {c.isPrimary && <Tag className="!m-0">{t('agenda.google.primary')}</Tag>}
                      </span>
                    ),
                  }))}
                />
                <Button
                  type="primary"
                  className="w-fit"
                  icon={<GoogleOutlined />}
                  disabled={picked.length === 0}
                  loading={bind.isPending || sync.isPending}
                  onClick={bringPicked}
                >
                  {t('agenda.google.bring', { count: picked.length })}
                </Button>
              </>
            )}
          </div>
        }
      />
    </SettingsSection>
  )
}

/** The title the losing version carried: `summary` on an event, `title` on a task. */
function discardedTitle(conflict: SyncConflictDto): string {
  try {
    const version = JSON.parse(conflict.losingVersion) as { summary?: string; title?: string }
    return version.summary || version.title || conflict.title
  } catch {
    return conflict.title
  }
}

function ConflictList() {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const { data: conflicts, error } = useSyncConflicts()

  if (error) return <Alert type="error" showIcon title={toErrorMessage(error, t('agenda.google.conflictsError'))} />
  if (!conflicts?.length) return null

  return (
    <SettingRow
      vertical
      label={t('agenda.google.conflictsLabel')}
      description={t('agenda.google.conflictsDesc')}
      control={
        <ul className="m-0 flex list-none flex-col gap-2 p-0">
          {conflicts.map((conflict) => (
            <li key={conflict.id} className="flex min-w-0 flex-col">
              <span className="break-words">
                {conflict.title} · {t(`agenda.google.conflictWinner.${conflict.losingSide}`)}
              </span>
              <Typography.Text type="secondary" className="break-words">
                {formatDateTime(conflict.detectedAt, timeZone)} ·{' '}
                {t('agenda.google.conflictDiscarded', { title: discardedTitle(conflict) })}
              </Typography.Text>
            </li>
          ))}
        </ul>
      }
    />
  )
}

function BindingRow({ binding }: { binding: CalendarBindingDto }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { timeZone } = usePreferences()
  const sync = useSyncBinding()
  const unlink = useUnlinkBinding()

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
      message.success(t('agenda.google.unlinked'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.google.unlinkError')))
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
          <span className="size-3 shrink-0 rounded-sm" style={{ background: binding.calendarColor ?? undefined }} />
          <span className="break-words">{binding.calendarName}</span>
          <Tag color={STATUS_COLOR[binding.status]} className="!m-0">
            {t(`agenda.google.status.${binding.status}`)}
          </Tag>
          <Tag className="!m-0">{t(`agenda.google.direction.${binding.direction}`)}</Tag>
        </span>
      }
      description={description}
      control={
        <div className="flex flex-wrap gap-2">
          <Button icon={<SyncOutlined />} loading={sync.isPending} onClick={syncNow}>
            {t('agenda.google.syncNow')}
          </Button>
          <Popconfirm
            title={t('agenda.google.unlinkTitle')}
            description={t('agenda.google.unlinkDesc')}
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
