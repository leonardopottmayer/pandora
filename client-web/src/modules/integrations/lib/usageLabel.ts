import type { TFunction } from 'i18next'
import type { AccountUsage } from '../models'

/** "Assistant · chat", "Email · me@example.com": the module/purpose keys translated, plus the detail. */
export function usageLabel(t: TFunction, usage: AccountUsage): string {
  const base = t(`connections.usage.${usage.module}.${usage.purpose}`, {
    defaultValue: `${usage.module} · ${usage.purpose}`,
  })
  return usage.detail ? `${base} · ${usage.detail}` : base
}
