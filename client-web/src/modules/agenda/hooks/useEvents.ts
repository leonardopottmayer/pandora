import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import type { CreateEventRequest, EventFilters } from '../models'
import * as eventsService from '../services/events.service'
import type { DeleteEventParams, UpdateEventParams } from '../services/events.service'

export function useEventOccurrences(filters: EventFilters, enabled = true) {
  return useQuery({
    queryKey: agendaKeys.eventList(filters),
    queryFn: () => eventsService.listOccurrences(filters),
    enabled,
  })
}

export function useEvent(id: string | null) {
  return useQuery({
    queryKey: agendaKeys.event(id ?? ''),
    queryFn: () => eventsService.getEvent(id as string),
    enabled: !!id,
  })
}

/** Invalidates events and the Today view (which includes events). */
function useInvalidateEvents() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: agendaKeys.events() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.today() })
  }
}

export function useCreateEvent() {
  const invalidate = useInvalidateEvents()
  return useMutation({
    mutationFn: (body: CreateEventRequest) => eventsService.createEvent(body),
    onSuccess: invalidate,
  })
}

export function useUpdateEvent() {
  const invalidate = useInvalidateEvents()
  return useMutation({
    mutationFn: (params: UpdateEventParams) => eventsService.updateEvent(params),
    onSuccess: invalidate,
  })
}

export function useDeleteEvent() {
  const invalidate = useInvalidateEvents()
  return useMutation({
    mutationFn: (params: DeleteEventParams) => eventsService.deleteEvent(params),
    onSuccess: invalidate,
  })
}
