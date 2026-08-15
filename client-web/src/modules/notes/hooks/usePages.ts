import { useMemo } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import { buildTree } from '../lib/buildTree'
import type { CreatePageRequest, MovePageRequest, PageTreeNode, UpdatePageRequest } from '../models'
import * as pagesService from '../services/pages.service'

export function usePageTree(includeArchived = false, tagIds: string[] = []) {
  const query = useQuery({
    queryKey: noteKeys.pageTree(includeArchived, tagIds),
    queryFn: () => pagesService.listPages(includeArchived, tagIds),
  })
  const tree: PageTreeNode[] = useMemo(() => buildTree(query.data ?? []), [query.data])
  return { ...query, tree }
}

export function usePage(id: string | null) {
  return useQuery({
    queryKey: noteKeys.page(id ?? ''),
    queryFn: () => pagesService.getPage(id!),
    enabled: !!id,
  })
}

export function useBacklinks(id: string | null) {
  return useQuery({
    queryKey: noteKeys.backlinks(id ?? ''),
    queryFn: () => pagesService.getBacklinks(id!),
    enabled: !!id,
  })
}

/** The whole wiki graph — nodes and edges for the global graph view, optionally cut by tag. */
export function useGraph(tagIds: string[] = []) {
  return useQuery({
    queryKey: noteKeys.graph(tagIds),
    queryFn: () => pagesService.getGraph(tagIds),
  })
}

/** The neighborhood of one page, for the local graph panel. */
export function useLocalGraph(id: string | null, depth: number) {
  return useQuery({
    queryKey: noteKeys.localGraph(id ?? '', depth),
    queryFn: () => pagesService.getLocalGraph(id!, depth),
    enabled: !!id,
  })
}

/**
 * Full-text search for the command palette. The term is expected to be debounced by the caller;
 * previous results stay on screen while the next ones load, so the list does not blink per keystroke.
 */
export function useSearchPages(term: string, tagIds: string[] = []) {
  return useQuery({
    queryKey: noteKeys.search(term, tagIds),
    queryFn: () => pagesService.searchPages(term, tagIds),
    // With tags picked, an empty term is not an empty search: it browses those tags' pages.
    enabled: term.trim().length > 0 || tagIds.length > 0,
    placeholderData: keepPreviousData,
  })
}

/** Invalidates every notes query after a structural mutation (create/move/archive/delete). */
function useInvalidatePages() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: noteKeys.pages() })
}

/**
 * The mutations that write or drop content also decide which tags exist: a body mentioning a new
 * `#tag` creates it, and deleting the last page that carried one sweeps it away.
 */
function useInvalidateTags() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: noteKeys.tags() })
}

export function useCreatePage() {
  const invalidate = useInvalidatePages()
  const invalidateTags = useInvalidateTags()
  return useMutation({
    mutationFn: (body: CreatePageRequest) => pagesService.createPage(body),
    onSuccess: () => {
      invalidate()
      invalidateTags()
    },
  })
}

export function useUpdatePage() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdatePageRequest }) =>
      pagesService.updatePage(id, body),
    // Autosave path: refresh the tree so a renamed title shows in the sidebar, and seed the
    // detail cache with the saved page. We do NOT invalidate the detail query — that would
    // refetch and clobber what the user is still typing.
    onSuccess: (page) => {
      queryClient.setQueryData(noteKeys.page(page.id), page)
      queryClient.invalidateQueries({ queryKey: noteKeys.pages(), refetchType: 'none' })
      queryClient.invalidateQueries({ queryKey: [...noteKeys.pages(), 'tree'] })
      // A save rewrites this page's outgoing links, so any other page's backlinks — and the
      // shape of the graph — may have changed.
      queryClient.invalidateQueries({ queryKey: noteKeys.allBacklinks() })
      queryClient.invalidateQueries({ queryKey: noteKeys.allGraphs() })
      // The same save may have written the first #tag of a new one, or dropped the last of an old.
      queryClient.invalidateQueries({ queryKey: noteKeys.tags() })
    },
  })
}

export function useMovePage() {
  const invalidate = useInvalidatePages()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: MovePageRequest }) =>
      pagesService.movePage(id, body),
    onSuccess: invalidate,
  })
}

/**
 * Applies a batch of moves in one shot. The backend orders siblings by the raw `orderIndex`
 * (ties broken by id), so a drag-and-drop renumbers the whole destination sibling list to make
 * the dropped position stick — hence a batch rather than a single move.
 */
export function useReorderPages() {
  const invalidate = useInvalidatePages()
  return useMutation({
    mutationFn: (moves: { id: string; body: MovePageRequest }[]) =>
      Promise.all(moves.map((m) => pagesService.movePage(m.id, m.body))),
    onSuccess: invalidate,
  })
}

export function useSetPageFavorite() {
  const invalidate = useInvalidatePages()
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) =>
      pagesService.setPageFavorite(id, favorite),
    onSuccess: invalidate,
  })
}

export function useSetPageArchived() {
  const invalidate = useInvalidatePages()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) =>
      pagesService.setPageArchived(id, archived),
    onSuccess: invalidate,
  })
}

export function useDeletePage() {
  const invalidate = useInvalidatePages()
  const invalidateTags = useInvalidateTags()
  return useMutation({
    mutationFn: (id: string) => pagesService.deletePage(id),
    onSuccess: () => {
      invalidate()
      invalidateTags()
    },
  })
}
