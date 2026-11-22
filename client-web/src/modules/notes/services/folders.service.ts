import { apiClient } from '@/lib/api/client'
import type { FolderDto, FolderPropertiesDto, SaveFolderRequest } from '../models'

const BASE = '/api/v1/notes/folders'

export async function getFolderProperties(id: string, includeArchived = false, tagIds: string[] = []): Promise<FolderPropertiesDto> {
  const params = new URLSearchParams({ includeArchived: String(includeArchived) })
  tagIds.forEach((tagId) => params.append('tagIds', tagId))
  const { data } = await apiClient.get<FolderPropertiesDto>(`${BASE}/${id}/properties`, { params })
  return data
}

/** The user's folders, one space's when `spaceId` is set, every space's otherwise. */
export async function listFolders(spaceId: string | null = null): Promise<FolderDto[]> {
  const { data } = await apiClient.get<FolderDto[]>(BASE, { params: { spaceId: spaceId ?? undefined } })
  return data
}

export async function createFolder(body: SaveFolderRequest): Promise<FolderDto> {
  const { data } = await apiClient.post<FolderDto>(BASE, body)
  return data
}

export async function updateFolder(id: string, body: SaveFolderRequest): Promise<FolderDto> {
  const { data } = await apiClient.put<FolderDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteFolder(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}
