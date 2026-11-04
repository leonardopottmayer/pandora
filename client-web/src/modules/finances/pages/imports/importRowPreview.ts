import type { ImportRowDto } from '../../models'
import type { FlowDirection } from '../../lib/enums'

export interface ImportRowPreview {
  description: string
  occurredOn?: string
  amount?: number
  currency?: string
  direction?: FlowDirection
}

/** Read the parser's PascalCase snapshot, retaining the original row when it cannot be decoded. */
export function importRowPreview(row: ImportRowDto): ImportRowPreview {
  const fallback = { description: row.rawData }
  if (!row.parsedPayload) return fallback
  try {
    const value: unknown = JSON.parse(row.parsedPayload)
    if (!value || typeof value !== 'object' || Array.isArray(value)) return fallback
    const data = value as Record<string, unknown>
    let direction: FlowDirection | undefined
    if (data.IsCredit === true) direction = 'in'
    if (data.IsCredit === false) direction = 'out'
    return {
      description: typeof data.Description === 'string' && data.Description.trim() ? data.Description : row.rawData,
      occurredOn: typeof data.OccurredOn === 'string' ? data.OccurredOn : undefined,
      amount: typeof data.Amount === 'number' && Number.isFinite(data.Amount) ? data.Amount : undefined,
      currency: typeof data.Currency === 'string' ? data.Currency : undefined,
      direction,
    }
  } catch {
    return fallback
  }
}

/** Keep non-JSON input intact when showing the parser snapshot. */
export function formatImportPayload(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2) }
  catch { return value }
}
