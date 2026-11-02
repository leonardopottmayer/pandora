import type { Root } from '../models'

export { formatBytes } from '@/lib/formatBytes'

/** Where a catalog path is on its device, written the way that device writes paths (`E:\Movies\a.mkv`). */
export function localPathOf(root: Root, relativePath: string): string {
  const separator = root.localPath.includes('\\') ? '\\' : '/'
  const base = root.localPath.replace(/[\\/]+$/, '')
  return relativePath === '/' ? root.localPath : base + relativePath.replaceAll('/', separator)
}

/** `382` → `"6:22"`, `7200` → `"2:00:00"`. */
export function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = String(seconds % 60).padStart(2, '0')
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${s}` : `${m}:${s}`
}

/** `"03:00:00"` → `"03:00"`. */
export function shortTime(time: string): string {
  return time.slice(0, 5)
}
