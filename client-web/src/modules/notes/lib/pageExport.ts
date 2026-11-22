import { fetchMediaBlob } from '../services/media.service'
import { mediaPath } from './media'

// Saving a page as a file: Markdown or HTML downloaded, PDF through the browser's print dialog.

/** A title as a file name: no characters a file system refuses, never empty. */
export function fileNameOf(title: string, extension: string): string {
  const base = title.replace(/[\\/:*?"<>|]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 80) || 'page'
  return `${base}.${extension}`
}

export function downloadText(name: string, text: string, type: string) {
  const url = URL.createObjectURL(new Blob([text], { type }))
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  // The click has started the download by the time this runs.
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** A library image as a data URL, so an exported HTML file carries it; null when it cannot be read. */
export async function mediaDataUrl(mediaId: string): Promise<string | null> {
  try {
    const blob = await fetchMediaBlob(mediaPath(mediaId))
    return await new Promise<string>((resolve, reject) => {
      const reader = new FileReader()
      reader.onload = () => resolve(String(reader.result))
      reader.onerror = () => reject(reader.error)
      reader.readAsDataURL(blob)
    })
  } catch {
    return null
  }
}
