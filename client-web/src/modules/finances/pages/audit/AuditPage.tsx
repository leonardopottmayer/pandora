import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Empty, Input, Segmented, Select, Table, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PageHeading } from '@/components/settings/PageHeading'
import type { AuditEventDto } from '../../models'
import type { AuditTimelineParams } from '../../services/audit.service'
import { formatDateTime } from '../../lib/format'
import { useAllAuditTimeline } from '../../hooks/useAudit'
import { EntityIdSelect } from './EntityIdSelect'
import { AuditEventDrawer } from './AuditEventDrawer'
import { usePreferences } from '@/modules/identity/context/preferences-context'

// The audit log has its own entity vocabulary, distinct from TaggableEntityType.
const ENTITY_TYPES = ['account', 'card', 'statement', 'transaction', 'recurring-transaction', 'pending-transaction', 'user-category', 'tag']
type Mode = 'entity' | 'correlation'

export function AuditPage() {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [mode, setMode] = useState<Mode>('entity')
  const [entityType, setEntityType] = useState('account')
  const [entityId, setEntityId] = useState('')
  const [correlationId, setCorrelationId] = useState('')
  const [selectedEvent, setSelectedEvent] = useState<AuditEventDto | null>(null)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const validEntity = mode === 'entity' && !!entityType && !!entityId.trim()
  const validCorrelation = mode === 'correlation' && !!correlationId.trim()
  const enabled = validEntity || validCorrelation
  const params: Omit<AuditTimelineParams, 'skip' | 'take'> = validEntity
    ? { entityType, entityId: entityId.trim() }
    : validCorrelation ? { correlationId: correlationId.trim() } : {}
  const { data: events = [], isLoading, isError, error } = useAllAuditTimeline(params, enabled)
  const entityLabel = (value: string) => t(`finances.audit.entityTypes.${value}`, { defaultValue: value })
  const columns: ColumnsType<AuditEventDto> = [
    {
      title: t('finances.audit.eventType'), dataIndex: 'eventType',
      render: (value: string, event) => <div className="flex min-w-0 flex-col gap-1">
        <Button type="link" className="!h-auto !max-w-full !justify-start !p-0" onClick={() => setSelectedEvent(event)}>
          <span className="truncate">{t(`finances.audit.events.${value}`, { defaultValue: value })}</span>
        </Button>
        <Typography.Text type="secondary" className="truncate text-xs" title={event.entityId}>{entityLabel(event.entityType)} · {event.entityId}</Typography.Text>
        <Typography.Text type="secondary" className="text-xs tabular-nums md:hidden">{formatDateTime(event.occurredAt, timeZone)}</Typography.Text>
      </div>,
    },
    { title: t('finances.audit.occurredAt'), dataIndex: 'occurredAt', width: 180, responsive: ['md'], render: (value: string) => <span className="tabular-nums">{formatDateTime(value, timeZone)}</span> },
  ]
  return (
    <div className="flex min-w-0 flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="shrink-0"><PageHeading title={t('nav.audit')} description={t('finances.audit.subtitle')} /></div>
      <div className="flex shrink-0 flex-col gap-3">
        <Segmented<Mode> className="w-fit max-w-full" value={mode} onChange={(value) => { setMode(value); setSelectedEvent(null) }} options={[
          { value: 'entity', label: t('finances.audit.byEntity') }, { value: 'correlation', label: t('finances.audit.byCorrelation') },
        ]} />
        <div className="flex min-w-0 flex-wrap items-start gap-2">
          {/* Preserve the card/statement selection while using correlation mode. */}
          <div style={{ display: mode === 'entity' ? 'contents' : 'none' }}>
            <Select className="w-full sm:w-52" aria-label={t('finances.audit.entityType')} value={entityType}
              onChange={(value) => { setEntityType(value); setEntityId(''); setSelectedEvent(null) }}
              options={ENTITY_TYPES.map((value) => ({ value, label: entityLabel(value) }))} />
            <div className="w-full min-w-0 sm:max-w-xl sm:flex-1">
              <EntityIdSelect entityType={entityType} value={entityId} onChange={(value) => { setEntityId(value); setSelectedEvent(null) }} />
            </div>
          </div>
          {mode === 'correlation' && <div className="w-full sm:max-w-xl">
            <Input allowClear aria-label={t('finances.audit.correlationId')} placeholder={t('finances.audit.correlationIdPlaceholder')} value={correlationId}
              onChange={(event) => { setCorrelationId(event.target.value); setSelectedEvent(null) }} />
          </div>}
        </div>
      </div>
      {isError && <Alert type="error" showIcon title={error.message} className="shrink-0" />}
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          {!enabled ? <Empty className="!my-12" description={t('finances.audit.emptyFilter')} /> : <Table rowKey="id" loading={isLoading} dataSource={events} columns={columns} size="middle" tableLayout="fixed" pagination={false}
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            onRow={(event) => ({ onClick: () => setSelectedEvent(event), className: 'cursor-pointer' })}
            locale={{ emptyText: t('finances.audit.empty') }} />}
        </div>
        {enabled && <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>{t('finances.audit.count', { count: events.length })}</Typography.Text>}
      </div>
      <AuditEventDrawer event={selectedEvent} onClose={() => setSelectedEvent(null)} />
    </div>
  )
}
