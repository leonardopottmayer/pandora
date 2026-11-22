import { useQuery } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import { getLinkPreview } from '../services/links.service'

/** A link's preview, read once per session: a page does not change while it is open. */
export function useLinkPreview(url: string | null) {
  return useQuery({
    queryKey: noteKeys.linkPreview(url ?? ''),
    queryFn: () => getLinkPreview(url!),
    enabled: !!url,
    staleTime: Infinity,
    retry: false,
  })
}
