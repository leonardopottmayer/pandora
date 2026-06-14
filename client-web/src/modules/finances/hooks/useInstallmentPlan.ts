import { useQuery } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import * as installmentPlansService from '../services/installmentPlans.service'

export function useInstallmentPlan(id: string) {
  return useQuery({
    queryKey: financeKeys.installmentPlan(id),
    queryFn: () => installmentPlansService.getInstallmentPlan(id),
    enabled: !!id,
  })
}
