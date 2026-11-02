// Central query key factory for the notes module — keeps queries and invalidations in sync.
export const noteKeys = {
  all: ['notes'] as const,

  pages: () => [...noteKeys.all, 'pages'] as const,
  pageTree: (includeArchived: boolean, tagIds: string[], spaceId: string | null) =>
    [...noteKeys.pages(), 'tree', spaceId, includeArchived, ...tagIds] as const,
  page: (id: string) => [...noteKeys.pages(), 'detail', id] as const,
  allBacklinks: () => [...noteKeys.pages(), 'backlinks'] as const,
  backlinks: (id: string) => [...noteKeys.allBacklinks(), id] as const,
  search: (term: string, tagIds: string[], spaceId: string | null) =>
    [...noteKeys.pages(), 'search', spaceId, term, ...tagIds] as const,
  allGraphs: () => [...noteKeys.pages(), 'graph'] as const,
  graph: (tagIds: string[], spaceId: string | null) =>
    [...noteKeys.allGraphs(), 'global', spaceId, ...tagIds] as const,
  localGraph: (id: string, depth: number) => [...noteKeys.allGraphs(), 'local', id, depth] as const,

  // Each space's page count moves with page mutations, which invalidate this along with the pages.
  spaces: () => [...noteKeys.all, 'spaces'] as const,
  spaceList: (includeArchived: boolean) => [...noteKeys.spaces(), includeArchived] as const,

  // Tags sit outside the pages subtree, since they outlive any single page. The mutations that can
  // change the list (a save writing a new #tag, a delete sweeping the last one) invalidate it by name.
  tags: () => [...noteKeys.all, 'tags'] as const,

  // A media path addresses immutable bytes, so this entry is never invalidated — it is a download
  // cache, keyed by the path the markdown stores (or a thumbnail's).
  mediaBlob: (path: string) => [...noteKeys.all, 'media-blob', path] as const,

  // The library: its items per filter, and its folder tree with counts.
  media: () => [...noteKeys.all, 'media'] as const,
  mediaList: (filter: { folderId: string | null; unsorted: boolean; kind: string | null; q: string }) =>
    [...noteKeys.media(), 'list', filter] as const,
  mediaFolders: () => [...noteKeys.media(), 'folders'] as const,
}
