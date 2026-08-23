import { Card, Divider, Segmented, Select, Typography } from 'antd'
import { BulbOutlined, BulbFilled, DesktopOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { usePreferences } from '../context/preferences-context'
import type { AppLanguage, AppTheme, WeekStartsOn } from '../models'

function timeZoneOptions(current: string): { label: string; value: string }[] {
  const intl = Intl as typeof Intl & { supportedValuesOf?: (key: string) => string[] }
  const supported =
    typeof intl.supportedValuesOf === 'function' ? intl.supportedValuesOf('timeZone') : []
  const values = supported.length > 0 ? supported : [current]
  if (!values.includes(current)) values.unshift(current)
  return values.map((v) => ({ label: v, value: v }))
}

export function SettingsPage() {
  const { t } = useTranslation()
  const {
    theme,
    setTheme,
    language,
    setLanguage,
    timeZone,
    setTimeZone,
    weekStartsOn,
    setWeekStartsOn,
    defaultAlertOffsetMinutes,
    setDefaultAlertOffsetMinutes,
  } = usePreferences()

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
      <Card title={t('settings.title')}>
        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('settings.themeLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('settings.themeDesc')}</Typography.Text>
          <Segmented<AppTheme>
            className="mt-2 w-fit"
            value={theme}
            onChange={setTheme}
            options={[
              { label: t('settings.themeLight'), value: 'light', icon: <BulbOutlined /> },
              { label: t('settings.themeDark'), value: 'dark', icon: <BulbFilled /> },
              { label: t('settings.themeSystem'), value: 'system', icon: <DesktopOutlined /> },
            ]}
          />
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('settings.languageLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('settings.languageDesc')}</Typography.Text>
          <Segmented<AppLanguage>
            className="mt-2 w-fit"
            value={language}
            onChange={setLanguage}
            options={[
              { label: 'Portugues', value: 'pt-BR' },
              { label: 'English', value: 'en' },
            ]}
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
