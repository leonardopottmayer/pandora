import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Checkbox, Input, Tag, theme } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import type { CatalogModule } from '../../models'
import { actionLabel, moduleLabel, permissionLabel, resourceLabel } from '../../lib/labels'
import { missingDependencies } from '../../lib/patterns'

interface PermissionMatrixProps {
  catalog: CatalogModule[]
  value: ReadonlySet<string>
  onChange?: (keys: Set<string>) => void
  /** The admin role: everything, and nothing to change. */
  readOnly?: boolean
  /** Features switched on in the system; resources under one that is off are greyed. */
  isFeatureOn: (feature: string) => boolean
}

/**
 * Module → resource in rows, the resource's actions beside it. A module or resource checkbox ticks
 * everything under it (and shows a dash when only part is ticked).
 */
export function PermissionMatrix({ catalog, value, onChange, readOnly, isFeatureOn }: PermissionMatrixProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [search, setSearch] = useState('')

  const term = search.trim().toLowerCase()
  const modules = catalog
    .map((mod) => ({
      ...mod,
      resources: mod.resources.filter(
        (r) =>
          !term ||
          moduleLabel(t, mod.key).toLowerCase().includes(term) ||
          resourceLabel(t, mod.key, r.key).toLowerCase().includes(term),
      ),
    }))
    .filter((mod) => mod.resources.length > 0)

  function set(keys: string[], checked: boolean) {
    const next = new Set(value)
    keys.forEach((k) => (checked ? next.add(k) : next.delete(k)))
    onChange?.(next)
  }

  function groupBox(keys: string[], label: string) {
    const held = keys.filter((k) => value.has(k)).length
    return (
      <Checkbox
        checked={held === keys.length}
        indeterminate={held > 0 && held < keys.length}
        disabled={readOnly}
        onChange={(e) => set(keys, e.target.checked)}
        aria-label={label}
      />
    )
  }

  const missing = missingDependencies(value, catalog)

  return (
    <div className="flex flex-col gap-3">
      <Input
        allowClear
        prefix={<SearchOutlined />}
        placeholder={t('admin.roles.searchPermissions')}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="w-full sm:w-64"
      />

      {missing.length > 0 && (
        <Alert
          type="warning"
          showIcon
          title={t('admin.roles.missingRead')}
          description={missing.map((key) => permissionLabel(t, key)).join(', ')}
        />
      )}

      <div className="overflow-hidden rounded-lg" style={{ border: `1px solid ${token.colorBorderSecondary}` }}>
        {modules.map((mod) => {
          const moduleKeys = mod.resources.flatMap((r) => r.actions.map((a) => a.key))
          return (
            <div key={mod.key}>
              <div
                className="flex items-center gap-2 border-x-0 border-b border-t-0 border-solid px-3 py-2 text-sm font-semibold"
                style={{
                  borderColor: token.colorBorderSecondary,
                  background: token.colorBgContainer,
                  boxShadow: `inset 0 0 0 9999px ${token.colorFillAlter}`,
                }}
              >
                {groupBox(moduleKeys, moduleLabel(t, mod.key))}
                {moduleLabel(t, mod.key)}
              </div>
              {mod.resources.map((resource) => {
                const off = !!resource.feature && !isFeatureOn(resource.feature)
                const name = resourceLabel(t, mod.key, resource.key)
                return (
                  <div
                    key={resource.key}
                    className="flex flex-col gap-2 border-x-0 border-b border-t-0 border-solid px-3 py-2 sm:flex-row sm:items-center"
                    style={{ borderColor: token.colorBorderSecondary, opacity: off ? 0.55 : 1 }}
                  >
                    <div className="flex min-w-0 items-center gap-2 pl-4 sm:w-56 sm:shrink-0">
                      {groupBox(resource.actions.map((a) => a.key), `${moduleLabel(t, mod.key)} · ${name}`)}
                      <span className="truncate text-sm">{name}</span>
                      {off && <Tag>{t('admin.roles.featureOff')}</Tag>}
                    </div>
                    <div className="flex flex-wrap gap-x-4 gap-y-1 pl-10 sm:pl-0">
                      {resource.actions.map((action) => (
                        <Checkbox
                          key={action.key}
                          checked={value.has(action.key)}
                          disabled={readOnly}
                          onChange={(e) => set([action.key], e.target.checked)}
                          aria-label={permissionLabel(t, action.key)}
                        >
                          <span className="text-sm">{actionLabel(t, action.action)}</span>
                        </Checkbox>
                      ))}
                    </div>
                  </div>
                )
              })}
            </div>
          )
        })}
      </div>
    </div>
  )
}
