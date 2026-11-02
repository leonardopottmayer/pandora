import { useTranslation } from 'react-i18next'
import { Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { providerLabel, type IntegrationEvent, type IntegrationEventType } from '../models'
import { useAccounts, useIntegrationEvents } from '../hooks/useIntegrations'

const EVENT_TAG_COLOR: Record<IntegrationEventType, string> = {
  connected: 'green',
  reconnected: 'green',
  'refresh-failed': 'orange',
  expired: 'orange',
  revoked: 'red',
  disconnected: 'default',
}

export function IntegrationActivity() {
  const { t } = useTranslation()
  const { data: events, isLoading } = useIntegrationEvents()
  const { data: accounts } = useAccounts()

  const columns: ColumnsType<IntegrationEvent> = [
    {
      title: t('connections.activity.colWhen'),
      dataIndex: 'occurredAt',
      width: 180,
      render: (value: string) => <Typography.Text type="secondary">{new Date(value).toLocaleString()}</Typography.Text>,
    },
    {
      title: t('connections.activity.colEvent'),
      dataIndex: 'eventType',
      width: 150,
      render: (type: IntegrationEventType) => (
        <Tag color={EVENT_TAG_COLOR[type]}>{t(`connections.activity.event.${type}`)}</Tag>
      ),
    },
    {
      title: t('connections.activity.colAccount'),
      key: 'account',
      width: 200,
      render: (_, event) => {
        // An account that was disconnected is gone, so only its provider is left to show.
        const label = accounts?.find((a) => a.id === event.externalAccountId)?.label
        const provider = providerLabel(event.provider)
        return label ? `${provider} · ${label}` : provider
      },
    },
    {
      title: t('connections.activity.colDetail'),
      dataIndex: 'detail',
      ellipsis: { showTitle: true },
      render: (detail: string | null) => detail ?? <Typography.Text type="secondary">—</Typography.Text>,
    },
  ]

  return (
    <SettingsSection title={t('connections.activity.description')}>
      <Table<IntegrationEvent>
        rowKey="id"
        size="small"
        tableLayout="fixed"
        loading={isLoading}
        columns={columns}
        dataSource={events ?? []}
        locale={{ emptyText: t('connections.activity.empty') }}
        pagination={{ pageSize: 20, hideOnSinglePage: true }}
        scroll={{ x: 680 }}
      />
    </SettingsSection>
  )
}
