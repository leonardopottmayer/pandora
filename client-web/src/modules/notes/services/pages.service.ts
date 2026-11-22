import { apiClient } from '@/lib/api/client'
import type {
  BacklinkDto,
  CreatePageRequest,
  MovePageRequest,
  PageDto,
  PageGraphDto,
  PageSearchResultDto,
  PageSummaryDto,
  PageAppearance,
  PageVersionDto,
  PublicPageDto,
  TrashItemDto,
  PageVersionSummaryDto,
  UpdatePageRequest,
} from '../models'

const BASE = '/api/v1/notes/pages'

export async function setPageTemplate(id: string, isTemplate: boolean): Promise<PageDto> {
  const { data } = await apiClient.put<PageDto>(`${BASE}/${id}/template`, { isTemplate })
  return data
}

export async function createPageFromTemplate(body: { templateId: string; title: string; spaceId: string | null; folderId: string | null }): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(`${BASE}/from-template`, body)
  return data
}

export async function listPageVersions(id: string, before?: number): Promise<PageVersionSummaryDto[]> {
  const { data } = await apiClient.get<PageVersionSummaryDto[]>(`${BASE}/${id}/versions`, { params: { before } })
  return data
}

export async function getPageVersion(id: string, number: number): Promise<PageVersionDto> {
  const { data } = await apiClient.get<PageVersionDto>(`${BASE}/${id}/versions/${number}`)
  return data
}

export async function restorePageVersion(id: string, number: number, expectedVersion: number): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(`${BASE}/${id}/versions/${number}/restore`, { expectedVersion })
  return data
}

/** The user's pages, one space's when `spaceId` is set, every space's otherwise. */
export async function listPages(
  includeArchived = false,
  tagIds: string[] = [],
  spaceId: string | null = null,
): Promise<PageSummaryDto[]> {
  const { data } = await apiClient.get<PageSummaryDto[]>(BASE, {
    params: { includeArchived, tagIds, spaceId: spaceId ?? undefined },
    paramsSerializer: { indexes: null },
  })
  return data
}

export async function getPage(id: string): Promise<PageDto> {
  const { data } = await apiClient.get<PageDto>(`${BASE}/${id}`)
  return data
}

export async function getBacklinks(id: string): Promise<BacklinkDto[]> {
  const { data } = await apiClient.get<BacklinkDto[]>(`${BASE}/${id}/backlinks`)
  return data
}

export async function searchPages(
  term: string,
  tagIds: string[] = [],
  spaceId: string | null = null,
): Promise<PageSearchResultDto[]> {
  const { data } = await apiClient.get<PageSearchResultDto[]>(`${BASE}/search`, {
    params: { q: term, tagIds, spaceId: spaceId ?? undefined },
    paramsSerializer: { indexes: null },
  })
  return data
}

/** The whole wiki graph of the user, cut down to one space and to the pages carrying every tag in `tagIds`. */
export async function getGraph(tagIds: string[] = [], spaceId: string | null = null): Promise<PageGraphDto> {
  const { data } = await apiClient.get<PageGraphDto>(`${BASE}/graph`, {
    params: { tagIds, spaceId: spaceId ?? undefined },
    paramsSerializer: { indexes: null },
  })
  return data
}

/** The neighborhood of one page, `depth` hops out in either direction. */
export async function getLocalGraph(id: string, depth: number): Promise<PageGraphDto> {
  const { data } = await apiClient.get<PageGraphDto>(`${BASE}/${id}/graph`, { params: { depth } })
  return data
}

export async function createPage(body: CreatePageRequest): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(BASE, body)
  return data
}

export async function updatePage(id: string, body: UpdatePageRequest): Promise<PageDto> {
  const { data } = await apiClient.put<PageDto>(`${BASE}/${id}`, body)
  return data
}

export async function movePage(id: string, body: MovePageRequest): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(`${BASE}/${id}/move`, body)
  return data
}

export async function setPageFavorite(id: string, favorite: boolean): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(`${BASE}/${id}/${favorite ? 'favorite' : 'unfavorite'}`)
  return data
}

export async function setPageArchived(id: string, archived: boolean): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(`${BASE}/${id}/${archived ? 'archive' : 'unarchive'}`)
  return data
}

export async function deletePage(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}

export async function setPageAppearance(id: string, appearance: PageAppearance): Promise<PageDto> {
  const { data } = await apiClient.put<PageDto>(`${BASE}/${id}/appearance`, appearance)
  return data
}

export async function setPageLocked(id: string, locked: boolean): Promise<PageDto> {
  const { data } = await apiClient.put<PageDto>(`${BASE}/${id}/lock`, { locked })
  return data
}

/** `days` 1 to 365, or null for a verification that does not run out; `verified: false` clears it. */
export async function setPageVerification(id: string, verified: boolean, days: number | null = null): Promise<PageDto> {
  const { data } = await apiClient.put<PageDto>(`${BASE}/${id}/verification`, { verified, days })
  return data
}

export async function setPagePublic(id: string, isPublic: boolean): Promise<PageDto> {
  const { data } = await apiClient.put<PageDto>(`${BASE}/${id}/public`, { public: isPublic })
  return data
}

/** Notes that the page was opened, for the recent pages; not an edit. */
export async function markPageViewed(id: string): Promise<void> {
  await apiClient.post(`${BASE}/${id}/viewed`)
}

export async function listRecentPages(limit = 8): Promise<PageSummaryDto[]> {
  const { data } = await apiClient.get<PageSummaryDto[]>(`${BASE}/recent`, { params: { limit } })
  return data
}

const TRASH = '/api/v1/notes/trash'

export async function listTrash(): Promise<TrashItemDto[]> {
  const { data } = await apiClient.get<TrashItemDto[]>(TRASH)
  return data
}

export async function restoreFromTrash(id: string): Promise<PageDto> {
  const { data } = await apiClient.post<PageDto>(`${TRASH}/${id}/restore`)
  return data
}

/** Deletes one page in the trash for good, or empties the trash with no id; answers how many went. */
export async function deleteFromTrash(id: string | null): Promise<number> {
  const { data } = await apiClient.delete<number>(id ? `${TRASH}/${id}` : TRASH)
  return data
}

/** The page behind a public link; no sign-in needed. */
export async function getPublicPage(token: string): Promise<PublicPageDto> {
  const { data } = await apiClient.get<PublicPageDto>(`/api/v1/public/notes/pages/${encodeURIComponent(token)}`)
  return data
}
