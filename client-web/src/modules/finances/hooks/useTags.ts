import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { CreateTagRequest, UpdateTagRequest } from '../models'
import * as tagsService from '../services/tags.service'

export function useTags() {
  return useQuery({
    queryKey: financeKeys.tagList(),
    queryFn: tagsService.listTags,
  })
}

export function useTagLinks(id: string) {
  return useQuery({
    queryKey: financeKeys.tagLinks(id),
    queryFn: () => tagsService.getTagLinks(id),
    enabled: !!id,
  })
}

function useInvalidateTags() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: financeKeys.tags() })
}

export function useCreateTag() {
  const invalidate = useInvalidateTags()
  return useMutation({
    mutationFn: (body: CreateTagRequest) => tagsService.createTag(body),
    onSuccess: invalidate,
  })
}

export function useUpdateTag() {
  const invalidate = useInvalidateTags()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTagRequest }) =>
      tagsService.updateTag(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteTag() {
  const invalidate = useInvalidateTags()
  return useMutation({
    mutationFn: (id: string) => tagsService.deleteTag(id),
    onSuccess: invalidate,
  })
}
