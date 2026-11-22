import { apiClient } from '@/lib/api/client'
import type { CalendarDto, CreateCalendarRequest, UpdateCalendarRequest } from '../models'

const BASE = '/api/v1.0/agenda/calendars'

export async function listCalendars(): Promise<CalendarDto[]> {
  const { data } = await apiClient.get<CalendarDto[]>(BASE)
  return data
}

export async function createCalendar(body: CreateCalendarRequest): Promise<CalendarDto> {
  const { data } = await apiClient.post<CalendarDto>(BASE, body)
  return data
}

export async function updateCalendar(
  id: string,
  body: UpdateCalendarRequest,
): Promise<CalendarDto> {
  const { data } = await apiClient.patch<CalendarDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteCalendar(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}
