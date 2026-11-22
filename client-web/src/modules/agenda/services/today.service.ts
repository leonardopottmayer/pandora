import { apiClient } from '@/lib/api/client'
import type { TodayItemDto } from '../models'

const BASE = '/api/v1.0/agenda'

export async function getToday(): Promise<TodayItemDto[]> {
  const { data } = await apiClient.get<TodayItemDto[]>(`${BASE}/today`)
  return data
}
