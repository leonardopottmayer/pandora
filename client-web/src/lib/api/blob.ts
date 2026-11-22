import { apiClient } from './client'

/**
 * Fetches a file's bytes through an authenticated API route. Those routes require the Bearer token,
 * so a plain `<img src>` or link would get a 401 — callers turn this blob into an object URL instead.
 * Routes that serve immutable bytes (the notes media) are cached by the browser, so fetching the same
 * path again does not download it again.
 */
export async function fetchBlob(url: string): Promise<Blob> {
  const { data } = await apiClient.get<Blob>(url, { responseType: 'blob' })
  return data
}

/**
 * Saves a file to disk. A plain click on a link to the API cannot do it: the browser would navigate
 * the current tab to an endpoint that answers 401 without the Bearer token (and, in dev, is not even
 * the same origin as the app). Fetching the blob here and handing it to a throwaway anchor keeps the
 * download in place, with no tab involved.
 */
export async function downloadBlob(url: string, fileName: string): Promise<void> {
  const blob = await fetchBlob(url)
  const objectUrl = URL.createObjectURL(blob)

  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()

  // The click starts the save synchronously, but revoking in the same tick races it in some browsers.
  setTimeout(() => URL.revokeObjectURL(objectUrl), 0)
}
