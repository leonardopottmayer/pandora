import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams, useNavigate } from 'react-router-dom'
import {
  App,
  Button,
  Card,
  Descriptions,
  Flex,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { ArrowLeftOutlined, LinkOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { ImportRowDto } from '../../models'
import {
  useAbortImportFile,
  useImportFile,
  useImportRows,
  useRetryImportFile,
} from '../../hooks/useImports'
import { useLinkPendingTransaction } from '../../hooks/usePendingTransactions'
import { TransactionDetailModal } from '../../components/TransactionDetailModal'
import { LinkTransactionModal } from '../../components/LinkTransactionModal'

const { Text } = Typography

const STATUS_COLORS: Record<string, string> = {
  received: 'blue',
  parsing: 'processing',
  completed: 'green',
  failed: 'red',
  aborted: 'default',
}

const DEDUP_COLORS: Record<string, string> = {
  new: 'green',
  certain: 'red',
  suspected: 'orange',
  matched: 'blue',
}

const ROW_STATUS_COLORS: Record<string, string> = {
  pending: 'default',
  skipped: 'default',
  'suggestion-created': 'green',
  error: 'red',
}

/** Best-effort description from the row's parsed payload, used to pre-fill the link search box. */
function parsedDescription(row: ImportRowDto | null): string | undefined {
  if (!row?.parsedPayload) return undefined
  try {
    const payload = JSON.parse(row.parsedPayload) as { Description?: string }
    return payload.Description
  } catch {
    return undefined
  }
}

export function ImportDetailPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const { data: file, isLoading: fileLoading } = useImportFile(id!)
  const { data: rows, isLoading: rowsLoading } = useImportRows(id!)
  const abortMutation = useAbortImportFile()
  const retryMutation = useRetryImportFile()
  const linkMutation = useLinkPendingTransaction()

  const [viewTxId, setViewTxId] = useState<string | null>(null)
  const [linkingRow, setLinkingRow] = useState<ImportRowDto | null>(null)

  async function handleLink(transactionId: string) {
    if (!linkingRow?.pendingTransactionId) return
    try {
      await linkMutation.mutateAsync({ id: linkingRow.pendingTransactionId, transactionId })
      message.success(t('finances.imports.linked'))
      setLinkingRow(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.imports.actionError')))
    }
  }

  async function handleAbort() {
    try {
      await abortMutation.mutateAsync(id!)
      message.success(t('finances.imports.aborted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.imports.actionError')))
    }
  }

  async function handleRetry() {
    try {
      await retryMutation.mutateAsync(id!)
      message.success(t('finances.imports.retried'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.imports.actionError')))
    }
  }

  const columns: ColumnsType<ImportRowDto> = [
    {
      title: '#',
      dataIndex: 'rowIndex',
      width: 60,
    },
    {
      title: t('finances.imports.rowStatus'),
      dataIndex: 'status',
      width: 160,
      render: (s) => (
        <Tag color={ROW_STATUS_COLORS[s] ?? 'default'}>
          {t(`finances.imports.rowStatuses.${s}`)}
        </Tag>
      ),
    },
    {
      title: t('finances.imports.dedupStatus'),
      dataIndex: 'dedupStatus',
      width: 110,
      render: (s) => (
        <Tag color={DEDUP_COLORS[s] ?? 'default'}>
          {t(`finances.imports.dedupStatuses.${s}`)}
        </Tag>
      ),
    },
    {
      title: t('finances.imports.rawData'),
      dataIndex: 'rawData',
      ellipsis: true,
    },
    {
      title: t('finances.imports.externalId'),
      dataIndex: 'externalId',
      width: 160,
      render: (v) => v ?? '—',
    },
    {
      title: t('finances.imports.installment'),
      width: 100,
      render: (_, r) =>
        r.installmentNumber && r.installmentCount
          ? `${r.installmentNumber}/${r.installmentCount}`
          : '—',
    },
    {
      title: t('finances.imports.suggestion'),
      dataIndex: 'pendingTransactionId',
      width: 140,
      render: (v) =>
        v ? (
          <a onClick={() => navigate('/finances/inbox')}>
            {t('finances.imports.viewInbox')}
          </a>
        ) : (
          '—'
        ),
    },
    {
      title: t('finances.imports.duplicate'),
      width: 160,
      render: (_, r) =>
        r.matchedTransactionId ? (
          <a onClick={() => setViewTxId(r.matchedTransactionId)}>
            {t('finances.imports.viewTransaction')}
          </a>
        ) : r.matchedPendingTransactionId ? (
          <Text type="warning">{t('finances.imports.existingSuggestion')}</Text>
        ) : (
          '—'
        ),
    },
    {
      title: t('finances.imports.error'),
      dataIndex: 'errorMessage',
      ellipsis: true,
      render: (v) => v ? <Text type="danger">{v}</Text> : '—',
    },
    {
      title: t('common.actions'),
      width: 120,
      align: 'right',
      render: (_, r) =>
        r.pendingTransactionId && r.dedupStatus !== 'matched' ? (
          <Button size="small" icon={<LinkOutlined />} onClick={() => setLinkingRow(r)}>
            {t('finances.imports.link')}
          </Button>
        ) : (
          '—'
        ),
    },
  ]

  if (fileLoading) return <Spin style={{ display: 'block', margin: '64px auto' }} />

  if (!file) return null

  return (
    <Flex vertical gap={16}>
      <Card
        title={
          <Space>
            <Button icon={<ArrowLeftOutlined />} type="text" onClick={() => navigate('/finances/imports')} />
            {file.fileName}
          </Space>
        }
        extra={
          <Space>
            {file.status === 'failed' && (
              <Button
                icon={<ReloadOutlined />}
                onClick={handleRetry}
                loading={retryMutation.isPending}
              >
                {t('finances.imports.retry')}
              </Button>
            )}
            {(file.status === 'received' || file.status === 'parsing' || file.status === 'failed') && (
              <Button
                danger
                icon={<StopOutlined />}
                onClick={handleAbort}
                loading={abortMutation.isPending}
              >
                {t('finances.imports.abort')}
              </Button>
            )}
          </Space>
        }
      >
        <Descriptions column={3} size="small">
          <Descriptions.Item label={t('finances.imports.status')}>
            <Tag color={STATUS_COLORS[file.status] ?? 'default'}>
              {t(`finances.imports.statuses.${file.status}`)}
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.imports.totalRows')}>{file.totalRows}</Descriptions.Item>
          <Descriptions.Item label={t('finances.imports.parsedRows')}>{file.parsedRows}</Descriptions.Item>
          <Descriptions.Item label={t('finances.imports.suggestions')}>{file.suggestionRows}</Descriptions.Item>
          <Descriptions.Item label={t('finances.imports.duplicates')}>{file.duplicateRows}</Descriptions.Item>
          <Descriptions.Item label={t('finances.imports.errors')}>{file.errorRows}</Descriptions.Item>
          {file.errorMessage && (
            <Descriptions.Item label={t('finances.imports.errorMessage')} span={3}>
              <Text type="danger">{file.errorMessage}</Text>
            </Descriptions.Item>
          )}
          <Descriptions.Item label={t('finances.imports.createdAt')}>
            {new Date(file.createdAt).toLocaleString()}
          </Descriptions.Item>
          {file.completedAt && (
            <Descriptions.Item label={t('finances.imports.completedAt')}>
              {new Date(file.completedAt).toLocaleString()}
            </Descriptions.Item>
          )}
        </Descriptions>
      </Card>

      <Card title={t('finances.imports.rowsTitle')}>
        <Table<ImportRowDto>
          rowKey="id"
          columns={columns}
          dataSource={rows}
          loading={rowsLoading}
          pagination={{ pageSize: 50 }}
          size="small"
        />
      </Card>

      <TransactionDetailModal
        transactionId={viewTxId}
        open={!!viewTxId}
        onClose={() => setViewTxId(null)}
      />

      <LinkTransactionModal
        open={!!linkingRow}
        defaultSearch={parsedDescription(linkingRow)}
        accountId={file.accountId}
        loading={linkMutation.isPending}
        onClose={() => setLinkingRow(null)}
        onPick={handleLink}
      />
    </Flex>
  )
}
