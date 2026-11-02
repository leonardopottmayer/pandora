import { apiClient } from '@/lib/api/client'
import type { SaveSpaceRequest, SpaceDto } from '../models'

const BASE = '/api/v1/notes/spaces'

export async function listSpaces(includeArchived = false): Promise<SpaceDto[]> {
  const { data } = await apiClient.get<SpaceDto[]>(BASE, { params: { includeArchived } })
  return data
}

export async function createSpace(body: SaveSpaceRequest): Promise<SpaceDto> {
  const { data } = await apiClient.post<SpaceDto>(BASE, body)
  return data
}

export async function updateSpace(id: string, body: SaveSpaceRequest): Promise<SpaceDto> {
  const { data } = await apiClient.put<SpaceDto>(`${BASE}/${id}`, body)
  return data
}

export async function setDefaultSpace(id: string): Promise<SpaceDto> {
  const { data } = await apiClient.post<SpaceDto>(`${BASE}/${id}/default`)
  return data
}

export async function setSpaceArchived(id: string, archived: boolean): Promise<SpaceDto> {
  const { data } = await apiClient.post<SpaceDto>(`${BASE}/${id}/${archived ? 'archive' : 'unarchive'}`)
  return data
}

export async function deleteSpace(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}
