import { apiClient } from '@/lib/api/client'
import type { AttachmentDto } from '../models'

const BASE = '/api/v1/notes/attachments'

export async function uploadAttachment(file: File, pageId?: string): Promise<AttachmentDto> {
  const formData = new FormData()
  formData.append('file', file)
  if (pageId) formData.append('pageId', pageId)

  const { data } = await apiClient.post<AttachmentDto>(BASE, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

/**
 * Fetches an attachment's binary through the authenticated endpoint. The download route
 * requires the Bearer token, so a plain `<img src>` would 401 — callers turn this blob into
 * an object URL instead. `url` is the path stored in the markdown (`/api/v1/notes/attachments/{id}`).
 */
export async function fetchAttachmentBlob(url: string): Promise<Blob> {
  const { data } = await apiClient.get<Blob>(url, { responseType: 'blob' })
  return data
}
