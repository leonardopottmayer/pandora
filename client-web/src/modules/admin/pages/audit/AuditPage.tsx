import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Segmented, Table, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PageHeading } from '@/components/settings/PageHeading'
import { formatDateTime } from '@/modules/finances/lib/format'
import type { AuditEntryDto } from '../../models'
import { useAudit } from '../../hooks/useAdmin'
import { featureLabel } from '../../lib/labels'
import { AdminError } from '../../components/AdminError'

const TARGETS = ['', 'user', 'role', 'feature'] as const

/** What the change did, in one line, from the before/after JSON the backend recorded. */
function useDescribe() {
  const { t } = useTranslation()
  return (entry: AuditEntryDto): string => {
    const after = entry.after ? JSON.parse(entry.after) : null
    const before = entry.before ? JSON.parse(entry.before) : null
    switch (entry.action) {
      case 'role.assigned':
        return t('admin.audit.roleAssigned', { role: after?.role })
      case 'role.removed':
        return t('admin.audit.roleRemoved', { role: before?.role })
      case 'feature.changed':
        return after?.enabled ? t('admin.audit.featureOn') : t('admin.audit.featureOff')
      case 'user.exceptions-changed':
        return t('admin.audit.exceptionsChanged', { count: Array.isArray(after) ? after.length : 0 })
      default:
        return t(`admin.audit.actions.${entry.action.replaceAll('.', '_')}`, { defaultValue: entry.action })
    }
  }
}

export function AuditPage() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [target, setTarget] = useState<string>('')
  const { data, isLoading, error, fetchNextPage, hasNextPage, isFetchingNextPage } = useAudit(target)
  const describe = useDescribe()
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)

  if (error) return <AdminError error={error} />

  const entries = data?.pages.flat() ?? []
  const targetName = (entry: AuditEntryDto) =>
    entry.targetType === 'feature' ? featureLabel(t, entry.targetId) : (entry.targetName ?? t('admin.audit.gone'))

  const columns: ColumnsType<AuditEntryDto> = [
    {
      title: t('admin.audit.when'),
      dataIndex: 'occurredAt',
      width: 150,
      render: (value: string) => <span className="tabular-nums">{formatDateTime(value)}</span>,
    },
    {
      title: t('admin.audit.change'),
      key: 'change',
      render: (_, entry) => (
        <div className="flex min-w-0 flex-col">
          <span className="truncate">
            {targetName(entry)} — {describe(entry)}
          </span>
          <Typography.Text type="secondary" className="truncate text-xs">
            {entry.actorId ? (entry.actorName ?? t('admin.audit.gone')) : t('admin.audit.system')}
          </Typography.Text>
        </div>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="shrink-0">
        <PageHeading title={t('nav.adminAudit')} description={t('admin.audit.subtitle')} />
      </div>
      <div className="shrink-0">
        <Segmented
          value={target}
          onChange={(v) => setTarget(v as string)}
          options={TARGETS.map((value) => ({ value, label: t(`admin.audit.targets.${value || 'all'}`) }))}
        />
      </div>
      <div
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
      >
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          <Table
            rowKey="id"
            tableLayout="fixed"
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            loading={isLoading}
            dataSource={entries}
            columns={columns}
            pagination={false}
            locale={{ emptyText: t('admin.audit.empty') }}
          />
          {hasNextPage && (
            <div className="flex justify-center p-3">
              <Button loading={isFetchingNextPage} onClick={() => void fetchNextPage()}>
                {t('admin.audit.loadMore')}
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
