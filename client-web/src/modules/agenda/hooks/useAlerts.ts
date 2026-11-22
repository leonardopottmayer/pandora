import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import type { AlertSubjectType, CreateAlertRequest } from '../models'
import * as alertsService from '../services/alerts.service'

export function useAlerts(subjectType: AlertSubjectType, subjectId: string | null) {
  return useQuery({
    queryKey: agendaKeys.alertList(subjectType, subjectId ?? ''),
    queryFn: () => alertsService.listAlerts(subjectType, subjectId as string),
    enabled: !!subjectId,
  })
}

function useInvalidateAlerts(subjectType: AlertSubjectType, subjectId: string | null) {
  const queryClient = useQueryClient()
  return () =>
    queryClient.invalidateQueries({
      queryKey: agendaKeys.alertList(subjectType, subjectId ?? ''),
    })
}

export function useCreateAlert(subjectType: AlertSubjectType, subjectId: string | null) {
  const invalidate = useInvalidateAlerts(subjectType, subjectId)
  return useMutation({
    mutationFn: (body: CreateAlertRequest) =>
      alertsService.createAlert(subjectType, subjectId as string, body),
    onSuccess: invalidate,
  })
}

export function useDeleteAlert(subjectType: AlertSubjectType, subjectId: string | null) {
  const invalidate = useInvalidateAlerts(subjectType, subjectId)
  return useMutation({
    mutationFn: (id: string) => alertsService.deleteAlert(id),
    onSuccess: invalidate,
  })
}
