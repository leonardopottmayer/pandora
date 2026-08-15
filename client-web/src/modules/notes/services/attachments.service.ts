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

/**
 * Saves an attachment to disk. A plain click on the link in the markdown cannot do it: the href is a
 * path on the API, so the browser would navigate the current tab to an endpoint that answers 401
 * without the Bearer token (and, in dev, is not even the same origin as the app). Fetching the blob
 * here and handing it to a throwaway anchor keeps the download in place, with no tab involved.
 */
export async function downloadAttachment(url: string, fileName: string): Promise<void> {
  const blob = await fetchAttachmentBlob(url)
  const objectUrl = URL.createObjectURL(blob)

  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()

  // The click starts the save synchronously, but revoking in the same tick races it in some browsers.
  setTimeout(() => URL.revokeObjectURL(objectUrl), 0)
}
