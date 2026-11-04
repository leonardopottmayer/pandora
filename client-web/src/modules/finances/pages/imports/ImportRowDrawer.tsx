import { useTranslation } from 'react-i18next'
import { Button, Drawer, Space, Typography } from 'antd'
import type { ImportRowDto } from '../../models'
import { formatDate, formatDateTime } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { formatImportPayload, importRowPreview } from './importRowPreview'

interface ImportRowDrawerProps {
  row: ImportRowDto | null
  onClose: () => void
  onLink: (row: ImportRowDto) => void
  onViewTransaction: (id: string) => void
  onViewInbox: () => void
}

export function ImportRowDrawer({ row, onClose, onLink, onViewTransaction, onViewInbox }: ImportRowDrawerProps) {
  const { t } = useTranslation()
  const preview = row ? importRowPreview(row) : null
  const details: [string, string | number | null][] = row ? [
    ['finances.imports.rowStatus', t(`finances.imports.rowStatuses.${row.status}`)],
    ['finances.imports.dedupStatus', t(`finances.imports.dedupStatuses.${row.dedupStatus}`)],
    ['finances.imports.rowId', row.id],
    ['finances.imports.externalId', row.externalId],
    ['finances.imports.dedupKey', row.dedupKey],
    ['finances.imports.suggestionId', row.pendingTransactionId],
    ['finances.imports.matchedTransactionId', row.matchedTransactionId],
    ['finances.imports.matchedSuggestionId', row.matchedPendingTransactionId],
    ['finances.imports.installment', row.installmentNumber && row.installmentCount ? `${row.installmentNumber}/${row.installmentCount}` : null],
    ['finances.imports.rowCreatedAt', formatDateTime(row.createdAt)],
  ] : []
  return <Drawer open={!!row} onClose={onClose} size={560} destroyOnHidden title={t('finances.imports.rowDetails')}
    footer={row && <Space wrap>
      {row.pendingTransactionId && row.dedupStatus !== 'matched' && <Button type="primary" onClick={() => onLink(row)}>{t('finances.imports.link')}</Button>}
      {row.matchedTransactionId && <Button onClick={() => onViewTransaction(row.matchedTransactionId!)}>{t('finances.imports.viewTransaction')}</Button>}
      {(row.pendingTransactionId || row.matchedPendingTransactionId) && <Button onClick={onViewInbox}>{t('finances.imports.viewInbox')}</Button>}
    </Space>}>
    {row && preview && <div className="flex min-w-0 flex-col gap-6">
      <div className="flex min-w-0 flex-col gap-2">
        <Typography.Text type="secondary">{t('finances.imports.rowNumber', { index: row.rowIndex })}</Typography.Text>
        <Typography.Title level={5} className="!m-0 break-words">{preview.description}</Typography.Title>
        {preview.occurredOn && <Typography.Text type="secondary">{formatDate(preview.occurredOn)}</Typography.Text>}
        {preview.amount != null && preview.currency && <CurrencyAmount amount={preview.amount} currency={preview.currency} direction={preview.direction} />}
        {row.errorMessage && <Typography.Text type="danger" className="break-words">{row.errorMessage}</Typography.Text>}
      </div>
      <dl className="m-0 flex min-w-0 flex-col gap-4">
        {details.filter(([, value]) => value != null).map(([label, value]) => <div key={label}>
          <dt><Typography.Text type="secondary" className="text-xs">{t(label)}</Typography.Text></dt>
          <dd className="m-0 break-all">{value}</dd>
        </div>)}
      </dl>
      <div className="min-w-0">
        <Typography.Text type="secondary">{t('finances.imports.rawData')}</Typography.Text>
        <pre className="mt-2 whitespace-pre-wrap break-all text-xs">{row.rawData}</pre>
      </div>
      {row.parsedPayload && <div className="min-w-0">
        <Typography.Text type="secondary">{t('finances.imports.parsedData')}</Typography.Text>
        <pre className="mt-2 whitespace-pre-wrap break-all text-xs">{formatImportPayload(row.parsedPayload)}</pre>
      </div>}
    </div>}
  </Drawer>
}
