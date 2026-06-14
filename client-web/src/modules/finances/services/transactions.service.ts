import { apiClient } from '@/lib/api/client'
import type {
  CreateTransactionRequest,
  CreateTransferRequest,
  SetEntityTagsRequest,
  TransactionDto,
  TransactionFilters,
  UpdateTransactionRequest,
  VoidTransactionRequest,
} from '../models'

const BASE = '/api/v1.0/finances/transactions'

export async function listTransactions(filters: TransactionFilters = {}): Promise<TransactionDto[]> {
  const { data } = await apiClient.get<TransactionDto[]>(BASE, {
    params: {
      accountId: filters.accountId,
      from: filters.from,
      to: filters.to,
      kind: filters.kind,
      status: filters.status,
      systemCategoryId: filters.systemCategoryId,
      userCategoryId: filters.userCategoryId,
      text: filters.text,
      origin: filters.origin,
      tags: filters.tags,
      skip: filters.skip,
      take: filters.take,
    },
  })
  return data
}

/** Cria um lançamento. Pode retornar N transações quando `installments > 1`. */
export async function createTransaction(
  body: CreateTransactionRequest,
): Promise<TransactionDto | TransactionDto[]> {
  const { data } = await apiClient.post<TransactionDto | TransactionDto[]>(BASE, body)
  return data
}

export async function createTransfer(body: CreateTransferRequest): Promise<TransactionDto[]> {
  const { data } = await apiClient.post<TransactionDto[]>(`${BASE}/transfer`, body)
  return data
}

export async function updateTransaction(
  id: string,
  body: UpdateTransactionRequest,
): Promise<TransactionDto> {
  const { data } = await apiClient.put<TransactionDto>(`${BASE}/${id}`, body)
  return data
}

export async function postTransaction(id: string): Promise<TransactionDto> {
  const { data } = await apiClient.post<TransactionDto>(`${BASE}/${id}/post`)
  return data
}

export async function voidTransaction(
  id: string,
  body?: VoidTransactionRequest,
): Promise<TransactionDto> {
  const { data } = await apiClient.post<TransactionDto>(`${BASE}/${id}/void`, body ?? {})
  return data
}

export async function setTransactionTags(id: string, body: SetEntityTagsRequest): Promise<void> {
  await apiClient.put(`${BASE}/${id}/tags`, body)
}
