import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Checkbox, Dropdown, Flex, Typography } from 'antd'
import { EditOutlined, EllipsisOutlined, InboxOutlined, PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CalendarDto } from '../../models'
import { useCalendars, useDeleteCalendar, useUpdateCalendar } from '../../hooks/useCalendars'
import { CalendarFormModal } from './CalendarFormModal'

export function CalendarSidebar() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data } = useCalendars()
  const updateMutation = useUpdateCalendar()
  const deleteMutation = useDeleteCalendar()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<CalendarDto | null>(null)

  const calendars = (data ?? []).filter((c) => !c.archivedAt)

  async function toggleVisible(calendar: CalendarDto, isVisible: boolean) {
    try {
      await updateMutation.mutateAsync({ id: calendar.id, body: { isVisible } })
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.calendars.saveError')))
    }
  }

  async function handleArchive(calendar: CalendarDto) {
    try {
      await updateMutation.mutateAsync({ id: calendar.id, body: { archive: true } })
      message.success(t('agenda.calendars.updated'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.calendars.saveError')))
    }
  }

  async function handleDelete(calendar: CalendarDto) {
    try {
      await deleteMutation.mutateAsync(calendar.id)
      message.success(t('agenda.calendars.deleted'))
    } catch (err) {
      // A calendar with live events is refused (Agenda.CalendarNotEmpty) — archive instead.
      message.error(toErrorMessage(err, t('agenda.calendars.notEmptyHint')))
    }
  }

  return (
    <div>
      <Flex justify="space-between" align="center" className="mb-2">
        <Typography.Text strong>{t('agenda.calendars.title')}</Typography.Text>
        <Button
          size="small"
          type="text"
          icon={<PlusOutlined />}
          aria-label={t('agenda.calendars.new')}
          onClick={() => {
            setEditing(null)
            setModalOpen(true)
          }}
        />
      </Flex>

      <Flex vertical gap="small">
        {calendars.map((calendar) => (
          <Flex key={calendar.id} align="center" justify="space-between" gap="small">
            <Checkbox
              checked={calendar.isVisible}
              onChange={(e) => toggleVisible(calendar, e.target.checked)}
            >
              <Flex align="center" gap={6} component="span">
                <span
                  style={{
                    width: 10,
                    height: 10,
                    borderRadius: 2,
                    background: calendar.color ?? '#1677ff',
                    display: 'inline-block',
                  }}
                />
                {calendar.name}
              </Flex>
            </Checkbox>
            <Dropdown
              trigger={['click']}
              menu={{
                items: [
                  {
                    key: 'edit',
                    icon: <EditOutlined />,
                    label: t('common.edit'),
                    onClick: () => {
                      setEditing(calendar)
                      setModalOpen(true)
                    },
                  },
                  {
                    key: 'archive',
                    icon: <InboxOutlined />,
                    label: t('agenda.calendars.archive'),
                    onClick: () => handleArchive(calendar),
                  },
                  {
                    key: 'delete',
                    danger: true,
                    label: t('common.delete'),
                    onClick: () => handleDelete(calendar),
                  },
                ],
              }}
            >
              <Button
                size="small"
                type="text"
                icon={<EllipsisOutlined />}
                aria-label={t('common.actions')}
              />
            </Dropdown>
          </Flex>
        ))}
      </Flex>

      <CalendarFormModal open={modalOpen} calendar={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
