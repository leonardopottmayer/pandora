import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, InputNumber, Spin, Typography, theme } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AlertSubjectType } from '../models'
import { useAlerts, useCreateAlert, useDeleteAlert } from '../hooks/useAlerts'
import { useAgendaPreferences } from '../hooks/useAgendaPreferences'

interface AlertsEditorProps {
  subjectType: AlertSubjectType
  /** Subject id — alerts can only be managed once the subject exists. */
  subjectId: string | null
}

/** Lists and edits alerts (offset in minutes) for a task or event. */
export function AlertsEditor({ subjectType, subjectId }: AlertsEditorProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const { data: preferences } = useAgendaPreferences()
  // The default arrives after the first render, so what is typed is kept apart from it.
  const [chosenOffset, setChosenOffset] = useState<number | null>(null)
  const offset = chosenOffset ?? preferences?.defaultAlertOffsetMinutes ?? -15
  const { data, isLoading, isError, error } = useAlerts(subjectType, subjectId)
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
    <div className="flex min-w-0 flex-col gap-2">
      {isError ? <Alert type="error" showIcon title={error.message} /> : isLoading ? <Spin size="small" /> : !data?.length ? (
        <Typography.Text type="secondary">{t('agenda.alerts.empty')}</Typography.Text>
      ) : <ul className="m-0 list-none p-0">
        {data.map((alert) => <li key={alert.id} className="flex items-center justify-between gap-2 py-2" style={{ borderBottom: `1px solid ${token.colorBorderSecondary}` }}>
          <span>{offsetLabel(alert.offsetMinutes)}</span>
          <Button size="small" type="text" danger icon={<DeleteOutlined />} aria-label={t('common.delete')}
            loading={deleteAlert.isPending && deleteAlert.variables === alert.id} onClick={() => handleRemove(alert.id)} />
        </li>)}
      </ul>}

      <div className="flex flex-wrap items-center gap-2">
        <InputNumber
          value={offset}
          onChange={(v) => setChosenOffset(v ?? 0)}
          aria-label={t('agenda.alerts.offset')}
        />
        <Button
          icon={<PlusOutlined />}
          loading={createAlert.isPending}
          onClick={handleAdd}
        >
          {t('agenda.alerts.add')}
        </Button>
      </div>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {t('agenda.alerts.offsetHint')}
      </Typography.Text>
    </div>
  )
}
