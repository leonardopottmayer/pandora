import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import type { CalendarDto, CreateCalendarRequest, UpdateCalendarRequest } from '../models'
import * as calendarsService from '../services/calendars.service'

export function useCalendars() {
  return useQuery({
    queryKey: agendaKeys.calendarList(),
    queryFn: () => calendarsService.listCalendars(),
  })
}

/** id → calendar map for colour/name lookups. */
export function useCalendarMap(): Map<string, CalendarDto> {
  const { data } = useCalendars()
  return useMemo(() => {
    const map = new Map<string, CalendarDto>()
    for (const c of data ?? []) map.set(c.id, c)
    return map
  }, [data])
}

/** Deleting a calendar with live events fails; the caller shows the 409 message. */
function useInvalidateCalendars() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: agendaKeys.calendars() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.events() })
  }
}

export function useCreateCalendar() {
  const invalidate = useInvalidateCalendars()
  return useMutation({
    mutationFn: (body: CreateCalendarRequest) => calendarsService.createCalendar(body),
    onSuccess: invalidate,
  })
}

export function useUpdateCalendar() {
  const invalidate = useInvalidateCalendars()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateCalendarRequest }) =>
      calendarsService.updateCalendar(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteCalendar() {
  const invalidate = useInvalidateCalendars()
  return useMutation({
    mutationFn: (id: string) => calendarsService.deleteCalendar(id),
    onSuccess: invalidate,
  })
}
