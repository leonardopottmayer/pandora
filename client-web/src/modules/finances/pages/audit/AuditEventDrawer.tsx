import { useTranslation } from 'react-i18next'
import { Drawer, Typography } from 'antd'
import type { AuditEventDto } from '../../models'
import { formatDateTime } from '../../lib/format'
import { usePreferences } from '@/modules/identity/context/preferences-context'

function prettyJson(data: string) {
  try { return JSON.stringify(JSON.parse(data), null, 2) }
  catch { return data }
}

export function AuditEventDrawer({ event, onClose }: { event: AuditEventDto | null; onClose: () => void }) {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const details: [string, string | null][] = event ? [
    ['finances.audit.eventId', event.id],
    ['finances.audit.occurredAt', formatDateTime(event.occurredAt, timeZone)],
    ['finances.audit.eventCode', event.eventType],
    ['finances.audit.entityType', t(`finances.audit.entityTypes.${event.entityType}`, { defaultValue: event.entityType })],
    ['finances.audit.entityId', event.entityId],
    ['finances.audit.actorUserId', event.actorUserId],
    ['finances.audit.correlationId', event.correlationId],
  ] : []
  return <Drawer title={t('finances.audit.details')} open={!!event} onClose={onClose} size={560} destroyOnHidden>
    {event && <div className="flex min-w-0 flex-col gap-6">
      <Typography.Title level={5} className="!m-0 break-words">{t(`finances.audit.events.${event.eventType}`, { defaultValue: event.eventType })}</Typography.Title>
      <dl className="m-0 flex min-w-0 flex-col gap-4">
        {details.map(([label, value]) => <div key={label}>
          <dt><Typography.Text type="secondary" className="text-xs">{t(label)}</Typography.Text></dt>
          <dd className="m-0 break-all">{value || '—'}</dd>
        </div>)}
      </dl>
      <div className="min-w-0">
        <Typography.Text type="secondary">{t('finances.audit.data')}</Typography.Text>
        {event.data ? <pre className="mt-2 whitespace-pre-wrap break-all text-xs">{prettyJson(event.data)}</pre>
          : <Typography.Paragraph className="mt-2" type="secondary">{t('finances.audit.noData')}</Typography.Paragraph>}
      </div>
    </div>}
  </Drawer>
}
