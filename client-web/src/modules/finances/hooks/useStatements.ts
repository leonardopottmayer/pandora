import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { PayStatementRequest } from '../models'
import * as statementsService from '../services/statements.service'

export function useStatement(id: string) {
  return useQuery({
    queryKey: financeKeys.statement(id),
    queryFn: () => statementsService.getStatement(id),
    enabled: !!id,
  })
}

/** Paying/closing a statement affects the statement, cards (limit), and account balances. */
function useInvalidateStatementEffects() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: financeKeys.statements() })
    queryClient.invalidateQueries({ queryKey: financeKeys.cards() })
    queryClient.invalidateQueries({ queryKey: financeKeys.accounts() })
    queryClient.invalidateQueries({ queryKey: financeKeys.transactions() })
  }
}

export function usePayStatement() {
  const invalidate = useInvalidateStatementEffects()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: PayStatementRequest }) =>
      statementsService.payStatement(id, body),
    onSuccess: invalidate,
  })
}

export function useCloseStatement() {
  const invalidate = useInvalidateStatementEffects()
  return useMutation({
    mutationFn: (id: string) => statementsService.closeStatement(id),
    onSuccess: invalidate,
  })
}

export function useReopenStatement() {
  const invalidate = useInvalidateStatementEffects()
  return useMutation({
    mutationFn: (id: string) => statementsService.reopenStatement(id),
    onSuccess: invalidate,
  })
}
