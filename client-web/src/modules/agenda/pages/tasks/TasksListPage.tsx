import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Checkbox, Dropdown, Empty, Input, Skeleton, Typography, theme } from 'antd'
import {
  AlignLeftOutlined,
  CalendarOutlined,
  CheckSquareOutlined,
  EllipsisOutlined,
  FlagFilled,
  PlusOutlined,
  RetweetOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import { TASK_DUE_BUCKETS, type TaskDto, type TaskDueBucket, type TaskPriority } from '../../models'
import { TASK_PRIORITY_META } from '../../lib/enums'
import { daysFromToday, formatDue } from '../../lib/datetime'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { useCompleteTask, useCreateTask, useDeleteTask, useReopenTask, useTasks } from '../../hooks/useTasks'
import { useTaskLists } from '../../hooks/useTaskLists'
import { TaskFormModal } from './TaskFormModal'
import { TaskListSidebar } from './TaskListSidebar'

const BUCKET_LABEL: Record<TaskDueBucket, string> = {
  Overdue: 'agenda.tasks.buckets.overdue',
  Today: 'agenda.tasks.buckets.today',
  Week: 'agenda.tasks.buckets.week',
  Later: 'agenda.tasks.buckets.later',
  None: 'agenda.tasks.buckets.none',
}

/** Assigns an open task to a due bucket by calendar day in the account's zone (the backend's ?due= uses UTC). */
function bucketOf(task: TaskDto, timeZone: string): TaskDueBucket {
  if (!task.dueAt) return 'None'
  const days = daysFromToday(task.dueAt, timeZone)
  if (days < 0) return 'Overdue'
  if (days === 0) return 'Today'
  if (days <= 7) return 'Week'
  return 'Later'
}

export function TasksListPage() {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const [selectedListId, setSelectedListId] = useState<string | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TaskDto | null>(null)
  const [subtaskParent, setSubtaskParent] = useState<string | null>(null)
  const [quickTitle, setQuickTitle] = useState('')

  // Every list's tasks at once: the list rail counts them, and the selected list is a filter.
  const { data, isLoading } = useTasks()
  const { data: lists } = useTaskLists()
  const createTask = useCreateTask()
  const completeTask = useCompleteTask()
  const reopenTask = useReopenTask()
  const deleteTask = useDeleteTask()

  const activeLists = (lists ?? []).filter((l) => !l.archivedAt)
  const listName = new Map(activeLists.map((l) => [l.id, l.name]))
  // Quick add goes to the list on screen, or to the default one under "All tasks".
  const quickAddListId = selectedListId ?? (activeLists.find((l) => l.isDefault) ?? activeLists[0])?.id

  const PRIORITY_COLOR: Record<TaskPriority, string | undefined> = {
    None: undefined,
    Low: token.colorInfo,
    Medium: token.colorWarning,
    High: token.colorError,
  }

  const { buckets, done, childrenOf, openCounts } = useMemo(() => {
    const tasks = data ?? []
    const openCounts = new Map<string, number>()
    for (const task of tasks) {
      if (task.status !== 'Done') openCounts.set(task.listId, (openCounts.get(task.listId) ?? 0) + 1)
    }
    const visible = tasks.filter((task) => !selectedListId || task.listId === selectedListId)
    const childrenOf = new Map<string, TaskDto[]>()
    const buckets = new Map<TaskDueBucket, TaskDto[]>()
    const done: TaskDto[] = []
    for (const task of visible) {
      if (task.parentTaskId) {
        childrenOf.set(task.parentTaskId, [...(childrenOf.get(task.parentTaskId) ?? []), task])
      } else if (task.status === 'Done') {
        done.push(task)
      } else {
        const bucket = bucketOf(task, timeZone)
        buckets.set(bucket, [...(buckets.get(bucket) ?? []), task])
      }
    }
    done.sort((a, b) => (b.completedAt ?? '').localeCompare(a.completedAt ?? ''))
    return { buckets, done, childrenOf, openCounts }
  }, [data, selectedListId, timeZone])

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

  async function handleQuickAdd() {
    const title = quickTitle.trim()
    if (!title || !quickAddListId || createTask.isPending) return
    try {
      await createTask.mutateAsync({ listId: quickAddListId, title, timeZone })
      setQuickTitle('')
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.saveError')))
    }
  }

  // The task moving to (or out of) "Completed" is the feedback; only failures speak up.
  async function toggleDone(task: TaskDto, isDone: boolean) {
    try {
      await (isDone ? completeTask : reopenTask).mutateAsync(task.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.saveError')))
    }
  }

  function confirmDelete(task: TaskDto) {
    modal.confirm({
      title: t('agenda.tasks.deleteConfirm'),
      content: task.title,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await deleteTask.mutateAsync(task.id)
        } catch (err) {
          message.error(toErrorMessage(err, t('agenda.tasks.deleteError')))
        }
      },
    })
  }

  function renderTask(task: TaskDto, nested: boolean, first: boolean) {
    const isDone = task.status === 'Done'
    const overdue = !isDone && !!task.dueAt && daysFromToday(task.dueAt, timeZone) < 0
    const children = nested ? [] : (childrenOf.get(task.id) ?? [])
    const doneChildren = children.filter((c) => c.status === 'Done').length
    const priorityColor = PRIORITY_COLOR[task.priority]

    const meta = [
      task.dueAt && (
        <span key="due" style={overdue ? { color: token.colorError } : undefined}>
          <CalendarOutlined /> {formatDue(task.dueAt, task.dueHasTime, timeZone, i18n.language)}
        </span>
      ),
      task.rrule && (
        <span key="rrule" title={t('agenda.tasks.recurring')}>
          <RetweetOutlined aria-label={t('agenda.tasks.recurring')} />
        </span>
      ),
      task.notes && (
        <span key="notes" title={t('agenda.tasks.hasNotes')}>
          <AlignLeftOutlined aria-label={t('agenda.tasks.hasNotes')} />
        </span>
      ),
      children.length > 0 && (
        <span key="subtasks" title={t('agenda.tasks.subtasks')}>
          <CheckSquareOutlined /> {doneChildren}/{children.length}
        </span>
      ),
      !selectedListId && !nested && activeLists.length > 1 && listName.has(task.listId) && (
        <span key="list">
          <UnorderedListOutlined /> {listName.get(task.listId)}
        </span>
      ),
    ].filter(Boolean)

    return (
      <div
        key={task.id}
        className="flex items-start gap-3 py-2.5 pe-2"
        style={{
          paddingInlineStart: nested ? 48 : 16,
          borderTop: first ? undefined : `1px solid ${token.colorBorderSecondary}`,
        }}
      >
        <Checkbox
          className="mt-0.5"
          checked={isDone}
          aria-label={task.title}
          onChange={(e) => toggleDone(task, e.target.checked)}
        />
        <button
          type="button"
          className="flex min-w-0 flex-1 cursor-pointer flex-col items-start gap-0.5 text-left"
          onClick={() => openEdit(task)}
        >
          <span className="flex max-w-full items-center gap-1.5">
            {priorityColor && (
              <FlagFilled
                style={{ color: priorityColor }}
                title={t(TASK_PRIORITY_META[task.priority].labelKey)}
                aria-label={t(TASK_PRIORITY_META[task.priority].labelKey)}
              />
            )}
            <Typography.Text delete={isDone} type={isDone ? 'secondary' : undefined} className="break-words">
              {task.title}
            </Typography.Text>
          </span>
          {meta.length > 0 && (
            <span
              className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs"
              style={{ color: token.colorTextTertiary }}
            >
              {meta}
            </span>
          )}
        </button>
        <Dropdown
          trigger={['click']}
          menu={{
            items: [
              ...(nested
                ? []
                : [{ key: 'subtask', label: t('agenda.tasks.addSubtask'), onClick: () => openSubtask(task) }]),
              { key: 'edit', label: t('common.edit'), onClick: () => openEdit(task) },
              { type: 'divider' as const },
              { key: 'delete', danger: true, label: t('common.delete'), onClick: () => confirmDelete(task) },
            ],
          }}
        >
          <Button
            size="small"
            type="text"
            icon={<EllipsisOutlined />}
            aria-label={t('agenda.tasks.moreActions', { title: task.title })}
          />
        </Dropdown>
      </div>
    )
  }

  /** A parent and its subtasks, as one run of rows. */
  function renderRows(tasks: TaskDto[]) {
    return tasks.flatMap((task, index) => [
      renderTask(task, false, index === 0),
      ...(childrenOf.get(task.id) ?? []).map((child) => renderTask(child, true, false)),
    ])
  }

  const groups = TASK_DUE_BUCKETS.filter((b) => (buckets.get(b) ?? []).length > 0)

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('agenda.tasks.title')} description={t('agenda.tasks.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('agenda.tasks.new')}
        </Button>
      </div>

      <div className="grid items-start gap-6 md:grid-cols-[240px_minmax(0,1fr)]">
        <TaskListSidebar selectedId={selectedListId} onSelect={setSelectedListId} openCounts={openCounts} />

        <div className="flex min-w-0 flex-col gap-6">
          <Input
            size="large"
            prefix={<PlusOutlined style={{ color: token.colorTextTertiary }} />}
            placeholder={t('agenda.tasks.quickAdd')}
            aria-label={t('agenda.tasks.quickAdd')}
            value={quickTitle}
            maxLength={200}
            disabled={!quickAddListId}
            onChange={(e) => setQuickTitle(e.target.value)}
            onPressEnter={handleQuickAdd}
          />

          {isLoading ? (
            <Skeleton active />
          ) : (
            <>
              {groups.length === 0 && (
                <Empty
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                  description={t(done.length > 0 ? 'agenda.tasks.allDone' : 'agenda.tasks.empty')}
                />
              )}

              {groups.map((bucket) => {
                const tasks = buckets.get(bucket) ?? []
                return (
                  <SettingsSection
                    key={bucket}
                    title={
                      <span style={bucket === 'Overdue' ? { color: token.colorError } : undefined}>
                        {t(BUCKET_LABEL[bucket])} · {tasks.length}
                      </span>
                    }
                  >
                    {renderRows(tasks)}
                  </SettingsSection>
                )
              })}

              {done.length > 0 && (
                <details>
                  <summary
                    className="w-fit cursor-pointer select-none px-0.5 text-xs font-semibold uppercase tracking-wider"
                    style={{ color: token.colorTextSecondary }}
                  >
                    {t('agenda.tasks.done')} · {done.length}
                  </summary>
                  <div className="pt-2.5">
                    <SettingsSection>{renderRows(done)}</SettingsSection>
                  </div>
                </details>
              )}
            </>
          )}
        </div>
      </div>

      <TaskFormModal
        open={modalOpen}
        task={editing}
        defaultListId={subtaskParent ? data?.find((x) => x.id === subtaskParent)?.listId : quickAddListId}
        parentTaskId={subtaskParent}
        onClose={() => setModalOpen(false)}
      />
    </div>
  )
}
