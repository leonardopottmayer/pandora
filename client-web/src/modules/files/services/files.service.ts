import { apiClient } from '@/lib/api/client'
import type {
  Entry,
  Filter,
  FilterInput,
  FilterPreview,
  FilesPreferences,
  Mark,
  NewRoot,
  Page,
  ReviewDecision,
  ReviewNode,
  Root,
  RootSettings,
  Scan,
  SearchCriteria,
} from '../models'

const BASE = '/api/v1/files'

/** How many rows a page asks for; the backend caps it at 200. */
export const PAGE_SIZE = 100

export async function getPreferences(): Promise<FilesPreferences> {
  const { data } = await apiClient.get<FilesPreferences>(`${BASE}/preferences`)
  return data
}

export async function savePreferences(isEnabled: boolean): Promise<void> {
  await apiClient.put(`${BASE}/preferences`, { isEnabled })
}

export async function listRoots(): Promise<Root[]> {
  const { data } = await apiClient.get<Root[]>(`${BASE}/roots`)
  return data
}

export async function addRoot(root: NewRoot): Promise<Root> {
  const { data } = await apiClient.post<Root>(`${BASE}/roots`, root)
  return data
}

export async function updateRoot(id: string, settings: RootSettings): Promise<Root> {
  const { data } = await apiClient.patch<Root>(`${BASE}/roots/${id}`, settings)
  return data
}

export async function removeRoot(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/roots/${id}`)
}

export async function saveSelection(id: string, marks: Mark[]): Promise<Root> {
  const { data } = await apiClient.put<Root>(`${BASE}/roots/${id}/selection`, { marks })
  return data
}

export async function browse(rootId: string, parentPath: string, skip = 0): Promise<Page<Entry>> {
  const { data } = await apiClient.get<Page<Entry>>(`${BASE}/roots/${rootId}/entries`, {
    params: { parentPath, skip, take: PAGE_SIZE },
  })
  return data
}

export async function search(criteria: SearchCriteria, skip = 0): Promise<Page<Entry>> {
  const { data } = await apiClient.get<Page<Entry>>(`${BASE}/search`, {
    params: { ...criteria, skip, take: PAGE_SIZE },
  })
  return data
}

export async function listFilters(): Promise<Filter[]> {
  const { data } = await apiClient.get<Filter[]>(`${BASE}/filters`)
  return data
}

export async function saveFilter(id: string | null, filter: FilterInput): Promise<Filter> {
  const { data } = id
    ? await apiClient.patch<Filter>(`${BASE}/filters/${id}`, filter)
    : await apiClient.post<Filter>(`${BASE}/filters`, filter)
  return data
}

export async function deleteFilter(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/filters/${id}`)
}

export async function previewFilter(filter: FilterInput): Promise<FilterPreview> {
  const { data } = await apiClient.post<FilterPreview>(`${BASE}/filters/preview`, filter)
  return data
}

export async function listScans(rootId: string): Promise<Scan[]> {
  const { data } = await apiClient.get<Scan[]>(`${BASE}/scans`, { params: { rootId } })
  return data
}

/** Applies (`confirm`) or drops (`discard`) a scan the safety brake held. */
export async function resolveScan(id: string, action: 'confirm' | 'discard'): Promise<void> {
  await apiClient.post(`${BASE}/scans/${id}/${action}`)
}

/** Without a root, the roots that have something to review; with one, the children of a folder. */
export async function reviewTree(rootId?: string, parentPath?: string): Promise<ReviewNode[]> {
  const { data } = await apiClient.get<ReviewNode[]>(`${BASE}/review/tree`, { params: { rootId, parentPath } })
  return data
}

/** Returns how many entries the decision touched. */
export async function decideReview(decision: 'forget' | 'keep', on: ReviewDecision): Promise<number> {
  const { data } = await apiClient.post<number>(`${BASE}/review/${decision}`, on)
  return data
}
