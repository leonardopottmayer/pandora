// Agenda module types — mirror the backend DTOs/Requests
// (Pottmayer.Pandora.Modules.Agenda.Application.Dtos / .Presentation.Requests).
// Backend JSON is camelCase; instants arrive as ISO-8601 with offset.
// NOTE: unlike finances, Agenda serialises enums in PascalCase (`.ToString()`),
// except `TodayItemDto.kind`, which is lowercase.

// ---------------------------------------------------------------------------
// Enums (string unions + arrays for UI iteration)
// ---------------------------------------------------------------------------

export const TASK_STATUSES = ['Todo', 'InProgress', 'Done', 'Cancelled'] as const
export type TaskItemStatus = (typeof TASK_STATUSES)[number]

export const TASK_PRIORITIES = ['None', 'Low', 'Medium', 'High'] as const
export type TaskPriority = (typeof TASK_PRIORITIES)[number]

export const REMINDER_STATUSES = [
  'Scheduled',
  'Notified',
  'Acknowledged',
  'Snoozed',
  'Cancelled',
] as const
export type ReminderStatus = (typeof REMINDER_STATUSES)[number]

export const EVENT_STATUSES = ['Confirmed', 'Tentative', 'Cancelled'] as const
export type EventStatus = (typeof EVENT_STATUSES)[number]

export const CALENDAR_ORIGINS = ['Local', 'External'] as const
export type CalendarOrigin = (typeof CALENDAR_ORIGINS)[number]

/** Query bucket for GET /tasks?due= */
export const TASK_DUE_BUCKETS = ['Overdue', 'Today', 'Week', 'Later', 'None'] as const
export type TaskDueBucket = (typeof TASK_DUE_BUCKETS)[number]

/** `TodayItemDto.kind` — lowercase (backend exception). */
export type TodayItemKind = 'event' | 'task' | 'reminder'

/** Edit/delete scope for recurring events. */
export type EventScope = 'this' | 'this-and-future' | 'all'

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

export interface CalendarDto {
  id: string
  name: string
  color?: string | null
  isDefault: boolean
  isVisible: boolean
  timeZone: string
  origin: CalendarOrigin
  archivedAt?: string | null
  /** Fed by a Google calendar Pandora cannot write to: its events are edited in Google. */
  isReadOnly: boolean
}

export interface EventDto {
  id: string
  calendarId: string
  title: string
  description?: string | null
  location?: string | null
  url?: string | null
  startsAt: string
  endsAt: string
  isAllDay: boolean
  timeZone: string
  rrule?: string | null
  recurrenceEndsAt?: string | null
  status: EventStatus
}

export interface EventOccurrenceDto {
  eventId: string
  calendarId: string
  originalStartsAt: string
  startsAt: string
  endsAt: string
  isAllDay: boolean
  title: string
  description?: string | null
  location?: string | null
  url?: string | null
  status: EventStatus
}

export interface TodayItemDto {
  kind: TodayItemKind
  id: string
  title: string
  notes?: string | null
  at: string
  endsAt?: string | null
  isAllDay: boolean
  calendarId?: string | null
  status?: string | null
}

export interface TaskDto {
  id: string
  listId: string
  parentTaskId?: string | null
  title: string
  notes?: string | null
  dueAt?: string | null
  dueHasTime: boolean
  priority: TaskPriority
  status: TaskItemStatus
  completedAt?: string | null
  timeZone: string
  rrule?: string | null
  position: number
}

export interface TaskListDto {
  id: string
  name: string
  isDefault: boolean
  position: number
  archivedAt?: string | null
}

export interface ReminderDto {
  id: string
  title: string
  notes?: string | null
  remindAt: string
  timeZone: string
  rrule?: string | null
  recurrenceEndsAt?: string | null
  status: ReminderStatus
  snoozedUntil?: string | null
  acknowledgedAt?: string | null
}

/** Subject a reminder/alert can attach to. */
export type AlertSubjectType = 'task' | 'event'

export interface AlertDto {
  id: string
  subjectType: AlertSubjectType
  subjectId: string
  offsetMinutes: number
  channels?: string[] | null
  isEnabled: boolean
}

// ---------------------------------------------------------------------------
// Request types
// ---------------------------------------------------------------------------

export interface CreateCalendarRequest {
  name: string
  color?: string | null
  isDefault?: boolean
  timeZone?: string
}

export interface UpdateCalendarRequest {
  name?: string
  color?: string | null
  isVisible?: boolean
  timeZone?: string
  isDefault?: boolean
  archive?: boolean
}

export interface CreateEventRequest {
  calendarId: string
  title: string
  startsAt: string
  endsAt: string
  description?: string | null
  location?: string | null
  url?: string | null
  isAllDay?: boolean
  timeZone?: string
  rrule?: string | null
  status?: EventStatus
}

export interface UpdateEventRequest {
  title?: string
  description?: string | null
  location?: string | null
  url?: string | null
  startsAt?: string
  endsAt?: string
  isAllDay?: boolean
  calendarId?: string
}

export interface EventFilters {
  from: string
  to: string
  calendarIds?: string[]
}

export interface CreateTaskRequest {
  listId: string
  title: string
  notes?: string | null
  parentTaskId?: string | null
  dueAt?: string | null
  dueHasTime?: boolean
  priority?: TaskPriority
  timeZone?: string
  rrule?: string | null
  position?: number
}

export interface UpdateTaskRequest {
  title: string
  notes?: string | null
  dueAt?: string | null
  dueHasTime?: boolean
  priority?: TaskPriority
}

export interface TaskFilters {
  listId?: string
  status?: TaskItemStatus
  due?: TaskDueBucket
}

export interface CreateTaskListRequest {
  name: string
  isDefault?: boolean
  position?: number
}

export interface UpdateTaskListRequest {
  name?: string
  position?: number
  isDefault?: boolean
  archive?: boolean
}

export interface CreateReminderRequest {
  title: string
  notes?: string | null
  remindAt: string
  timeZone?: string
  rrule?: string | null
}

export interface SnoozeReminderRequest {
  until: string
}

export interface CreateAlertRequest {
  offsetMinutes: number
  channels?: string[] | null
}

/** A Pandora calendar fed by a Google calendar (Google -> Pandora only). */
export type CalendarBindingStatus = 'ok' | 'needs-reconnect' | 'error'

export type CalendarBindingDirection = 'read' | 'read-write'

/** An event or task both sides changed between two syncs; losingVersion is the version that lost, as JSON. */
export interface SyncConflictDto {
  id: string
  subjectType: 'event' | 'task'
  subjectId: string
  title: string
  losingSide: 'pandora' | 'google'
  losingVersion: string
  detectedAt: string
}

export interface CalendarBindingDto {
  id: string
  calendarId: string
  calendarName: string
  calendarColor?: string | null
  externalAccountId: string
  remoteCalendarId: string
  /** read: Google → Pandora only; read-write: Pandora's edits go to Google too. */
  direction: CalendarBindingDirection
  status: CalendarBindingStatus
  lastSyncedAt?: string | null
  lastError?: string | null
}

/** A Pandora task list synced both ways with a Google Tasks list. */
export interface TaskListBindingDto {
  id: string
  listId: string
  listName: string
  externalAccountId: string
  remoteListId: string
  status: CalendarBindingStatus
  lastSyncedAt?: string | null
  lastError?: string | null
}

/** A task list of the Google account; bindingId is set when it already syncs with Pandora. */
export interface GoogleTaskListDto {
  id: string
  name: string
  bindingId?: string | null
}

/** A calendar the Google account can see; bindingId is set when it already feeds Pandora. */
export interface GoogleCalendarDto {
  id: string
  name: string
  color?: string | null
  isPrimary: boolean
  accessRole?: string | null
  bindingId?: string | null
}
