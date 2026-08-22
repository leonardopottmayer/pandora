import { apiClient } from '@/lib/api/client'
import type { AlertDto, AlertSubjectType, CreateAlertRequest } from '../models'

const BASE = '/api/v1.0/agenda'

export async function listAlerts(
  subjectType: AlertSubjectType,
  subjectId: string,
): Promise<AlertDto[]> {
  const { data } = await apiClient.get<AlertDto[]>(`${BASE}/${subjectType}/${subjectId}/alerts`)
  return data
}

export async function createAlert(
  subjectType: AlertSubjectType,
  subjectId: string,
  body: CreateAlertRequest,
): Promise<AlertDto> {
  const { data } = await apiClient.post<AlertDto>(
    `${BASE}/${subjectType}/${subjectId}/alerts`,
    body,
  )
  return data
}

export async function deleteAlert(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/alerts/${id}`)
}
