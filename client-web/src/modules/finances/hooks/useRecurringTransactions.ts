import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type {
  CreateRecurringTransactionRequest,
  GenerateRecurringTransactionOccurrenceRequest,
  UpdateRecurringTransactionRequest,
} from '../models'
import * as recurringService from '../services/recurringTransactions.service'

export function useRecurringTransactions() {
  return useQuery({
    queryKey: financeKeys.recurringList(),
    queryFn: () => recurringService.listRecurringTransactions(),
  })
}

export function useRecurringTransaction(id: string) {
  return useQuery({
    queryKey: financeKeys.recurringItem(id),
    queryFn: () => recurringService.getRecurringTransaction(id),
    enabled: !!id,
  })
}

/** Invalidates everything under `finances/recurring` after a mutation. */
function useInvalidateRecurring() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: financeKeys.recurring() })
}

export function useCreateRecurringTransaction() {
  const invalidate = useInvalidateRecurring()
  return useMutation({
    mutationFn: (body: CreateRecurringTransactionRequest) =>
      recurringService.createRecurringTransaction(body),
    onSuccess: invalidate,
  })
}

export function useUpdateRecurringTransaction() {
  const invalidate = useInvalidateRecurring()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateRecurringTransactionRequest }) =>
      recurringService.updateRecurringTransaction(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteRecurringTransaction() {
  const invalidate = useInvalidateRecurring()
  return useMutation({
    mutationFn: (id: string) => recurringService.deleteRecurringTransaction(id),
    onSuccess: invalidate,
  })
}

export function useGenerateRecurringOccurrence() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      body,
    }: {
      id: string
      body: GenerateRecurringTransactionOccurrenceRequest
    }) => recurringService.generateRecurringTransactionOccurrence(id, body),
    onSuccess: () => {
      // Generation advances the recurrence cursor and creates a pending or a posted transaction.
      queryClient.invalidateQueries({ queryKey: financeKeys.recurring() })
      queryClient.invalidateQueries({ queryKey: financeKeys.pending() })
      queryClient.invalidateQueries({ queryKey: financeKeys.transactions() })
      queryClient.invalidateQueries({ queryKey: financeKeys.accounts() })
      queryClient.invalidateQueries({ queryKey: financeKeys.cards() })
      queryClient.invalidateQueries({ queryKey: financeKeys.statements() })
    },
  })
}

export function useSetRecurringPaused() {
  const invalidate = useInvalidateRecurring()
  return useMutation({
    mutationFn: ({ id, paused }: { id: string; paused: boolean }) =>
      paused
        ? recurringService.pauseRecurringTransaction(id)
        : recurringService.resumeRecurringTransaction(id),
    onSuccess: invalidate,
  })
}
