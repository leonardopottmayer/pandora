// Central query key factory for the notes module — keeps queries and invalidations in sync.
export const noteKeys = {
  all: ['notes'] as const,

  pages: () => [...noteKeys.all, 'pages'] as const,
  pageTree: (includeArchived: boolean, tagIds: string[]) =>
    [...noteKeys.pages(), 'tree', includeArchived, ...tagIds] as const,
  page: (id: string) => [...noteKeys.pages(), 'detail', id] as const,
  allBacklinks: () => [...noteKeys.pages(), 'backlinks'] as const,
  backlinks: (id: string) => [...noteKeys.allBacklinks(), id] as const,
  search: (term: string, tagIds: string[]) =>
    [...noteKeys.pages(), 'search', term, ...tagIds] as const,
  allGraphs: () => [...noteKeys.pages(), 'graph'] as const,
  graph: (tagIds: string[]) => [...noteKeys.allGraphs(), 'global', ...tagIds] as const,
  localGraph: (id: string, depth: number) => [...noteKeys.allGraphs(), 'local', id, depth] as const,

  // Tags sit outside the pages subtree, since they outlive any single page. The mutations that can
  // change the list (a save writing a new #tag, a delete sweeping the last one) invalidate it by name.
  tags: () => [...noteKeys.all, 'tags'] as const,
}
