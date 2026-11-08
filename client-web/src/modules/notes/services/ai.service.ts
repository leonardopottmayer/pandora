import { apiClient } from '@/lib/api/client'
import type { AiGenerationDto, AskNotesRequest, NoteAnswerDto, TextTransformDto, TextTransformRequest } from '../models'

export async function transformText(body: TextTransformRequest, signal?: AbortSignal): Promise<TextTransformDto> {
  const { data } = await apiClient.post<TextTransformDto>('/api/v1/notes/ai/transform', body, { signal })
  return data
}

export async function summarizePage(pageId: string, signal?: AbortSignal): Promise<TextTransformDto> {
  const { data } = await apiClient.post<TextTransformDto>(`/api/v1/notes/pages/${pageId}/ai/summary`, undefined, { signal })
  return data
}

export async function askNotes(body: AskNotesRequest, signal?: AbortSignal): Promise<NoteAnswerDto> {
  const { data } = await apiClient.post<NoteAnswerDto>('/api/v1/notes/ai/ask', body, { signal })
  return data
}

export async function generateText(prompt: string, signal?: AbortSignal): Promise<AiGenerationDto> {
  const { data } = await apiClient.post<AiGenerationDto>('/api/v1/notes/ai/generate', { prompt }, { signal })
  return data
}
