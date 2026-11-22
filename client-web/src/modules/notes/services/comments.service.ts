import { apiClient } from '@/lib/api/client'
import type { BlockStampDto, CommentDto } from '../models'

const BASE = '/api/v1/notes'

export async function listComments(pageId: string): Promise<CommentDto[]> {
  const { data } = await apiClient.get<CommentDto[]>(`${BASE}/pages/${pageId}/comments`)
  return data
}

export async function addComment(pageId: string, body: { blockId: string; quote: string | null; body: string }): Promise<CommentDto> {
  const { data } = await apiClient.post<CommentDto>(`${BASE}/pages/${pageId}/comments`, body)
  return data
}

export async function replyToComment(threadId: string, body: string): Promise<CommentDto> {
  const { data } = await apiClient.post<CommentDto>(`${BASE}/comments/${threadId}/replies`, { body })
  return data
}

export async function editComment(id: string, body: string): Promise<CommentDto> {
  const { data } = await apiClient.put<CommentDto>(`${BASE}/comments/${id}`, { body })
  return data
}

export async function setCommentResolved(id: string, resolved: boolean): Promise<CommentDto> {
  const { data } = await apiClient.put<CommentDto>(`${BASE}/comments/${id}/resolved`, { resolved })
  return data
}

/** A reply, or a thread with its replies. */
export async function deleteComment(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/comments/${id}`)
}

export async function listBlockStamps(pageId: string): Promise<BlockStampDto[]> {
  const { data } = await apiClient.get<BlockStampDto[]>(`${BASE}/pages/${pageId}/block-stamps`)
  return data
}
