import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import * as pagesService from '../services/pages.service'

export function usePageVersions(pageId: string) {
  return useInfiniteQuery({
    queryKey: noteKeys.pageVersions(pageId),
    queryFn: ({ pageParam }) => pagesService.listPageVersions(pageId, pageParam),
    initialPageParam: undefined as number | undefined,
    getNextPageParam: (last) => last.length === 100 ? last.at(-1)?.number : undefined,
    staleTime: 0,
    // Keep the revision the user reviewed. A concurrent change must be rejected by restore,
    // rather than silently accepted because a focus refetch advanced the expected version.
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })
}

export function usePageVersion(pageId: string, number: number | undefined) {
  return useQuery({
    queryKey: noteKeys.pageVersion(pageId, number),
    queryFn: () => pagesService.getPageVersion(pageId, number!),
    enabled: number !== undefined,
    staleTime: Infinity,
  })
}

export function useRestorePageVersion(pageId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ number, expectedVersion }: { number: number; expectedVersion: number }) =>
      pagesService.restorePageVersion(pageId, number, expectedVersion),
    onSuccess: (page) => {
      queryClient.setQueryData(noteKeys.page(pageId), page)
      queryClient.invalidateQueries({ queryKey: noteKeys.pages() })
      queryClient.invalidateQueries({ queryKey: noteKeys.spaces() })
      queryClient.invalidateQueries({ queryKey: noteKeys.tags() })
      queryClient.invalidateQueries({ queryKey: noteKeys.media() })
    },
  })
}
