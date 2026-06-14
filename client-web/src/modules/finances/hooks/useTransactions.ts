import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type {
  CreateTransactionRequest,
  CreateTransferRequest,
  TransactionFilters,
  UpdateTransactionRequest,
  VoidTransactionRequest,
} from '../models'
import * as transactionsService from '../services/transactions.service'

export function useTransactions(filters: TransactionFilters = {}) {
  return useQuery({
    queryKey: financeKeys.transactionList(filters),
    queryFn: () => transactionsService.listTransactions(filters),
  })
}

/**
 * Invalida os caches afetados por uma mutação de transação: a própria lista, os
 * saldos de contas e os dados de cartões (faturas/limite).
 */
function useInvalidateTransactionEffects() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: financeKeys.transactions() })
    queryClient.invalidateQueries({ queryKey: financeKeys.accounts() })
    queryClient.invalidateQueries({ queryKey: financeKeys.cards() })
    queryClient.invalidateQueries({ queryKey: financeKeys.statements() })
  }
}

export function useCreateTransaction() {
  const invalidate = useInvalidateTransactionEffects()
  return useMutation({
    mutationFn: (body: CreateTransactionRequest) => transactionsService.createTransaction(body),
    onSuccess: invalidate,
  })
}

export function useCreateTransfer() {
  const invalidate = useInvalidateTransactionEffects()
  return useMutation({
    mutationFn: (body: CreateTransferRequest) => transactionsService.createTransfer(body),
    onSuccess: invalidate,
  })
}

export function useUpdateTransaction() {
  const invalidate = useInvalidateTransactionEffects()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTransactionRequest }) =>
      transactionsService.updateTransaction(id, body),
    onSuccess: invalidate,
  })
}

export function usePostTransaction() {
  const invalidate = useInvalidateTransactionEffects()
  return useMutation({
    mutationFn: (id: string) => transactionsService.postTransaction(id),
    onSuccess: invalidate,
  })
}

export function useVoidTransaction() {
  const invalidate = useInvalidateTransactionEffects()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body?: VoidTransactionRequest }) =>
      transactionsService.voidTransaction(id, body),
    onSuccess: invalidate,
  })
}
