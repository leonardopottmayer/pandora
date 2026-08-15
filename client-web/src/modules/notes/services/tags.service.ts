import { apiClient } from '@/lib/api/client'
import type { TagDto } from '../models'

const BASE = '/api/v1/notes/tags'

/** Every tag of the user, with how many pages carry each. */
export async function listTags(): Promise<TagDto[]> {
  const { data } = await apiClient.get<TagDto[]>(BASE)
  return data
}

/**
 * Paints a tag, or clears the color with `null`. It is the only editable part of a tag — the name
 * comes from the pages' markdown.
 */
export async function setTagColor(id: string, color: string | null): Promise<TagDto> {
  const { data } = await apiClient.put<TagDto>(`${BASE}/${id}/color`, { color })
  return data
}
