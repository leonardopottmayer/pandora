import { apiClient } from '@/lib/api/client'
import type {
  CardAvailableLimitDto,
  CardDto,
  CardStatementDto,
  CreateCardRequest,
  InstallmentPlanDto,
  SetEntityTagsRequest,
  UpdateCardRequest,
} from '../models'

const BASE = '/api/v1.0/finances/cards'

export interface ListCardsParams {
  includeArchived?: boolean
  tags?: string[]
}

export async function listCards(params: ListCardsParams = {}): Promise<CardDto[]> {
  const { data } = await apiClient.get<CardDto[]>(BASE, {
    params: { includeArchived: params.includeArchived, tags: params.tags },
  })
  return data
}

export async function getCard(id: string): Promise<CardDto> {
  const { data } = await apiClient.get<CardDto>(`${BASE}/${id}`)
  return data
}

export async function createCard(body: CreateCardRequest): Promise<CardDto> {
  const { data } = await apiClient.post<CardDto>(BASE, body)
  return data
}

export async function updateCard(id: string, body: UpdateCardRequest): Promise<CardDto> {
  const { data } = await apiClient.put<CardDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteCard(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

export async function archiveCard(id: string): Promise<CardDto> {
  const { data } = await apiClient.post<CardDto>(`${BASE}/${id}/archive`)
  return data
}

export async function unarchiveCard(id: string): Promise<CardDto> {
  const { data } = await apiClient.post<CardDto>(`${BASE}/${id}/unarchive`)
  return data
}

export async function getCardStatements(id: string): Promise<CardStatementDto[]> {
  const { data } = await apiClient.get<CardStatementDto[]>(`${BASE}/${id}/statements`)
  return data
}

export async function getCardInstallmentPlans(id: string): Promise<InstallmentPlanDto[]> {
  const { data } = await apiClient.get<InstallmentPlanDto[]>(`${BASE}/${id}/installment-plans`)
  return data
}

export async function getCardAvailableLimit(id: string): Promise<CardAvailableLimitDto> {
  const { data } = await apiClient.get<CardAvailableLimitDto>(`${BASE}/${id}/available-limit`)
  return data
}

export async function setCardTags(id: string, body: SetEntityTagsRequest): Promise<void> {
  await apiClient.put(`${BASE}/${id}/tags`, body)
}
