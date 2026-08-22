import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Flex, Menu, Tag, Typography } from 'antd'
import { EditOutlined, EllipsisOutlined, InboxOutlined, PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TaskListDto } from '../../models'
import { useDeleteTaskList, useTaskLists, useUpdateTaskList } from '../../hooks/useTaskLists'
import { TaskListFormModal } from './TaskListFormModal'

interface TaskListSidebarProps {
  /** Currently selected list id, or null for "all tasks". */
  selectedId: string | null
  onSelect: (id: string | null) => void
}

export function TaskListSidebar({ selectedId, onSelect }: TaskListSidebarProps) {
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

  const items = [
    { key: '__all__', label: t('agenda.taskLists.allTasks') },
    ...activeLists.map((l) => ({
      key: l.id,
      label: (
        <Flex justify="space-between" align="center" gap="small">
          <span>
            {l.name}
            {l.isDefault && (
              <Tag style={{ marginInlineStart: 8 }}>{t('agenda.taskLists.default')}</Tag>
            )}
          </span>
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
    <div>
      <Flex justify="space-between" align="center" className="mb-2">
        <Typography.Text strong>{t('agenda.taskLists.title')}</Typography.Text>
        <Button
          size="small"
          type="text"
          icon={<PlusOutlined />}
          aria-label={t('agenda.taskLists.new')}
          onClick={openCreate}
        />
      </Flex>
      <Menu
        mode="inline"
        selectedKeys={[selectedId ?? '__all__']}
        items={items}
        onClick={({ key }) => onSelect(key === '__all__' ? null : key)}
      />

      <TaskListFormModal open={modalOpen} list={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
