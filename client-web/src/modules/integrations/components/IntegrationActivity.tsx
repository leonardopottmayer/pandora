import { useTranslation } from 'react-i18next'
import { Alert, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { providerLabel, type IntegrationEvent, type IntegrationEventType } from '../models'
import { useAccounts, useIntegrationEvents } from '../hooks/useIntegrations'

const EVENT_TAG_COLOR: Record<IntegrationEventType, string> = {
  connected: 'green', reconnected: 'green', 'refresh-failed': 'orange',
  expired: 'orange', revoked: 'red', disconnected: 'default',
}

export function IntegrationActivity() {
  const { t, i18n } = useTranslation()
  const { timeZone } = usePreferences()
  const { data: events, isLoading, error: eventsError } = useIntegrationEvents()
  const { data: accounts, error: accountsError } = useAccounts()
  const loadError = eventsError ?? accountsError
  const date = (value: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'short', timeStyle: 'short', timeZone }).format(new Date(value))
  function account(event: IntegrationEvent) {
    // Disconnected accounts no longer exist, so only their provider remains.
    const label = accounts?.find((item) => item.id === event.externalAccountId)?.label
    const provider = providerLabel(event.provider)
    return label ? `${provider} · ${label}` : provider
  }
  const columns: ColumnsType<IntegrationEvent> = [
    {
      title: t('connections.activity.colEvent'), key: 'event',
      render: (_, event) => <div className="flex min-w-0 flex-col gap-1">
        <div><Tag color={EVENT_TAG_COLOR[event.eventType]}>{t(`connections.activity.event.${event.eventType}`)}</Tag></div>
        <Typography.Text type="secondary" className="truncate text-xs md:hidden" title={account(event)}>{account(event)}</Typography.Text>
        <Typography.Text type="secondary" className="text-xs tabular-nums lg:hidden">{date(event.occurredAt)}</Typography.Text>
        {event.detail && <Typography.Text className="truncate text-xs" title={event.detail}>{event.detail}</Typography.Text>}
      </div>,
    },
    {
      title: t('connections.activity.colWhen'), dataIndex: 'occurredAt', width: 180, responsive: ['lg'],
      render: (value: string) => <Typography.Text type="secondary" className="tabular-nums">{date(value)}</Typography.Text>,
    },
    {
      title: t('connections.activity.colAccount'), key: 'account', width: 200, responsive: ['md'],
      render: (_, event) => <span className="block truncate" title={account(event)}>{account(event)}</span>,
    },
  ]
  return <SettingsSection title={t('connections.activity.description')}>
    {loadError ? <Alert type="error" showIcon title={loadError.message} /> : <Table<IntegrationEvent>
      rowKey="id" size="small" tableLayout="fixed" loading={isLoading} columns={columns}
      dataSource={events ?? []} locale={{ emptyText: t('connections.activity.empty') }}
      pagination={{ pageSize: 20, hideOnSinglePage: true }} />}
  </SettingsSection>
}
