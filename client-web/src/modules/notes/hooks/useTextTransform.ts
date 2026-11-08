import { useMutation } from '@tanstack/react-query'
import type { TextTransformRequest } from '../models'
import { summarizePage, transformText } from '../services/ai.service'

/** Generates a preview only; no page data changes until the editor accepts and saves it. */
export function useTextTransform() {
  return useMutation({
    mutationFn: ({ body, signal, pageId }: { body: TextTransformRequest; signal: AbortSignal; pageId?: string }) =>
      pageId ? summarizePage(pageId, signal) : transformText(body, signal),
  })
}
