import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Flex, Popconfirm, Space, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { ReminderDto } from '../../models'
import { REMINDER_STATUS_META } from '../../lib/enums'
import { formatDateTime } from '../../lib/datetime'
import { EnumTag } from '../../components/EnumTag'
import { SnoozeModal } from '../../components/SnoozeModal'
import { useAcknowledgeReminder, useDeleteReminder, useReminders } from '../../hooks/useReminders'
import { ReminderFormModal } from './ReminderFormModal'

export function RemindersListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data, isLoading } = useReminders()
  const acknowledge = useAcknowledgeReminder()
  const deleteMutation = useDeleteReminder()
  const [modalOpen, setModalOpen] = useState(false)
  const [snoozeId, setSnoozeId] = useState<string | null>(null)

  async function handleAcknowledge(reminder: ReminderDto) {
    try {
      await acknowledge.mutateAsync(reminder.id)
      message.success(t('agenda.reminders.acknowledged'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  async function handleDelete(reminder: ReminderDto) {
    try {
      await deleteMutation.mutateAsync(reminder.id)
      message.success(t('agenda.reminders.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.deleteError')))
    }
  }

  const columns: ColumnsType<ReminderDto> = [
    { title: t('agenda.reminders.titleLabel'), dataIndex: 'title' },
    {
      title: t('agenda.reminders.remindAt'),
      dataIndex: 'remindAt',
      render: (value: string) => formatDateTime(value),
    },
    {
      title: t('agenda.reminders.status'),
      dataIndex: 'status',
      render: (_, reminder) => <EnumTag meta={REMINDER_STATUS_META[reminder.status]} />,
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, reminder) => (
        <Space>
          <Button size="small" onClick={() => setSnoozeId(reminder.id)}>
            {t('agenda.reminders.snooze')}
          </Button>
          <Button size="small" onClick={() => handleAcknowledge(reminder)}>
            {t('agenda.reminders.acknowledge')}
          </Button>
          <Popconfirm
            title={t('agenda.reminders.deleteConfirm')}
            okText={t('common.delete')}
            cancelText={t('common.cancel')}
            onConfirm={() => handleDelete(reminder)}
          >
            <Button size="small" danger>
              {t('agenda.reminders.cancel')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Card>
      <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('agenda.reminders.title')}
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>
          {t('agenda.reminders.new')}
        </Button>
      </Flex>

      <Table
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        pagination={false}
        size="middle"
        locale={{ emptyText: t('agenda.reminders.empty') }}
      />

      <ReminderFormModal open={modalOpen} onClose={() => setModalOpen(false)} />
      <SnoozeModal open={!!snoozeId} reminderId={snoozeId} onClose={() => setSnoozeId(null)} />
    </Card>
  )
}
