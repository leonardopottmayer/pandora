import type { TFunction } from 'i18next'

// The catalog's keys come from the backend; their names live here, with the raw segment as fallback.
const MODULE_NAV: Record<string, string> = {
  finances: 'nav.finances',
  notes: 'nav.notes',
  agenda: 'nav.agenda',
  channels: 'nav.channels',
  communications: 'nav.mail',
  files: 'nav.files',
  integrations: 'nav.integrations',
  assistant: 'nav.assistant',
  admin: 'nav.admin',
}

export function moduleLabel(t: TFunction, module: string): string {
  return MODULE_NAV[module] ? t(MODULE_NAV[module]) : module
}

export function resourceLabel(t: TFunction, module: string, resource: string): string {
  return t(`admin.resources.${module}.${resource}`, { defaultValue: resource })
}

export function actionLabel(t: TFunction, action: string): string {
  return t(`admin.actions.${action}`, { defaultValue: action })
}

/** `finances.import` → its own name; a module flag → the module's name. */
export function featureLabel(t: TFunction, key: string): string {
  return MODULE_NAV[key] ? t(MODULE_NAV[key]) : t(`admin.featureNames.${key.replaceAll('.', '_')}`, { defaultValue: key })
}

/** A permission key in words: "Finances · Cards · Delete". Wildcards read as "all". */
export function permissionLabel(t: TFunction, key: string): string {
  if (key === '*') return t('admin.everything')
  const [module, resource, action] = key.split('.')
  const parts = [moduleLabel(t, module)]
  if (resource) parts.push(resource === '*' ? t('admin.all') : resourceLabel(t, module, resource))
  if (action) parts.push(action === '*' ? t('admin.all') : actionLabel(t, action))
  return parts.join(' · ')
}
