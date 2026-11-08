import { useQueries } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import { fetchMediaBlob } from '../services/media.service'

/**
 * Resolves library images into object URLs, so an image block can show them.
 *
 * The URL has to be part of what React renders. Setting `img.src` after the fact — outside React's
 * model — meant any re-render that did not change the page restored the original path, and the
 * image broke: the browser cannot fetch that endpoint without the Bearer token. A theme switch, a
 * language switch and every refetch that rebuilt a prop identity did exactly that.
 *
 * The object URL is made inside the query, so the cache owns it: an item is downloaded once
 * per session instead of on every keystroke, and the URL stays valid for as long as anything may
 * still be showing it. Nothing revokes it — an id addresses immutable bytes, so at most one URL per
 * item exists per session, and the reload that ends the session releases them.
 */
export function useMediaPathUrls(paths: string[]): ReadonlyMap<string, string> {
  return useQueries({
    queries: paths.map((path) => ({
      queryKey: noteKeys.mediaBlob(path),
      queryFn: async () => URL.createObjectURL(await fetchMediaBlob(path)),
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
