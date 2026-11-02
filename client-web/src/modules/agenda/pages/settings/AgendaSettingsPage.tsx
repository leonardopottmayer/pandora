import { App, Segmented, Select, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { WeekStartsOn } from '@/modules/identity/models'
import { toErrorMessage } from '@/lib/api/envelope'
import { useAgendaPreferences, useSaveAgendaPreferences } from '../../hooks/useAgendaPreferences'
import { useCalendars, useUpdateCalendar } from '../../hooks/useCalendars'

export function AgendaSettingsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { timeZone, weekStartsOn, setWeekStartsOn } = usePreferences()
  const { data: preferences } = useAgendaPreferences()
  const savePreferences = useSaveAgendaPreferences()

  const { data: calendarList } = useCalendars()
  const calendars = (calendarList ?? []).filter((c) => !c.archivedAt)
  const defaultCalendarId = calendars.find((c) => c.isDefault)?.id
  const updateCalendar = useUpdateCalendar()

  async function chooseDefaultCalendar(id: string) {
    try {
      await updateCalendar.mutateAsync({ id, body: { isDefault: true } })
      message.success(t('agenda.calendars.updated'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.calendars.saveError')))
    }
  }

  async function chooseDefaultAlert(defaultAlertOffsetMinutes: number) {
    try {
      await savePreferences.mutateAsync({ defaultAlertOffsetMinutes })
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.settings.saveError')))
    }
  }

  const offsetOptions = [
    { label: t('settings.alertOffsetAtTime'), value: 0 },
    { label: t('settings.alertOffsetMinutes', { count: 5 }), value: -5 },
    { label: t('settings.alertOffsetMinutes', { count: 10 }), value: -10 },
    { label: t('settings.alertOffsetMinutes', { count: 15 }), value: -15 },
    { label: t('settings.alertOffsetMinutes', { count: 30 }), value: -30 },
    { label: t('settings.alertOffsetHours', { count: 1 }), value: -60 },
    { label: t('settings.alertOffsetDays', { count: 1 }), value: -1440 },
  ]

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeading title={t('agenda.settings.title')} description={t('agenda.settings.intro')} />

      <SettingsSection title={t('agenda.settings.groupAgenda')}>
        <SettingRow
          label={t('agenda.settings.defaultCalendarLabel')}
          description={t('agenda.settings.defaultCalendarDesc')}
          control={
            <Select
              className="w-full min-w-56 sm:w-64"
              placeholder={t('agenda.settings.noCalendars')}
              value={defaultCalendarId}
              disabled={calendars.length === 0}
              onChange={chooseDefaultCalendar}
              options={calendars.map((c) => ({ label: c.name, value: c.id }))}
            />
          }
        />
        <SettingRow
          label={t('settings.weekStartsOnLabel')}
          description={t('settings.weekStartsOnDesc')}
          control={
            <Segmented<WeekStartsOn>
              value={weekStartsOn}
              onChange={setWeekStartsOn}
              options={[
                { label: t('settings.weekSunday'), value: 'sunday' },
                { label: t('settings.weekMonday'), value: 'monday' },
              ]}
            />
          }
        />
        <SettingRow
          label={t('settings.alertOffsetLabel')}
          description={t('settings.alertOffsetDesc')}
          control={
            <Select
              className="w-full min-w-56 sm:w-64"
              value={preferences?.defaultAlertOffsetMinutes}
              disabled={!preferences}
              onChange={chooseDefaultAlert}
              options={offsetOptions}
            />
          }
        />
      </SettingsSection>

      <SettingsSection title={t('agenda.settings.groupInherited')}>
        <SettingRow
          label={t('settings.timeZoneLabel')}
          description={t('agenda.settings.inheritedDesc')}
          control={
            <div className="flex items-center gap-3">
              <Typography.Text type="secondary">{timeZone}</Typography.Text>
              <Link to="/settings" className="whitespace-nowrap text-sm font-medium">
                {t('agenda.settings.editInGeneral')}
              </Link>
            </div>
          }
        />
      </SettingsSection>
    </div>
  )
}
