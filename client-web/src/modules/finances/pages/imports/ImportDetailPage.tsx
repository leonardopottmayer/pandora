import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Alert, App, Breadcrumb, Button, Empty, Skeleton, Space, Tag, Typography, theme } from 'antd'
import { ReloadOutlined, StopOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { ImportRowDto, ImportStatus } from '../../models'
import { formatDate, formatDateTime } from '../../lib/format'
import { useAbortImportFile, useImportFile, useImportRows, useRetryImportFile } from '../../hooks/useImports'
import { useLinkPendingTransaction } from '../../hooks/usePendingTransactions'
import { StatTile } from '../../components/StatTile'
import { TransactionDetailDrawer } from '../../components/TransactionDetailDrawer'
import { LinkTransactionModal } from '../../components/LinkTransactionModal'
import { importRowPreview } from './importRowPreview'
import { ImportRowDrawer } from './ImportRowDrawer'
import { ImportRowsTable } from './ImportRowsTable'
import { usePreferences } from '@/modules/identity/context/preferences-context'

const STATUS_COLORS: Record<ImportStatus, string> = { received: 'blue', parsing: 'processing', completed: 'green', failed: 'red', aborted: 'default' }

export function ImportDetailPage() {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const { id = '' } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: file, isLoading: fileLoading, isError: fileError, error: fileFailure } = useImportFile(id)
  const { data: rows = [], isLoading: rowsLoading, isError: rowsError, error: rowsFailure } = useImportRows(file ? id : '', file?.status)
  const abortMutation = useAbortImportFile()
  const retryMutation = useRetryImportFile()
  const linkMutation = useLinkPendingTransaction()
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null)
  const [viewTxId, setViewTxId] = useState<string | null>(null)
  const [linkingRow, setLinkingRow] = useState<ImportRowDto | null>(null)

  function startLink(row: ImportRowDto) {
    setSelectedRowId(null)
    setLinkingRow(row)
  }
  function viewTransaction(transactionId: string) {
    setSelectedRowId(null)
    setViewTxId(transactionId)
  }
  const viewInbox = () => navigate('/finances/inbox')

  async function handleLink(transactionId: string) {
    if (!linkingRow?.pendingTransactionId) return
    try {
      await linkMutation.mutateAsync({ id: linkingRow.pendingTransactionId, transactionId })
      message.success(t('finances.imports.linked'))
      setLinkingRow(null)
    } catch (error) {
      message.error(toErrorMessage(error, t('finances.imports.actionError')))
    }
  }
  function confirmAbort() {
    modal.confirm({
      title: t('finances.imports.abortConfirm'), content: file?.fileName,
      okText: t('finances.imports.abort'), okButtonProps: { danger: true }, cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await abortMutation.mutateAsync(id)
          message.success(t('finances.imports.aborted'))
        } catch (error) {
          message.error(toErrorMessage(error, t('finances.imports.actionError')))
          throw error
        }
      },
    })
  }
  async function handleRetry() {
    try {
      await retryMutation.mutateAsync(id)
      message.success(t('finances.imports.retried'))
    } catch (error) {
      message.error(toErrorMessage(error, t('finances.imports.actionError')))
    }
  }

  if (fileLoading) return <Skeleton active />
  if (fileError) return <Alert type="error" title={fileFailure.message} showIcon />
  if (!file) return <Empty description={t('finances.imports.notFound')}><Link to="/finances/imports">{t('finances.imports.title')}</Link></Empty>
  const processing = file.status === 'received' || file.status === 'parsing'
  const metadata: [string, string | number | null][] = [
    ['finances.imports.createdAt', formatDateTime(file.createdAt, timeZone)],
    ['finances.imports.completedAt', file.completedAt ? formatDateTime(file.completedAt, timeZone) : null],
    ['finances.imports.cutoffDate', file.cutoffDate ? formatDate(file.cutoffDate) : null],
    [file.accountId ? 'finances.imports.account' : 'finances.imports.card', file.accountId ?? file.cardId],
    ['finances.imports.retryCount', file.retryCount],
    ['finances.audit.correlationId', file.correlationId],
  ]
  return <div className="flex min-w-0 flex-col gap-6 lg:h-[calc(100dvh-112px)]">
    <Breadcrumb className="shrink-0" items={[{ title: <Link to="/finances/imports">{t('finances.imports.title')}</Link> }, { title: t('finances.imports.detailTitle') }]} />
    <div className="flex shrink-0 flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-start sm:justify-between">
      <div className="min-w-0 flex-1">
        <PageHeading title={<span className="break-all">{file.fileName}</span>} description={t('finances.imports.parsedSummary', { parsed: file.parsedRows, total: file.totalRows })} />
        <Tag color={STATUS_COLORS[file.status]} className="mt-2">{t(`finances.imports.statuses.${file.status}`)}</Tag>
      </div>
      <Space wrap>
        {file.suggestionRows > 0 && <Button type="primary" onClick={viewInbox}>{t('finances.imports.viewInbox')}</Button>}
        {file.status === 'failed' && <Button icon={<ReloadOutlined aria-hidden />} onClick={handleRetry} loading={retryMutation.isPending}>{t('finances.imports.retry')}</Button>}
        {(processing || file.status === 'failed') && <Button danger icon={<StopOutlined aria-hidden />} onClick={confirmAbort} loading={abortMutation.isPending}>{t('finances.imports.abort')}</Button>}
      </Space>
    </div>
    <div className="grid shrink-0 grid-cols-2 gap-3 sm:grid-cols-3 [&>div]:min-w-0 [&>div]:px-3 [&>div:first-child]:col-span-2 sm:[&>div:first-child]:col-span-1">
      <StatTile label={t('finances.imports.suggestions')} value={<span className="tabular-nums">{file.suggestionRows}</span>} />
      <StatTile label={t('finances.imports.duplicates')} value={<span className="tabular-nums" style={file.duplicateRows ? { color: token.colorWarning } : undefined}>{file.duplicateRows}</span>} />
      <StatTile label={t('finances.imports.errors')} value={<span className="tabular-nums" style={file.errorRows ? { color: token.colorError } : undefined}>{file.errorRows}</span>} />
    </div>
    {file.errorMessage && <Alert type="error" showIcon title={<span className="break-all">{file.errorMessage}</span>} className="shrink-0" />}
    <details className="shrink-0">
      <summary className="w-fit cursor-pointer select-none text-sm">{t('finances.imports.fileDetails')}</summary>
      <dl className="mb-0 mt-3 grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
        {metadata.filter(([, value]) => value != null).map(([label, value]) => <div key={label}>
          <dt><Typography.Text type="secondary" className="text-xs">{t(label)}</Typography.Text></dt>
          <dd className="m-0 break-all tabular-nums">{value}</dd>
        </div>)}
      </dl>
    </details>
    {rowsError && <Alert type="error" title={rowsFailure.message} showIcon className="shrink-0" />}
    <ImportRowsTable rows={rows} loading={rowsLoading} processing={processing} onOpen={(row) => setSelectedRowId(row.id)} onLink={startLink} onViewTransaction={viewTransaction} onViewInbox={viewInbox} />
    <ImportRowDrawer row={rows.find((row) => row.id === selectedRowId) ?? null} onClose={() => setSelectedRowId(null)} onLink={startLink} onViewTransaction={viewTransaction} onViewInbox={viewInbox} />
    {viewTxId && <TransactionDetailDrawer transactionId={viewTxId} onClose={() => setViewTxId(null)} />}
    {linkingRow && <LinkTransactionModal open defaultSearch={importRowPreview(linkingRow).description} accountId={file.accountId} loading={linkMutation.isPending} onClose={() => setLinkingRow(null)} onPick={handleLink} />}
  </div>
}
