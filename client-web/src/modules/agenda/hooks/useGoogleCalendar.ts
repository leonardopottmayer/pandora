import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import * as googleService from '../services/googleCalendar.service'

export function useCalendarBindings() {
  return useQuery({
    queryKey: agendaKeys.googleBindings(),
    queryFn: () => googleService.listBindings(),
  })
}

export function useGoogleCalendars(accountId: string | null) {
  return useQuery({
    queryKey: agendaKeys.googleCalendars(accountId ?? ''),
    queryFn: () => googleService.listGoogleCalendars(accountId!),
    enabled: accountId !== null,
  })
}

/** A sync changes calendars, events and alerts alike. */
function useInvalidateAfterSync() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: agendaKeys.google() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.calendars() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.events() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.today() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.alerts() })
  }
}

export function useBindGoogleCalendars() {
  const invalidate = useInvalidateAfterSync()
  return useMutation({
    mutationFn: ({ accountId, calendarIds }: { accountId: string; calendarIds: string[] }) =>
      googleService.bindCalendars(accountId, calendarIds),
    onSuccess: invalidate,
  })
}

export function useSyncBinding() {
  const invalidate = useInvalidateAfterSync()
  return useMutation({
    mutationFn: (id: string) => googleService.syncBinding(id),
    onSettled: invalidate,
  })
}

export function useUnlinkBinding() {
  const invalidate = useInvalidateAfterSync()
  return useMutation({
    mutationFn: (id: string) => googleService.unlinkBinding(id),
    onSuccess: invalidate,
  })
}
