import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as commentsService from '../services/comments.service'
import { noteKeys } from './queryKeys'

export function usePageComments(pageId: string | null) {
  return useQuery({
    queryKey: noteKeys.comments(pageId ?? ''),
    queryFn: () => commentsService.listComments(pageId!),
    enabled: !!pageId,
  })
}

export function useBlockStamps(pageId: string | null) {
  return useQuery({
    queryKey: noteKeys.blockStamps(pageId ?? ''),
    queryFn: () => commentsService.listBlockStamps(pageId!),
    enabled: !!pageId,
  })
}

/** Any change to a page's comments; the list is read again after it. */
export function useCommentChange(pageId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (run: () => Promise<unknown>) => run(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: noteKeys.comments(pageId) }),
  })
}
