import { apiClient } from '@/lib/api/client'
import type {
  ImportFileDto,
  ImportFileFilters,
  ImportLayoutDto,
  ImportRowDto,
} from '../models'

const BASE = '/api/v1.0/finances/imports'
const LAYOUTS_BASE = '/api/v1.0/finances/import-layouts'

export async function listImportFiles(filters: ImportFileFilters = {}): Promise<ImportFileDto[]> {
  const { data } = await apiClient.get<ImportFileDto[]>(BASE, {
    params: { skip: filters.skip, take: filters.take },
  })
  return data
}

export async function getImportFile(id: string): Promise<ImportFileDto> {
  const { data } = await apiClient.get<ImportFileDto>(`${BASE}/${id}`)
  return data
}

export async function getImportRows(importFileId: string): Promise<ImportRowDto[]> {
  const { data } = await apiClient.get<ImportRowDto[]>(`${BASE}/${importFileId}/rows`)
  return data
}

export async function uploadImportFile(
  file: File,
  accountId?: string,
  cardId?: string,
  cutoffDate?: string,
): Promise<ImportFileDto> {
  const formData = new FormData()
  formData.append('file', file)
  if (accountId) formData.append('accountId', accountId)
  if (cardId) formData.append('cardId', cardId)
  if (cutoffDate) formData.append('cutoffDate', cutoffDate)

  const { data } = await apiClient.post<ImportFileDto>(BASE, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

export async function abortImportFile(id: string): Promise<void> {
  await apiClient.post(`${BASE}/${id}/abort`)
}

export async function retryImportFile(id: string): Promise<void> {
  await apiClient.post(`${BASE}/${id}/retry`)
}

export async function listImportLayouts(): Promise<ImportLayoutDto[]> {
  const { data } = await apiClient.get<ImportLayoutDto[]>(LAYOUTS_BASE)
  return data
}
