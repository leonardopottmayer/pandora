import { Segmented, Select, Typography } from 'antd'
import { BulbOutlined, BulbFilled, DesktopOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
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
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeading title={t('settings.title')} description={t('settings.subtitle')} />

      <SettingsSection title={t('settings.groupAppearance')}>
        <SettingRow
          label={t('settings.themeLabel')}
          description={t('settings.themeDesc')}
          control={
            <Segmented<AppTheme>
              value={theme}
              onChange={setTheme}
              options={[
                { label: t('settings.themeLight'), value: 'light', icon: <BulbOutlined /> },
                { label: t('settings.themeDark'), value: 'dark', icon: <BulbFilled /> },
                { label: t('settings.themeSystem'), value: 'system', icon: <DesktopOutlined /> },
              ]}
            />
          }
        />
        <SettingRow
          label={t('settings.languageLabel')}
          description={t('settings.languageDesc')}
          control={
            <Segmented<AppLanguage>
              value={language}
              onChange={setLanguage}
              options={[
                { label: 'Portugues', value: 'pt-BR' },
                { label: 'English', value: 'en' },
              ]}
            />
          }
        />
      </SettingsSection>

      <SettingsSection title={t('settings.groupRegion')}>
        <SettingRow
          label={t('settings.timeZoneLabel')}
          description={t('settings.timeZoneDesc')}
          control={
            <Select
              className="w-full min-w-56 sm:w-64"
              showSearch
              value={timeZone}
              onChange={setTimeZone}
              options={timeZoneOptions(timeZone)}
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
              value={defaultAlertOffsetMinutes}
              onChange={setDefaultAlertOffsetMinutes}
              options={offsetOptions}
            />
          }
        />
      </SettingsSection>

      <Typography.Text type="secondary" className="text-xs">
        {t('settings.sharedHint')}
      </Typography.Text>
    </div>
  )
}
