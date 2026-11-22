import { apiClient } from '@/lib/api/client'
import type { MediaFolderDto, MediaFoldersDto, MediaItemDto, MediaUsageDto } from '../models'

const BASE = '/api/v1/notes/media'
const FOLDERS = '/api/v1/notes/media-folders'

/** Adds a file to the library, in a folder or unsorted. */
export async function uploadMedia(file: File, folderId?: string | null): Promise<MediaItemDto> {
  const formData = new FormData()
  formData.append('file', file)
  if (folderId) formData.append('folderId', folderId)

  const { data } = await apiClient.post<MediaItemDto>(BASE, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

export interface MediaFilter {
  /** One folder's own items; with `unsorted`, the items in no folder; with neither, all of them. */
  folderId: string | null
  unsorted: boolean
  kind: string | null
  q: string
  /** Only the items no page shows. */
  unused?: boolean
}

export async function listMedia(filter: MediaFilter): Promise<MediaItemDto[]> {
  const { data } = await apiClient.get<MediaItemDto[]>(BASE, {
    params: {
      folderId: filter.folderId ?? undefined,
      unsorted: filter.unsorted || undefined,
      kind: filter.kind ?? undefined,
      q: filter.q.trim() || undefined,
      unused: filter.unused || undefined,
    },
  })
  return data
}

/** The items behind these ids, deleted ones included; ids that are not the user's are left out. */
export async function lookupMedia(ids: string[]): Promise<MediaItemDto[]> {
  const { data } = await apiClient.get<MediaItemDto[]>(`${BASE}/lookup`, {
    params: { ids },
    paramsSerializer: { indexes: null },
  })
  return data
}

/** The pages that show an item. */
export async function getMediaUsages(id: string): Promise<MediaUsageDto[]> {
  const { data } = await apiClient.get<MediaUsageDto[]>(`${BASE}/${id}/usages`)
  return data
}

export async function renameMedia(id: string, name: string): Promise<MediaItemDto> {
  const { data } = await apiClient.put<MediaItemDto>(`${BASE}/${id}`, { name })
  return data
}

/** To a folder, or to no folder at all ("Unsorted") with null. */
export async function moveMedia(id: string, folderId: string | null): Promise<MediaItemDto> {
  const { data } = await apiClient.post<MediaItemDto>(`${BASE}/${id}/move`, { folderId })
  return data
}

export async function deleteMedia(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

export async function listMediaFolders(): Promise<MediaFoldersDto> {
  const { data } = await apiClient.get<MediaFoldersDto>(FOLDERS)
  return data
}

export async function createMediaFolder(name: string, parentId: string | null): Promise<MediaFolderDto> {
  const { data } = await apiClient.post<MediaFolderDto>(FOLDERS, { name, parentId })
  return data
}

export async function renameMediaFolder(id: string, name: string): Promise<MediaFolderDto> {
  const { data } = await apiClient.put<MediaFolderDto>(`${FOLDERS}/${id}`, { name })
  return data
}

export async function moveMediaFolder(id: string, parentId: string | null): Promise<MediaFolderDto> {
  const { data } = await apiClient.post<MediaFolderDto>(`${FOLDERS}/${id}/move`, { parentId })
  return data
}

/** What the folder holds moves up a level; nothing is deleted with it. */
export async function deleteMediaFolder(id: string): Promise<void> {
  await apiClient.delete(`${FOLDERS}/${id}`)
}

// The bytes come through the shared authenticated download helpers; these names are what the notes
// code calls them.
export { fetchBlob as fetchMediaBlob, downloadBlob as downloadMedia } from '@/lib/api/blob'
