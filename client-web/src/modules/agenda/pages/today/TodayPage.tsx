import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Empty, List, Space, Typography } from 'antd'
import { CheckOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TodayItemDto } from '../../models'
import { TODAY_KIND_META } from '../../lib/enums'
import { formatTime } from '../../lib/datetime'
import { EnumTag } from '../../components/EnumTag'
import { SnoozeModal } from '../../components/SnoozeModal'
import { useToday } from '../../hooks/useToday'
import { useCompleteTask } from '../../hooks/useTasks'
import { useAcknowledgeReminder } from '../../hooks/useReminders'

export function TodayPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data, isLoading } = useToday()
  const completeTask = useCompleteTask()
  const acknowledge = useAcknowledgeReminder()
  const [snoozeId, setSnoozeId] = useState<string | null>(null)

  async function handleComplete(item: TodayItemDto) {
    try {
      await completeTask.mutateAsync(item.id)
      message.success(t('agenda.tasks.completed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.saveError')))
    }
  }

  async function handleAcknowledge(item: TodayItemDto) {
    try {
      await acknowledge.mutateAsync(item.id)
      message.success(t('agenda.reminders.acknowledged'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  function renderActions(item: TodayItemDto) {
    if (item.kind === 'task' && item.status !== 'Done') {
      return [
        <Button
          key="complete"
          size="small"
          icon={<CheckOutlined />}
          onClick={() => handleComplete(item)}
        >
          {t('agenda.tasks.complete')}
        </Button>,
      ]
    }
    if (item.kind === 'reminder') {
      return [
        <Button key="ack" size="small" onClick={() => handleAcknowledge(item)}>
          {t('agenda.reminders.acknowledge')}
        </Button>,
        <Button key="snooze" size="small" onClick={() => setSnoozeId(item.id)}>
          {t('agenda.reminders.snooze')}
        </Button>,
      ]
    }
    return []
  }

  return (
    <Card>
      <Typography.Title level={4} style={{ margin: 0 }} className="mb-4">
        {t('nav.agendaToday')}
      </Typography.Title>

      <List
        loading={isLoading}
        dataSource={data ?? []}
        locale={{ emptyText: <Empty description={t('agenda.today.empty')} /> }}
        renderItem={(item) => (
          <List.Item actions={renderActions(item)}>
            <List.Item.Meta
              avatar={
                <Space direction="vertical" size={0} align="center">
                  <Typography.Text strong>
                    {item.isAllDay ? t('agenda.today.allDay') : formatTime(item.at)}
                  </Typography.Text>
                  <EnumTag meta={TODAY_KIND_META[item.kind]} />
                </Space>
              }
              title={item.title}
              description={item.notes ?? undefined}
            />
          </List.Item>
        )}
      />

      <SnoozeModal open={!!snoozeId} reminderId={snoozeId} onClose={() => setSnoozeId(null)} />
    </Card>
  )
}
