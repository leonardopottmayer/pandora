import { apiClient } from '@/lib/api/client'
import type {
  CreateTransferFromPendingRequest,
  PendingTransactionDto,
  PendingTransactionFilters,
  TransactionDto,
  UpdatePendingTransactionRequest,
} from '../models'

const BASE = '/api/v1.0/finances/pending-transactions'

export async function listPendingTransactions(
  filters: PendingTransactionFilters = {},
): Promise<PendingTransactionDto[]> {
  const { data } = await apiClient.get<PendingTransactionDto[]>(BASE, {
    params: {
      source: filters.source,
      accountId: filters.accountId,
      cardId: filters.cardId,
      from: filters.from,
      to: filters.to,
      skip: filters.skip,
      take: filters.take,
    },
  })
  return data
}

export async function updatePendingTransaction(
  id: string,
  body: UpdatePendingTransactionRequest,
): Promise<PendingTransactionDto> {
  const { data } = await apiClient.put<PendingTransactionDto>(`${BASE}/${id}`, body)
  return data
}

export async function approvePendingTransaction(id: string): Promise<TransactionDto> {
  const { data } = await apiClient.post<TransactionDto>(`${BASE}/${id}/approve`)
  return data
}

export async function rejectPendingTransaction(
  id: string,
  reason?: string | null,
): Promise<PendingTransactionDto> {
  const { data } = await apiClient.post<PendingTransactionDto>(`${BASE}/${id}/reject`, { reason })
  return data
}

export async function approvePendingTransactionBatch(ids: string[]): Promise<number> {
  const { data } = await apiClient.post<number>(`${BASE}/approve-batch`, { ids })
  return data
}

export async function linkPendingTransaction(
  id: string,
  transactionId: string,
): Promise<PendingTransactionDto> {
  const { data } = await apiClient.post<PendingTransactionDto>(`${BASE}/${id}/link`, { transactionId })
  return data
}

export async function createTransferFromPending(
  body: CreateTransferFromPendingRequest,
): Promise<TransactionDto[]> {
  const { data } = await apiClient.post<TransactionDto[]>(`${BASE}/transfer`, body)
  return data
}
