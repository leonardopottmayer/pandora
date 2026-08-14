// Central query key factory for the notes module — keeps queries and invalidations in sync.
export const noteKeys = {
  all: ['notes'] as const,

  pages: () => [...noteKeys.all, 'pages'] as const,
  pageTree: (includeArchived: boolean) => [...noteKeys.pages(), 'tree', includeArchived] as const,
  page: (id: string) => [...noteKeys.pages(), 'detail', id] as const,
  allBacklinks: () => [...noteKeys.pages(), 'backlinks'] as const,
  backlinks: (id: string) => [...noteKeys.allBacklinks(), id] as const,
  search: (term: string) => [...noteKeys.pages(), 'search', term] as const,
  allGraphs: () => [...noteKeys.pages(), 'graph'] as const,
  graph: () => [...noteKeys.allGraphs(), 'global'] as const,
  localGraph: (id: string, depth: number) => [...noteKeys.allGraphs(), 'local', id, depth] as const,
}
