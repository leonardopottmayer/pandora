import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { CreateAccountRequest, TransactionFilters, UpdateAccountRequest } from '../models'
import * as accountsService from '../services/accounts.service'
import type { ListAccountsParams } from '../services/accounts.service'

export function useAccounts(params: ListAccountsParams = {}) {
  return useQuery({
    queryKey: financeKeys.accountList(params),
    queryFn: () => accountsService.listAccounts(params),
  })
}

export function useAccount(id: string) {
  return useQuery({
    queryKey: financeKeys.account(id),
    queryFn: () => accountsService.getAccount(id),
    enabled: !!id,
  })
}

export function useAccountBalance(id: string) {
  return useQuery({
    queryKey: financeKeys.accountBalance(id),
    queryFn: () => accountsService.getAccountBalance(id),
    enabled: !!id,
  })
}

export function useAccountTransactions(id: string, filters: TransactionFilters = {}) {
  return useQuery({
    queryKey: financeKeys.accountTransactions(id, filters),
    queryFn: () => accountsService.getAccountTransactions(id, filters),
    enabled: !!id,
  })
}

/** Invalida tudo sob `finances/accounts` após uma mutação. */
function useInvalidateAccounts() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: financeKeys.accounts() })
}

export function useCreateAccount() {
  const invalidate = useInvalidateAccounts()
  return useMutation({
    mutationFn: (body: CreateAccountRequest) => accountsService.createAccount(body),
    onSuccess: invalidate,
  })
}

export function useUpdateAccount() {
  const invalidate = useInvalidateAccounts()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateAccountRequest }) =>
      accountsService.updateAccount(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteAccount() {
  const invalidate = useInvalidateAccounts()
  return useMutation({
    mutationFn: (id: string) => accountsService.deleteAccount(id),
    onSuccess: invalidate,
  })
}

export function useSetAccountArchived() {
  const invalidate = useInvalidateAccounts()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) =>
      archived ? accountsService.archiveAccount(id) : accountsService.unarchiveAccount(id),
    onSuccess: invalidate,
  })
}
