import { apiClient } from '@/lib/api/client'
import type {
  CreateTagRequest,
  LinkTagRequest,
  TaggableEntityType,
  TagDto,
  TagLinkDto,
  UpdateTagRequest,
} from '../models'

const BASE = '/api/v1.0/finances/tags'

export async function listTags(): Promise<TagDto[]> {
  const { data } = await apiClient.get<TagDto[]>(BASE)
  return data
}

export async function createTag(body: CreateTagRequest): Promise<TagDto> {
  const { data } = await apiClient.post<TagDto>(BASE, body)
  return data
}

export async function updateTag(id: string, body: UpdateTagRequest): Promise<TagDto> {
  const { data } = await apiClient.put<TagDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteTag(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

export async function getTagLinks(id: string): Promise<TagLinkDto[]> {
  const { data } = await apiClient.get<TagLinkDto[]>(`${BASE}/${id}/links`)
  return data
}

export async function linkTag(id: string, body: LinkTagRequest): Promise<TagLinkDto> {
  const { data } = await apiClient.post<TagLinkDto>(`${BASE}/${id}/links`, body)
  return data
}

export async function unlinkTag(
  id: string,
  entityType: TaggableEntityType,
  entityId: string,
): Promise<void> {
  await apiClient.delete(`${BASE}/${id}/links/${entityType}/${entityId}`)
}
