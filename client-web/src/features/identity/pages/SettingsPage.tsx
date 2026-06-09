import { Card, Divider, Segmented, Typography } from 'antd'
import { BulbOutlined, BulbFilled, DesktopOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { usePreferences } from '../context/preferences-context'
import type { AppLanguage, AppTheme } from '../models'

export function SettingsPage() {
  const { t } = useTranslation()
  const { theme, setTheme, language, setLanguage } = usePreferences()

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
      </Card>
    </div>
  )
}
