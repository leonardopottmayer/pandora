import { apiClient } from '@/lib/api/client'
import type { CalendarBindingDto, GoogleCalendarDto, SyncConflictDto } from '../models'

const BASE = '/api/v1.0/agenda/google'

export async function listGoogleCalendars(accountId: string): Promise<GoogleCalendarDto[]> {
  const { data } = await apiClient.get<GoogleCalendarDto[]>(`${BASE}/calendars`, { params: { accountId } })
  return data
}

export async function listBindings(): Promise<CalendarBindingDto[]> {
  const { data } = await apiClient.get<CalendarBindingDto[]>(`${BASE}/bindings`)
  return data
}

export async function listConflicts(): Promise<SyncConflictDto[]> {
  const { data } = await apiClient.get<SyncConflictDto[]>(`${BASE}/conflicts`)
  return data
}

export async function bindCalendars(accountId: string, calendarIds: string[]): Promise<CalendarBindingDto[]> {
  const { data } = await apiClient.post<CalendarBindingDto[]>(`${BASE}/bindings`, { accountId, calendarIds })
  return data
}

export async function syncBinding(id: string): Promise<CalendarBindingDto> {
  const { data } = await apiClient.post<CalendarBindingDto>(`${BASE}/bindings/${id}/sync`)
  return data
}

export async function unlinkBinding(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/bindings/${id}`)
}
