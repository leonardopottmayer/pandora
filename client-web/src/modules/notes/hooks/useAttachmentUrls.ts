import { useQueries } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import { parseAttachmentImagePaths } from '../lib/attachments'
import { fetchAttachmentBlob } from '../services/attachments.service'

/**
 * Resolves the images a page embeds into object URLs, so the rendered HTML can carry them.
 *
 * The URL has to be part of what React renders. Setting `img.src` after the fact — outside React's
 * model — meant any re-render that did not change the markdown restored the original path, and the
 * image broke: the browser cannot fetch that endpoint without the Bearer token. A theme switch, a
 * language switch and every refetch that rebuilt a prop identity did exactly that.
 *
 * The object URL is made inside the query, so the cache owns it: an attachment is downloaded once
 * per session instead of on every keystroke, and the URL stays valid for as long as anything may
 * still be showing it. Nothing revokes it — an id addresses immutable bytes, so at most one URL per
 * attachment exists per session, and the reload that ends the session releases them.
 */
export function useAttachmentUrls(markdown: string): ReadonlyMap<string, string> {
  const paths = parseAttachmentImagePaths(markdown)

  return useQueries({
    queries: paths.map((path) => ({
      queryKey: noteKeys.attachment(path),
      queryFn: async () => URL.createObjectURL(await fetchAttachmentBlob(path)),
      staleTime: Infinity,
      gcTime: Infinity,
      retry: false,
    })),
    // Memoized by react-query: the map keeps its identity while the results do not change, which is
    // what keeps the rendered HTML from being rebuilt on every render.
    combine: (results) => {
      const urls = new Map<string, string>()
      results.forEach((result, index) => {
        if (result.data) urls.set(paths[index], result.data)
      })
      return urls
    },
  })
}
