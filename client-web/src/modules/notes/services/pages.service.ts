import { apiClient } from '@/lib/api/client'
import type {
  BacklinkDto,
  CreatePageRequest,
  MovePageRequest,
  PageDto,
  PageGraphDto,
  PageSearchResultDto,
  PageSummaryDto,
  PageVersionDto,
  PageVersionSummaryDto,
  UpdatePageRequest,
} from '../models'

const BASE = '/api/v1/notes/pages'

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
