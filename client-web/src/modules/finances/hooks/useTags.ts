import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { CreateTagRequest, TaggableEntityType, UpdateTagRequest } from '../models'
import * as tagsService from '../services/tags.service'
import { setAccountTags } from '../services/accounts.service'
import { setCardTags } from '../services/cards.service'
import { setStatementTags } from '../services/statements.service'
import { setTransactionTags } from '../services/transactions.service'

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

export function useEntityTags(entityType: TaggableEntityType, id: string) {
  return useQuery({
    queryKey: financeKeys.entityTags(entityType, id),
    queryFn: () => tagsService.getEntityTags(entityType, id),
    enabled: !!id,
  })
}

const SETTERS: Record<TaggableEntityType, ((id: string, body: { tagIds: string[] }) => Promise<void>) | undefined> = {
  account: setAccountTags,
  card: setCardTags,
  'card-statement': setStatementTags,
  transaction: setTransactionTags,
  'recurring-transaction': undefined,
  'pending-transaction': undefined,
}

/** Replaces an entity's tags; refreshes everything in Finances so tag filters see the change. */
export function useSetEntityTags(entityType: TaggableEntityType, id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (tagIds: string[]) => SETTERS[entityType]!(id, { tagIds }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: financeKeys.all }),
  })
}
