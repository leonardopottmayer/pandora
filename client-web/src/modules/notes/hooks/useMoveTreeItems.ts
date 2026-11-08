import { useMutation, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import { moveTreeItems } from '../services/tree.service'

/** A drop is one atomic write; wait for the refreshed hierarchy before enabling the next drop. */
export function useMoveTreeItems() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: moveTreeItems,
    onSettled: () => Promise.all([
      client.invalidateQueries({ queryKey: noteKeys.pages() }),
      client.invalidateQueries({ queryKey: noteKeys.folders() }),
      client.invalidateQueries({ queryKey: noteKeys.spaces() }),
    ]),
  })
}
