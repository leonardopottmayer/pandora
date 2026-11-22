import { apiClient } from '@/lib/api/client'
import type { NotesPreferencesDto, PageDto } from '../models'

export async function openDailyNote(date?: string): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>('/api/v1/notes/daily', date ? { date } : {})
  return data
}

export async function getNotesPreferences(): Promise<NotesPreferencesDto> {
  const { data } = await apiClient.get<NotesPreferencesDto>('/api/v1/notes/preferences')
  return data
}

export async function saveNotesPreferences(body: NotesPreferencesDto): Promise<NotesPreferencesDto> {
  const { data } = await apiClient.put<NotesPreferencesDto>('/api/v1/notes/preferences', body)
  return data
}
