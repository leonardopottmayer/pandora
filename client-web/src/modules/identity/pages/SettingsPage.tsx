import { Segmented, Select } from 'antd'
import { BulbOutlined, BulbFilled, DesktopOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { DesktopSettingsSection } from '@/components/settings/DesktopSettingsSection'
import { usePreferences } from '../context/preferences-context'
import type { AppLanguage, AppTheme } from '../models'

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
  const { theme, setTheme, language, setLanguage, timeZone, setTimeZone } = usePreferences()

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
      </SettingsSection>

      <DesktopSettingsSection />
    </div>
  )
}
