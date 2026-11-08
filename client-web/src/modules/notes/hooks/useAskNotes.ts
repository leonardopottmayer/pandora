import { useMutation } from '@tanstack/react-query'
import type { AskNotesRequest } from '../models'
import { askNotes } from '../services/ai.service'

/** Read-only answer generation; no page queries need invalidation. */
export function useAskNotes() {
  return useMutation({
    mutationFn: ({ body, signal }: { body: AskNotesRequest; signal: AbortSignal }) => askNotes(body, signal),
  })
}
