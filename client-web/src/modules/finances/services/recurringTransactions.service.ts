import { apiClient } from '@/lib/api/client'
import type {
  CreateRecurringTransactionRequest,
  RecurringTransactionDto,
  UpdateRecurringTransactionRequest,
} from '../models'

const BASE = '/api/v1.0/finances/recurring-transactions'

export async function listRecurringTransactions(): Promise<RecurringTransactionDto[]> {
  const { data } = await apiClient.get<RecurringTransactionDto[]>(BASE)
  return data
}

export async function getRecurringTransaction(id: string): Promise<RecurringTransactionDto> {
  const { data } = await apiClient.get<RecurringTransactionDto>(`${BASE}/${id}`)
  return data
}

export async function createRecurringTransaction(
  body: CreateRecurringTransactionRequest,
): Promise<RecurringTransactionDto> {
  const { data } = await apiClient.post<RecurringTransactionDto>(BASE, body)
  return data
}

export async function updateRecurringTransaction(
  id: string,
  body: UpdateRecurringTransactionRequest,
): Promise<RecurringTransactionDto> {
  const { data } = await apiClient.put<RecurringTransactionDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteRecurringTransaction(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

export async function pauseRecurringTransaction(id: string): Promise<RecurringTransactionDto> {
  const { data } = await apiClient.post<RecurringTransactionDto>(`${BASE}/${id}/pause`)
  return data
}

export async function resumeRecurringTransaction(id: string): Promise<RecurringTransactionDto> {
  const { data } = await apiClient.post<RecurringTransactionDto>(`${BASE}/${id}/resume`)
  return data
}
