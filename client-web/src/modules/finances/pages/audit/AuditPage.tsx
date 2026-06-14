import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Card, Empty, Input, Segmented, Select, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { AuditEventDto } from '../../models'
import type { AuditTimelineParams } from '../../services/audit.service'
import { formatDateTime } from '../../lib/format'
import { useAuditTimeline } from '../../hooks/useAudit'
import { EntityIdSelect } from './EntityIdSelect'

// Valores reais que a auditoria grava em entity_type (vocabulário próprio, distinto do TaggableEntityType).
const ENTITY_TYPES = ['account', 'card', 'statement', 'transaction', 'user-category', 'tag']

type Mode = 'entity' | 'correlation'

function prettyJson(data: string | null): string {
  if (!data) return ''
  try {
    return JSON.stringify(JSON.parse(data), null, 2)
  } catch {
    return data
  }
}

export function AuditPage() {
  const { t } = useTranslation()
  const [mode, setMode] = useState<Mode>('entity')
  const [entityType, setEntityType] = useState<string | undefined>('account')
  const [entityId, setEntityId] = useState('')
  const [correlationId, setCorrelationId] = useState('')

  // O backend exige (entityType + entityId) OU correlationId. Monta o filtro só quando válido.
  const validEntity = mode === 'entity' && !!entityType && entityId.trim().length > 0
  const validCorrelation = mode === 'correlation' && correlationId.trim().length > 0
  const enabled = validEntity || validCorrelation

  const params: AuditTimelineParams = validEntity
    ? { entityType, entityId: entityId.trim(), take: 100 }
    : validCorrelation
      ? { correlationId: correlationId.trim(), take: 100 }
      : { take: 100 }

  const { data, isLoading, isError, error } = useAuditTimeline(params, enabled)

  // Rótulo localizado do tipo de entidade; cai para o valor cru se não houver tradução.
  const entityTypeLabel = (value: string) =>
    t(`finances.audit.entityTypes.${value}`, { defaultValue: value })

  const columns: ColumnsType<AuditEventDto> = [
    {
      title: t('finances.audit.occurredAt'),
      dataIndex: 'occurredAt',
      width: 180,
      render: (value: string) => formatDateTime(value),
    },
    {
      title: t('finances.audit.eventType'),
      dataIndex: 'eventType',
      render: (value: string) => <Tag>{value}</Tag>,
    },
    {
      title: t('finances.audit.entityType'),
      dataIndex: 'entityType',
      render: (value: string) => entityTypeLabel(value),
    },
    { title: t('finances.audit.entityId'), dataIndex: 'entityId', ellipsis: true },
  ]

  return (
    <Card>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        {t('nav.audit')}
      </Typography.Title>

      <Alert
        type="info"
        showIcon
        className="mb-4"
        message={t('finances.audit.filterHint')}
        style={{ marginBottom: 16 }}
      />

      <Space wrap className="mb-4" style={{ width: '100%' }}>
        <Segmented
          value={mode}
          onChange={(value) => setMode(value as Mode)}
          options={[
            { value: 'entity', label: t('finances.audit.byEntity') },
            { value: 'correlation', label: t('finances.audit.byCorrelation') },
          ]}
        />
        {mode === 'entity' ? (
          <>
            <Select
              style={{ minWidth: 180 }}
              placeholder={t('finances.audit.entityType')}
              value={entityType}
              onChange={(value) => {
                setEntityType(value)
                setEntityId('')
              }}
              options={ENTITY_TYPES.map((e) => ({ value: e, label: entityTypeLabel(e) }))}
            />
            <EntityIdSelect entityType={entityType} value={entityId} onChange={setEntityId} />
          </>
        ) : (
          <Input
            allowClear
            style={{ minWidth: 320 }}
            placeholder={t('finances.audit.correlationIdPlaceholder')}
            value={correlationId}
            onChange={(e) => setCorrelationId(e.target.value)}
          />
        )}
      </Space>

      {isError && (
        <Alert type="error" showIcon className="mb-4" message={String((error as Error)?.message ?? '')} />
      )}

      {!enabled ? (
        <Empty description={t('finances.audit.emptyFilter')} />
      ) : (
        <Table
          rowKey="id"
          loading={isLoading}
          dataSource={data}
          columns={columns}
          size="middle"
          pagination={{ pageSize: 25 }}
          expandable={{
            rowExpandable: (record) => !!record.data,
            expandedRowRender: (record) => (
              <pre style={{ margin: 0, whiteSpace: 'pre-wrap' }}>{prettyJson(record.data)}</pre>
            ),
          }}
        />
      )}
    </Card>
  )
}
