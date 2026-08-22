import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import type { CreateReminderRequest } from '../models'
import * as remindersService from '../services/reminders.service'

export function useReminders() {
  return useQuery({
    queryKey: agendaKeys.reminderList(),
    queryFn: () => remindersService.listReminders(),
  })
}

/** Invalidates reminders and the Today view (which includes reminders). */
function useInvalidateReminders() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: agendaKeys.reminders() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.today() })
  }
}

export function useCreateReminder() {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: (body: CreateReminderRequest) => remindersService.createReminder(body),
    onSuccess: invalidate,
  })
}

export function useAcknowledgeReminder() {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: (id: string) => remindersService.acknowledgeReminder(id),
    onSuccess: invalidate,
  })
}

export function useSnoozeReminder() {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: ({ id, until }: { id: string; until: string }) =>
      remindersService.snoozeReminder(id, { until }),
    onSuccess: invalidate,
  })
}

export function useDeleteReminder() {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: (id: string) => remindersService.deleteReminder(id),
    onSuccess: invalidate,
  })
}
