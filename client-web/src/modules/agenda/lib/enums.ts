import type {
  CalendarOrigin,
  EventStatus,
  ReminderStatus,
  TaskItemStatus,
  TaskPriority,
  TodayItemKind,
} from '../models'

// Each enum maps to an i18n key (`agenda.enums.*`) and an antd `Tag` colour.
// Keys are resolved with `t(...)` in the UI; here we only store the structure.

export interface EnumMeta {
  labelKey: string
  color?: string
}

export const TASK_STATUS_META: Record<TaskItemStatus, EnumMeta> = {
  Todo: { labelKey: 'agenda.enums.taskStatus.todo', color: 'default' },
  InProgress: { labelKey: 'agenda.enums.taskStatus.inProgress', color: 'blue' },
  Done: { labelKey: 'agenda.enums.taskStatus.done', color: 'green' },
  Cancelled: { labelKey: 'agenda.enums.taskStatus.cancelled', color: 'default' },
}

export const TASK_PRIORITY_META: Record<TaskPriority, EnumMeta> = {
  None: { labelKey: 'agenda.enums.taskPriority.none' },
  Low: { labelKey: 'agenda.enums.taskPriority.low', color: 'cyan' },
  Medium: { labelKey: 'agenda.enums.taskPriority.medium', color: 'gold' },
  High: { labelKey: 'agenda.enums.taskPriority.high', color: 'red' },
}

export const REMINDER_STATUS_META: Record<ReminderStatus, EnumMeta> = {
  Scheduled: { labelKey: 'agenda.enums.reminderStatus.scheduled', color: 'blue' },
  Notified: { labelKey: 'agenda.enums.reminderStatus.notified', color: 'gold' },
  Acknowledged: { labelKey: 'agenda.enums.reminderStatus.acknowledged', color: 'green' },
  Snoozed: { labelKey: 'agenda.enums.reminderStatus.snoozed', color: 'purple' },
  Cancelled: { labelKey: 'agenda.enums.reminderStatus.cancelled', color: 'default' },
}

export const EVENT_STATUS_META: Record<EventStatus, EnumMeta> = {
  Confirmed: { labelKey: 'agenda.enums.eventStatus.confirmed', color: 'green' },
  Tentative: { labelKey: 'agenda.enums.eventStatus.tentative', color: 'gold' },
  Cancelled: { labelKey: 'agenda.enums.eventStatus.cancelled', color: 'default' },
}

export const CALENDAR_ORIGIN_META: Record<CalendarOrigin, EnumMeta> = {
  Local: { labelKey: 'agenda.enums.calendarOrigin.local' },
  External: { labelKey: 'agenda.enums.calendarOrigin.external', color: 'blue' },
}

export const TODAY_KIND_META: Record<TodayItemKind, EnumMeta> = {
  event: { labelKey: 'agenda.enums.todayKind.event', color: 'blue' },
  task: { labelKey: 'agenda.enums.todayKind.task', color: 'green' },
  reminder: { labelKey: 'agenda.enums.todayKind.reminder', color: 'purple' },
}
