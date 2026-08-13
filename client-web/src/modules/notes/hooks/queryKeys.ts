// Central query key factory for the notes module — keeps queries and invalidations in sync.
export const noteKeys = {
  all: ['notes'] as const,

  pages: () => [...noteKeys.all, 'pages'] as const,
  pageTree: (includeArchived: boolean) => [...noteKeys.pages(), 'tree', includeArchived] as const,
  page: (id: string) => [...noteKeys.pages(), 'detail', id] as const,
}
