import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { Tabs } from 'antd'
import { PageHeading } from '@/components/settings/PageHeading'
import { DeliveryHistory } from '../components/DeliveryHistory'
import { NotificationSettingsForm } from '../components/NotificationSettingsForm'

const HISTORY_TAB = 'history'
const SETTINGS_TAB = 'settings'

export function NotificationsPage() {
  const { t } = useTranslation()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') === HISTORY_TAB ? HISTORY_TAB : SETTINGS_TAB

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4">
      <PageHeading title={t('notifications.pageTitle')} description={t('notifications.pageSubtitle')} />
      <Tabs
        activeKey={tab}
        onChange={(next) => setSearchParams(next === HISTORY_TAB ? { tab: HISTORY_TAB } : {}, { replace: true })}
        items={[
          { key: SETTINGS_TAB, label: t('notifications.tabSettings'), children: <NotificationSettingsForm /> },
          { key: HISTORY_TAB, label: t('notifications.historyTitle'), children: <DeliveryHistory /> },
        ]}
      />
    </div>
  )
}
