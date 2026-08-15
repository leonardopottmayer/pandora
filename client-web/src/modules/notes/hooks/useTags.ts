import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import { buildTagIndex, type TagIndex } from '../lib/tags'
import * as tagsService from '../services/tags.service'

/** The user's tags, plus the slug→tag index the preview paints the chips with. */
export function useTags() {
  const query = useQuery({
    queryKey: noteKeys.tags(),
    queryFn: () => tagsService.listTags(),
  })
  const index: TagIndex = useMemo(() => buildTagIndex(query.data ?? []), [query.data])
  return { ...query, tags: query.data ?? [], index }
}

export function useSetTagColor() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, color }: { id: string; color: string | null }) =>
      tagsService.setTagColor(id, color),
    // The color shows on the chips inside the pages, so the previews have to catch up too.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: noteKeys.tags() }),
  })
}
