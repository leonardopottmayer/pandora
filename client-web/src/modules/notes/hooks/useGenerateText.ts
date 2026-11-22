import { useMutation } from '@tanstack/react-query'
import { generateText } from '../services/ai.service'

/** Generates a temporary preview; the page changes only when the user accepts it. */
export function useGenerateText() {
  return useMutation({
    mutationFn: ({ prompt, signal }: { prompt: string; signal: AbortSignal }) => generateText(prompt, signal),
  })
}
