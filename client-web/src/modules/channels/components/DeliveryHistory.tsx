import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Select, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { channelLabel, NOTIFICATION_STATUSES, type ChannelId, type NotificationHistoryItem, type NotificationStatus } from '../models'
import { useDeliveryHistory } from '../hooks/useChannels'

const STATUS_COLOR: Record<NotificationStatus, string> = {
  Pending: 'default',
  Sending: 'blue',
  Sent: 'green',
  Failed: 'orange',
  Dead: 'red',
}

export function DeliveryHistory() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<NotificationStatus | undefined>(undefined)
  const { data: history, isLoading, isError } = useDeliveryHistory({ status })

  function when(value: string): string {
    const date = dayjs(value)
    const time = date.format('HH:mm')
    if (date.isSame(dayjs(), 'day')) return t('notifications.today', { time })
    if (date.isSame(dayjs().subtract(1, 'day'), 'day')) return t('notifications.yesterday', { time })
    return `${date.toDate().toLocaleDateString()} ${time}`
  }

  const columns: ColumnsType<NotificationHistoryItem> = [
    {
      title: t('notifications.colWhen'),
      dataIndex: 'createdAt',
      width: 150,
      render: (value: string) => <Typography.Text type="secondary">{when(value)}</Typography.Text>,
    },
    {
      title: t('notifications.colSubject'),
      key: 'subject',
      render: (_, row) => {
        const category = row.category
          ? t(`notifications.category.${row.category}`, { defaultValue: row.category })
          : row.templateKey
        return (
          <div className="flex min-w-0 flex-col">
            <Typography.Text ellipsis={{ tooltip: row.subject }}>{row.subject || category}</Typography.Text>
            <Typography.Text type="secondary" className="text-xs">
              {category}
              {row.attemptCount > 1 && ` · ${t('notifications.attempts', { count: row.attemptCount })}`}
            </Typography.Text>
            {row.lastError && (
              <Typography.Text type="danger" className="text-xs" ellipsis={{ tooltip: row.lastError }}>
                {row.lastError}
              </Typography.Text>
            )}
          </div>
        )
      },
    },
    {
      title: t('notifications.colChannel'),
      dataIndex: 'channel',
      width: 100,
      render: (channel: ChannelId) => channelLabel(channel),
    },
    {
      title: t('notifications.colStatus'),
      dataIndex: 'status',
      width: 110,
      render: (value: NotificationStatus) => <Tag color={STATUS_COLOR[value]}>{t(`notifications.status.${value}`)}</Tag>,
    },
  ]

  return (
    <SettingsSection
      title={t('notifications.historyDesc')}
      extra={
        <Select<NotificationStatus>
          allowClear
          style={{ width: 160 }}
          placeholder={t('notifications.filterStatus')}
          value={status}
          onChange={setStatus}
          options={NOTIFICATION_STATUSES.map((s) => ({ label: t(`notifications.status.${s}`), value: s }))}
        />
      }
    >
      {isError ? (
        <div className="p-4">
          <Alert type="error" showIcon title={t('notifications.historyError')} />
        </div>
      ) : (
        <Table<NotificationHistoryItem>
          rowKey="id"
          size="small"
          tableLayout="fixed"
          loading={isLoading}
          columns={columns}
          dataSource={history ?? []}
          locale={{ emptyText: t('notifications.historyEmpty') }}
          pagination={{ pageSize: 20, hideOnSinglePage: true }}
          scroll={{ x: 620 }}
        />
      )}
    </SettingsSection>
  )
}
