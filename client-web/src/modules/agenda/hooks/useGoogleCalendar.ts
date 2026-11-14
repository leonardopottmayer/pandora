import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import * as googleService from '../services/googleCalendar.service'

export function useCalendarBindings() {
  return useQuery({
    queryKey: agendaKeys.googleBindings(),
    queryFn: () => googleService.listBindings(),
  })
}

export function useSyncConflicts() {
  return useQuery({
    queryKey: agendaKeys.googleConflicts(),
    queryFn: () => googleService.listConflicts(),
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

export function useTaskListBindings() {
  return useQuery({
    queryKey: agendaKeys.googleTaskListBindings(),
    queryFn: () => googleService.listTaskListBindings(),
  })
}

export function useGoogleTaskLists(accountId: string | null) {
  return useQuery({
    queryKey: agendaKeys.googleTaskLists(accountId ?? ''),
    queryFn: () => googleService.listGoogleTaskLists(accountId!),
    enabled: accountId !== null,
  })
}

/** A task sync changes lists and tasks. */
function useInvalidateAfterTaskSync() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: agendaKeys.google() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.taskLists() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.tasks() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.today() })
  }
}

export function useBindGoogleTaskLists() {
  const invalidate = useInvalidateAfterTaskSync()
  return useMutation({
    mutationFn: ({ accountId, listIds }: { accountId: string; listIds: string[] }) =>
      googleService.bindTaskLists(accountId, listIds),
    onSuccess: invalidate,
  })
}

export function useSyncTaskListBinding() {
  const invalidate = useInvalidateAfterTaskSync()
  return useMutation({
    mutationFn: (id: string) => googleService.syncTaskListBinding(id),
    onSettled: invalidate,
  })
}

export function useUnlinkTaskListBinding() {
  const invalidate = useInvalidateAfterTaskSync()
  return useMutation({
    mutationFn: (id: string) => googleService.unlinkTaskListBinding(id),
    onSuccess: invalidate,
  })
}
