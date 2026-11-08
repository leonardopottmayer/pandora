// Callout tones: the block's colour, icon and default title.

export const CALLOUT_TYPES = ['note', 'info', 'success', 'warning', 'error'] as const

export type CalloutType = (typeof CALLOUT_TYPES)[number]

export const CALLOUT_ICONS: Record<CalloutType, string> = {
  note: '💡',
  info: 'ℹ️',
  success: '✅',
  warning: '⚠️',
  error: '⛔',
}
