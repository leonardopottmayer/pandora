import { App, Card, Divider, Segmented, Select, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { WeekStartsOn } from '@/modules/identity/models'
import { toErrorMessage } from '@/lib/api/envelope'
import { useCalendars, useUpdateCalendar } from '../../hooks/useCalendars'

function timeZoneOptions(current: string): { label: string; value: string }[] {
  const intl = Intl as typeof Intl & { supportedValuesOf?: (key: string) => string[] }
  const supported =
    typeof intl.supportedValuesOf === 'function' ? intl.supportedValuesOf('timeZone') : []
  const values = supported.length > 0 ? supported : [current]
  if (!values.includes(current)) values.unshift(current)
  return values.map((v) => ({ label: v, value: v }))
}

export function AgendaSettingsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const {
    timeZone,
    setTimeZone,
    weekStartsOn,
    setWeekStartsOn,
    defaultAlertOffsetMinutes,
    setDefaultAlertOffsetMinutes,
  } = usePreferences()

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
    <div className="mx-auto max-w-2xl">
      <Card title={t('agenda.settings.title')}>
        <Typography.Paragraph type="secondary">{t('agenda.settings.intro')}</Typography.Paragraph>

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('agenda.settings.defaultCalendarLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('agenda.settings.defaultCalendarDesc')}</Typography.Text>
          <Select
            className="mt-2 w-full max-w-xs"
            placeholder={t('agenda.settings.noCalendars')}
            value={defaultCalendarId}
            disabled={calendars.length === 0}
            onChange={chooseDefaultCalendar}
            options={calendars.map((c) => ({ label: c.name, value: c.id }))}
          />
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('settings.timeZoneLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('settings.timeZoneDesc')}</Typography.Text>
          <Select
            className="mt-2 w-full max-w-xs"
            showSearch
            value={timeZone}
            onChange={setTimeZone}
            options={timeZoneOptions(timeZone)}
          />
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('settings.weekStartsOnLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('settings.weekStartsOnDesc')}</Typography.Text>
          <Segmented<WeekStartsOn>
            className="mt-2 w-fit"
            value={weekStartsOn}
            onChange={setWeekStartsOn}
            options={[
              { label: t('settings.weekSunday'), value: 'sunday' },
              { label: t('settings.weekMonday'), value: 'monday' },
            ]}
          />
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('settings.alertOffsetLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('settings.alertOffsetDesc')}</Typography.Text>
          <Select
            className="mt-2 w-full max-w-xs"
            value={defaultAlertOffsetMinutes}
            onChange={setDefaultAlertOffsetMinutes}
            options={offsetOptions}
          />
        </div>
      </Card>
    </div>
  )
}
