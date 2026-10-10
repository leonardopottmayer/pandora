import type { Root } from '../models'

export function formatBytes(bytes: number): string {
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${unit === 0 ? value : value.toFixed(1)} ${units[unit]}`
}

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
