import { apiClient } from '@/lib/api/client'
import type { InstallmentPlanDto } from '../models'

const BASE = '/api/v1.0/finances/installment-plans'

export async function getInstallmentPlan(id: string): Promise<InstallmentPlanDto> {
  const { data } = await apiClient.get<InstallmentPlanDto>(`${BASE}/${id}`)
  return data
}
