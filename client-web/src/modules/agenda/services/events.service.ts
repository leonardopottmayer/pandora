import { apiClient } from '@/lib/api/client'
import type {
  CreateEventRequest,
  EventDto,
  EventFilters,
  EventOccurrenceDto,
  EventScope,
  UpdateEventRequest,
} from '../models'

const BASE = '/api/v1.0/agenda/events'

export async function listOccurrences(filters: EventFilters): Promise<EventOccurrenceDto[]> {
  const { data } = await apiClient.get<EventOccurrenceDto[]>(BASE, {
    params: { from: filters.from, to: filters.to, calendarIds: filters.calendarIds },
  })
  return data
}

/** The series row (includes rrule/recurrenceEndsAt) — GET /events/{id}. */
export async function getEvent(id: string): Promise<EventDto> {
  const { data } = await apiClient.get<EventDto>(`${BASE}/${id}`)
  return data
}

export async function createEvent(body: CreateEventRequest): Promise<EventDto> {
  const { data } = await apiClient.post<EventDto>(BASE, body)
  return data
}

export interface UpdateEventParams {
  id: string
  body: UpdateEventRequest
  scope?: EventScope
  occurrenceStart?: string
}

export async function updateEvent({
  id,
  body,
  scope,
  occurrenceStart,
}: UpdateEventParams): Promise<EventDto> {
  const { data } = await apiClient.patch<EventDto>(`${BASE}/${id}`, body, {
    params: { scope, occurrenceStart },
  })
  return data
}

export interface DeleteEventParams {
  id: string
  scope: EventScope
  occurrenceStart?: string
}

export async function deleteEvent({ id, scope, occurrenceStart }: DeleteEventParams): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`, { params: { scope, occurrenceStart } })
}
