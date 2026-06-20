import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type {
  CreateTransferFromPendingRequest,
  PendingTransactionFilters,
  UpdatePendingTransactionRequest,
} from '../models'
import * as pendingService from '../services/pendingTransactions.service'

export function usePendingTransactions(filters: PendingTransactionFilters = {}) {
  return useQuery({
    queryKey: financeKeys.pendingList(filters),
    queryFn: () => pendingService.listPendingTransactions(filters),
  })
}

/**
 * Invalidates caches affected by a pending-transaction decision: the inbox plus
 * the transaction list, account balances and card data (an approval posts a transaction).
 */
function useInvalidatePendingEffects() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: financeKeys.pending() })
    queryClient.invalidateQueries({ queryKey: financeKeys.transactions() })
    queryClient.invalidateQueries({ queryKey: financeKeys.accounts() })
    queryClient.invalidateQueries({ queryKey: financeKeys.cards() })
    queryClient.invalidateQueries({ queryKey: financeKeys.statements() })
    // A manual link flips an import row's dedup status, so the import detail grid is stale too.
    queryClient.invalidateQueries({ queryKey: financeKeys.imports() })
  }
}

export function useUpdatePendingTransaction() {
  const invalidate = useInvalidatePendingEffects()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdatePendingTransactionRequest }) =>
      pendingService.updatePendingTransaction(id, body),
    onSuccess: invalidate,
  })
}

export function useApprovePendingTransaction() {
  const invalidate = useInvalidatePendingEffects()
  return useMutation({
    mutationFn: (id: string) => pendingService.approvePendingTransaction(id),
    onSuccess: invalidate,
  })
}

export function useRejectPendingTransaction() {
  const invalidate = useInvalidatePendingEffects()
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string | null }) =>
      pendingService.rejectPendingTransaction(id, reason),
    onSuccess: invalidate,
  })
}

export function useApprovePendingTransactionBatch() {
  const invalidate = useInvalidatePendingEffects()
  return useMutation({
    mutationFn: (ids: string[]) => pendingService.approvePendingTransactionBatch(ids),
    onSuccess: invalidate,
  })
}

export function useLinkPendingTransaction() {
  const invalidate = useInvalidatePendingEffects()
  return useMutation({
    mutationFn: ({ id, transactionId }: { id: string; transactionId: string }) =>
      pendingService.linkPendingTransaction(id, transactionId),
    onSuccess: invalidate,
  })
}

export function useCreateTransferFromPending() {
  const invalidate = useInvalidatePendingEffects()
  return useMutation({
    mutationFn: (body: CreateTransferFromPendingRequest) =>
      pendingService.createTransferFromPending(body),
    onSuccess: invalidate,
  })
}
