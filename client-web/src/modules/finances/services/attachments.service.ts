import { apiClient } from '@/lib/api/client'
import type { AttachmentDto, AttachmentKind, AttachmentOwner } from '../models'

const BASE = '/api/v1.0/finances/attachments'

export async function listAttachments(owner: AttachmentOwner): Promise<AttachmentDto[]> {
  const { data } = await apiClient.get<AttachmentDto[]>(BASE, { params: owner })
  return data
}

export async function uploadAttachment(
  owner: AttachmentOwner,
  kind: AttachmentKind,
  file: File,
): Promise<AttachmentDto> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('kind', kind)
  // `queued` is not a form field the API knows: no owner id is what queues the file.
  for (const [key, value] of Object.entries(owner)) formData.append(key, String(value))

  const { data } = await apiClient.post<AttachmentDto>(BASE, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

/** Files a queued attachment under one transaction, suggestion or statement. */
export async function assignAttachment(id: string, owner: AttachmentOwner): Promise<AttachmentDto> {
  const { data } = await apiClient.post<AttachmentDto>(`${BASE}/${id}/assign`, owner)
  return data
}

export async function deleteAttachment(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

/**
 * Opens an attachment in a new tab. The download route needs the Bearer token, so the browser cannot
 * follow `url` by itself: the bytes are fetched here and shown from an object URL instead. The tab is
 * opened before the fetch so the popup blocker still sees it as the user's click.
 */
export async function openAttachment(attachment: AttachmentDto): Promise<void> {
  const tab = window.open('', '_blank')
  try {
    const { data } = await apiClient.get<Blob>(attachment.url, { responseType: 'blob' })
    const objectUrl = URL.createObjectURL(data)
    if (tab) tab.location.href = objectUrl
    else window.open(objectUrl, '_blank')
    // The new tab has loaded it long before this; revoking right away would race the load.
    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
  } catch (err) {
    tab?.close()
    throw err
  }
}
