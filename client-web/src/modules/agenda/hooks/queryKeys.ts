import type { AlertSubjectType, EventFilters, TaskFilters } from '../models'

// Central query key factory for the agenda module. Centralising prevents
// mismatches between callers that query and those that invalidate in the cache.
export const agendaKeys = {
  all: ['agenda'] as const,

  today: () => [...agendaKeys.all, 'today'] as const,

  calendars: () => [...agendaKeys.all, 'calendars'] as const,
  calendarList: () => [...agendaKeys.calendars(), 'list'] as const,

  events: () => [...agendaKeys.all, 'events'] as const,
  eventList: (filters: EventFilters) => [...agendaKeys.events(), 'list', filters] as const,
  event: (id: string) => [...agendaKeys.events(), 'detail', id] as const,

  tasks: () => [...agendaKeys.all, 'tasks'] as const,
  taskList: (filters: TaskFilters = {}) => [...agendaKeys.tasks(), 'list', filters] as const,

  taskLists: () => [...agendaKeys.all, 'task-lists'] as const,
  taskListList: () => [...agendaKeys.taskLists(), 'list'] as const,

  reminders: () => [...agendaKeys.all, 'reminders'] as const,
  reminderList: () => [...agendaKeys.reminders(), 'list'] as const,

  alerts: () => [...agendaKeys.all, 'alerts'] as const,
  alertList: (subjectType: AlertSubjectType, subjectId: string) =>
    [...agendaKeys.alerts(), subjectType, subjectId] as const,
}
