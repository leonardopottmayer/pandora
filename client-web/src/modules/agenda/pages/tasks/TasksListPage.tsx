import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  App,
  Button,
  Card,
  Checkbox,
  Empty,
  Flex,
  Popconfirm,
  Space,
  Typography,
} from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { TASK_DUE_BUCKETS, type TaskDto, type TaskDueBucket } from '../../models'
import { TASK_PRIORITY_META } from '../../lib/enums'
import { formatDate, formatDateTime } from '../../lib/datetime'
import { EnumTag } from '../../components/EnumTag'
import {
  useCompleteTask,
  useDeleteTask,
  useReopenTask,
  useTasks,
} from '../../hooks/useTasks'
import { TaskFormModal } from './TaskFormModal'
import { TaskListSidebar } from './TaskListSidebar'

const BUCKET_LABEL: Record<TaskDueBucket, string> = {
  Overdue: 'agenda.tasks.buckets.overdue',
  Today: 'agenda.tasks.buckets.today',
  Week: 'agenda.tasks.buckets.week',
  Later: 'agenda.tasks.buckets.later',
  None: 'agenda.tasks.buckets.none',
}

/** Assigns a task to a due bucket, client-side (matches the backend's ?due= buckets). */
function bucketOf(task: TaskDto): TaskDueBucket {
  if (!task.dueAt) return 'None'
  const due = dayjs(task.dueAt)
  const now = dayjs()
  if (due.isBefore(now.startOf('day'))) return 'Overdue'
  if (due.isBefore(now.endOf('day'))) return 'Today'
  if (due.isBefore(now.endOf('day').add(7, 'day'))) return 'Week'
  return 'Later'
}

export function TasksListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [selectedListId, setSelectedListId] = useState<string | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TaskDto | null>(null)
  const [subtaskParent, setSubtaskParent] = useState<string | null>(null)

  const { data, isLoading } = useTasks({ listId: selectedListId ?? undefined })
  const completeTask = useCompleteTask()
  const reopenTask = useReopenTask()
  const deleteTask = useDeleteTask()

  const { buckets, childrenOf } = useMemo(() => {
    const tasks = data ?? []
    const children = new Map<string, TaskDto[]>()
    for (const task of tasks) {
      if (task.parentTaskId) {
        const arr = children.get(task.parentTaskId) ?? []
        arr.push(task)
        children.set(task.parentTaskId, arr)
      }
    }
    const grouped = new Map<TaskDueBucket, TaskDto[]>()
    for (const task of tasks) {
      if (task.parentTaskId) continue // subtasks render nested under their parent
      const bucket = bucketOf(task)
      const arr = grouped.get(bucket) ?? []
      arr.push(task)
      grouped.set(bucket, arr)
    }
    return { buckets: grouped, childrenOf: children }
  }, [data])

  function openCreate() {
    setEditing(null)
    setSubtaskParent(null)
    setModalOpen(true)
  }

  function openEdit(task: TaskDto) {
    setEditing(task)
    setSubtaskParent(null)
    setModalOpen(true)
  }

  function openSubtask(parent: TaskDto) {
    setEditing(null)
    setSubtaskParent(parent.id)
    setModalOpen(true)
  }

  async function toggleDone(task: TaskDto, done: boolean) {
    try {
      if (done) {
        await completeTask.mutateAsync(task.id)
        message.success(t('agenda.tasks.completed'))
      } else {
        await reopenTask.mutateAsync(task.id)
        message.success(t('agenda.tasks.reopened'))
      }
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.saveError')))
    }
  }

  async function handleDelete(task: TaskDto) {
    try {
      await deleteTask.mutateAsync(task.id)
      message.success(t('agenda.tasks.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.deleteError')))
    }
  }

  function renderTask(task: TaskDto, nested = false) {
    const done = task.status === 'Done'
    return (
      <div key={task.id}>
        <Flex
          align="center"
          gap="small"
          style={{ paddingInlineStart: nested ? 28 : 0, paddingBlock: 6 }}
        >
          <Checkbox checked={done} onChange={(e) => toggleDone(task, e.target.checked)} />
          <Typography.Text delete={done} style={{ flex: 1 }}>
            {task.title}
          </Typography.Text>
          {task.priority !== 'None' && <EnumTag meta={TASK_PRIORITY_META[task.priority]} />}
          {task.dueAt && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {task.dueHasTime ? formatDateTime(task.dueAt) : formatDate(task.dueAt)}
            </Typography.Text>
          )}
          <Space size={4}>
            {!nested && (
              <Button size="small" type="text" onClick={() => openSubtask(task)}>
                <PlusOutlined />
              </Button>
            )}
            <Button size="small" type="text" onClick={() => openEdit(task)}>
              {t('common.edit')}
            </Button>
            <Popconfirm
              title={t('agenda.tasks.deleteConfirm')}
              okText={t('common.delete')}
              cancelText={t('common.cancel')}
              onConfirm={() => handleDelete(task)}
            >
              <Button size="small" type="text" danger>
                {t('common.delete')}
              </Button>
            </Popconfirm>
          </Space>
        </Flex>
        {!nested && (childrenOf.get(task.id) ?? []).map((child) => renderTask(child, true))}
      </div>
    )
  }

  const hasTasks = (data ?? []).length > 0

  return (
    <Flex gap="large" align="flex-start" wrap>
      <Card style={{ width: 260, flexShrink: 0 }}>
        <TaskListSidebar selectedId={selectedListId} onSelect={setSelectedListId} />
      </Card>

      <Card style={{ flex: 1, minWidth: 320 }} loading={isLoading}>
        <Flex justify="space-between" align="center" className="mb-4">
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('agenda.tasks.title')}
          </Typography.Title>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('agenda.tasks.new')}
          </Button>
        </Flex>

        {!hasTasks && !isLoading && <Empty description={t('agenda.tasks.empty')} />}

        {TASK_DUE_BUCKETS.filter((b) => (buckets.get(b) ?? []).length > 0).map((bucket) => (
          <div key={bucket} className="mb-4">
            <Typography.Text type="secondary" strong>
              {t(BUCKET_LABEL[bucket])}
            </Typography.Text>
            {(buckets.get(bucket) ?? []).map((task) => renderTask(task))}
          </div>
        ))}
      </Card>

      <TaskFormModal
        open={modalOpen}
        task={editing}
        defaultListId={selectedListId ?? undefined}
        parentTaskId={subtaskParent}
        onClose={() => setModalOpen(false)}
      />
    </Flex>
  )
}
