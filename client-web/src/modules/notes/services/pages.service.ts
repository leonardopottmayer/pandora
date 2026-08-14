import { apiClient } from '@/lib/api/client'
import type {
  BacklinkDto,
  CreatePageRequest,
  MovePageRequest,
  PageDto,
  PageGraphDto,
  PageSearchResultDto,
  PageSummaryDto,
  UpdatePageRequest,
} from '../models'

const BASE = '/api/v1/notes/pages'

export async function listPages(includeArchived = false): Promise<PageSummaryDto[]> {
  const { data } = await apiClient.get<PageSummaryDto[]>(BASE, { params: { includeArchived } })
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

export async function searchPages(term: string): Promise<PageSearchResultDto[]> {
  const { data } = await apiClient.get<PageSearchResultDto[]>(`${BASE}/search`, { params: { q: term } })
  return data
}

/** The whole wiki graph of the user. */
export async function getGraph(): Promise<PageGraphDto> {
  const { data } = await apiClient.get<PageGraphDto>(`${BASE}/graph`)
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
