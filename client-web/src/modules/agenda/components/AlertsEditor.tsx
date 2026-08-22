import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, InputNumber, List, Space, Typography } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AlertSubjectType } from '../models'
import { useAlerts, useCreateAlert, useDeleteAlert } from '../hooks/useAlerts'

interface AlertsEditorProps {
  subjectType: AlertSubjectType
  /** Subject id — alerts can only be managed once the subject exists. */
  subjectId: string | null
}

/** Lists and edits alerts (offset in minutes) for a task or event. */
export function AlertsEditor({ subjectType, subjectId }: AlertsEditorProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [offset, setOffset] = useState<number>(-15)
  const { data } = useAlerts(subjectType, subjectId)
  const createAlert = useCreateAlert(subjectType, subjectId)
  const deleteAlert = useDeleteAlert(subjectType, subjectId)

  async function handleAdd() {
    try {
      await createAlert.mutateAsync({ offsetMinutes: offset })
      message.success(t('agenda.alerts.added'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.alerts.saveError')))
    }
  }

  async function handleRemove(id: string) {
    try {
      await deleteAlert.mutateAsync(id)
      message.success(t('agenda.alerts.removed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.alerts.saveError')))
    }
  }

  function offsetLabel(minutes: number): string {
    if (minutes === 0) return t('agenda.alerts.atTime')
    return `${minutes} min`
  }

  return (
    <Space direction="vertical" style={{ width: '100%' }} size="small">
      <List
        size="small"
        dataSource={data ?? []}
        locale={{ emptyText: t('agenda.alerts.empty') }}
        renderItem={(alert) => (
          <List.Item
            actions={[
              <Button
                key="remove"
                size="small"
                type="text"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('common.delete')}
                onClick={() => handleRemove(alert.id)}
              />,
            ]}
          >
            {offsetLabel(alert.offsetMinutes)}
          </List.Item>
        )}
      />

      <Space>
        <InputNumber
          value={offset}
          onChange={(v) => setOffset(v ?? 0)}
          aria-label={t('agenda.alerts.offset')}
        />
        <Button
          icon={<PlusOutlined />}
          loading={createAlert.isPending}
          onClick={handleAdd}
        >
          {t('agenda.alerts.add')}
        </Button>
      </Space>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {t('agenda.alerts.offsetHint')}
      </Typography.Text>
    </Space>
  )
}
