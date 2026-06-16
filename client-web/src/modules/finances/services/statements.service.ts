import { apiClient } from '@/lib/api/client'
import type {
  CardStatementDetailDto,
  CardStatementDto,
  PayStatementRequest,
  SetEntityTagsRequest,
} from '../models'

const BASE = '/api/v1.0/finances/statements'

export async function getStatement(id: string): Promise<CardStatementDetailDto> {
  const { data } = await apiClient.get<CardStatementDetailDto>(`${BASE}/${id}`)
  return data
}

export async function payStatement(
  id: string,
  body: PayStatementRequest,
): Promise<CardStatementDto> {
  const { data } = await apiClient.post<CardStatementDto>(`${BASE}/${id}/pay`, body)
  return data
}

export async function closeStatement(id: string): Promise<CardStatementDto> {
  const { data } = await apiClient.post<CardStatementDto>(`${BASE}/${id}/close`)
  return data
}

export async function reopenStatement(id: string): Promise<CardStatementDto> {
  const { data } = await apiClient.post<CardStatementDto>(`${BASE}/${id}/reopen`)
  return data
}

export async function setStatementTags(id: string, body: SetEntityTagsRequest): Promise<void> {
  await apiClient.put(`${BASE}/${id}/tags`, body)
}
