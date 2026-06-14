import { apiClient } from '@/lib/api/client'
import type {
  AccountBalanceDto,
  AccountDto,
  CreateAccountRequest,
  SetEntityTagsRequest,
  TransactionDto,
  TransactionFilters,
  UpdateAccountRequest,
} from '../models'

const BASE = '/api/v1.0/finances/accounts'

export interface ListAccountsParams {
  includeArchived?: boolean
  tags?: string[]
}

export async function listAccounts(params: ListAccountsParams = {}): Promise<AccountDto[]> {
  const { data } = await apiClient.get<AccountDto[]>(BASE, {
    params: { includeArchived: params.includeArchived, tags: params.tags },
  })
  return data
}

export async function getAccount(id: string): Promise<AccountDto> {
  const { data } = await apiClient.get<AccountDto>(`${BASE}/${id}`)
  return data
}

export async function getAccountBalance(id: string): Promise<AccountBalanceDto> {
  const { data } = await apiClient.get<AccountBalanceDto>(`${BASE}/${id}/balance`)
  return data
}

export async function getAccountTransactions(
  id: string,
  filters: TransactionFilters = {},
): Promise<TransactionDto[]> {
  const { data } = await apiClient.get<TransactionDto[]>(`${BASE}/${id}/transactions`, {
    params: {
      from: filters.from,
      to: filters.to,
      kind: filters.kind,
      status: filters.status,
      text: filters.text,
      tags: filters.tags,
      skip: filters.skip,
      take: filters.take,
    },
  })
  return data
}

export async function createAccount(body: CreateAccountRequest): Promise<AccountDto> {
  const { data } = await apiClient.post<AccountDto>(BASE, body)
  return data
}

export async function updateAccount(id: string, body: UpdateAccountRequest): Promise<AccountDto> {
  const { data } = await apiClient.put<AccountDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteAccount(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

export async function archiveAccount(id: string): Promise<AccountDto> {
  const { data } = await apiClient.post<AccountDto>(`${BASE}/${id}/archive`)
  return data
}

export async function unarchiveAccount(id: string): Promise<AccountDto> {
  const { data } = await apiClient.post<AccountDto>(`${BASE}/${id}/unarchive`)
  return data
}

export async function setAccountTags(id: string, body: SetEntityTagsRequest): Promise<void> {
  await apiClient.put(`${BASE}/${id}/tags`, body)
}
