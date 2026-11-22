import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Flex, Menu, Tag, Typography } from 'antd'
import { EditOutlined, EllipsisOutlined, InboxOutlined, PlusOutlined } from '@ant-design/icons'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TaskListDto } from '../../models'
import { useDeleteTaskList, useTaskLists, useUpdateTaskList } from '../../hooks/useTaskLists'
import { TaskListFormModal } from './TaskListFormModal'

interface TaskListSidebarProps {
  /** Currently selected list id, or null for "all tasks". */
  selectedId: string | null
  onSelect: (id: string | null) => void
  /** Open tasks per list id. */
  openCounts: Map<string, number>
}

function Count({ value }: { value: number | undefined }) {
  return value ? (
    <Typography.Text type="secondary" className="text-xs tabular-nums">
      {value}
    </Typography.Text>
  ) : null
}

export function TaskListSidebar({ selectedId, onSelect, openCounts }: TaskListSidebarProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data } = useTaskLists()
  const deleteMutation = useDeleteTaskList()
  const updateMutation = useUpdateTaskList()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TaskListDto | null>(null)

  function openCreate() {
    setEditing(null)
    setModalOpen(true)
  }

  function openEdit(list: TaskListDto) {
    setEditing(list)
    setModalOpen(true)
  }

  async function handleDelete(list: TaskListDto) {
    try {
      await deleteMutation.mutateAsync(list.id)
      message.success(t('agenda.taskLists.deleted'))
      if (selectedId === list.id) onSelect(null)
    } catch (err) {
      // A non-empty list is refused (Agenda.TaskListNotEmpty) — suggest archiving.
      message.error(toErrorMessage(err, t('agenda.taskLists.notEmptyHint')))
    }
  }

  async function handleArchive(list: TaskListDto) {
    try {
      await updateMutation.mutateAsync({ id: list.id, body: { archive: true } })
      message.success(t('agenda.taskLists.updated'))
      if (selectedId === list.id) onSelect(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.taskLists.saveError')))
    }
  }

  const activeLists = (data ?? []).filter((l) => !l.archivedAt)

  const allCount = [...openCounts.values()].reduce((sum, n) => sum + n, 0)

  const items = [
    {
      key: '__all__',
      label: (
        <Flex justify="space-between" align="center" gap="small">
          <span>{t('agenda.taskLists.allTasks')}</span>
          <span className="pe-1">
            <Count value={allCount} />
          </span>
        </Flex>
      ),
    },
    ...activeLists.map((l) => ({
      key: l.id,
      label: (
        <Flex justify="space-between" align="center" gap="small">
          <span className="truncate">
            {l.name}
            {l.isDefault && (
              <Tag style={{ marginInlineStart: 8 }}>{t('agenda.taskLists.default')}</Tag>
            )}
          </span>
          <Count value={openCounts.get(l.id)} />
          <Dropdown
            trigger={['click']}
            menu={{
              items: [
                {
                  key: 'edit',
                  icon: <EditOutlined />,
                  label: t('common.edit'),
                  onClick: () => openEdit(l),
                },
                {
                  key: 'archive',
                  icon: <InboxOutlined />,
                  label: t('agenda.taskLists.archive'),
                  onClick: () => handleArchive(l),
                },
                {
                  key: 'delete',
                  danger: true,
                  label: t('common.delete'),
                  onClick: () => handleDelete(l),
                },
              ],
            }}
          >
            <Button
              size="small"
              type="text"
              icon={<EllipsisOutlined />}
              aria-label={t('common.actions')}
              onClick={(e) => e.stopPropagation()}
            />
          </Dropdown>
        </Flex>
      ),
    })),
  ]

  return (
    <SettingsSection
      title={t('agenda.taskLists.title')}
      extra={
        <Button
          size="small"
          type="text"
          icon={<PlusOutlined />}
          title={t('agenda.taskLists.new')}
          aria-label={t('agenda.taskLists.new')}
          onClick={openCreate}
        />
      }
      bodyClassName="py-1"
    >
      <Menu
        mode="inline"
        selectedKeys={[selectedId ?? '__all__']}
        items={items}
        style={{ borderInlineEnd: 0, background: 'transparent' }}
        onClick={({ key }) => onSelect(key === '__all__' ? null : key)}
      />

      <TaskListFormModal open={modalOpen} list={editing} onClose={() => setModalOpen(false)} />
    </SettingsSection>
  )
}
