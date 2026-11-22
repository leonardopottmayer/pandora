import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Dropdown, Input, Select, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, SearchOutlined } from '@ant-design/icons'
import { DEDUP_STATUSES, IMPORT_ROW_STATUSES, type DedupStatus, type ImportRowDto, type ImportRowStatus } from '../../models'
import { formatDate } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { importRowPreview } from './importRowPreview'

interface ImportRowsTableProps {
  rows: ImportRowDto[]
  loading: boolean
  processing: boolean
  onOpen: (row: ImportRowDto) => void
  onLink: (row: ImportRowDto) => void
  onViewTransaction: (id: string) => void
  onViewInbox: () => void
}

export function ImportRowsTable({ rows, loading, processing, onOpen, onLink, onViewTransaction, onViewInbox }: ImportRowsTableProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [text, setText] = useState('')
  const [status, setStatus] = useState<ImportRowStatus>()
  const [dedup, setDedup] = useState<DedupStatus>()
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const filtered = useMemo(() => {
    const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase().trim()
    const search = normalize(text)
    return rows.filter((row) => (!status || row.status === status) && (!dedup || row.dedupStatus === dedup)
      && normalize(`${importRowPreview(row).description} ${row.rawData} ${row.externalId ?? ''}`).includes(search))
  }, [rows, text, status, dedup])
  const columns: ColumnsType<ImportRowDto> = [
    { title: '#', dataIndex: 'rowIndex', width: 56, responsive: ['lg'], render: (value: number) => <span className="tabular-nums">{value}</span> },
    {
      title: t('finances.transactions.description'), key: 'description',
      render: (_, row) => {
        const preview = importRowPreview(row)
        return <div className="flex min-w-0 flex-col gap-1">
          <Button type="link" className="!h-auto !max-w-full !justify-start !p-0" onClick={() => onOpen(row)}><span className="truncate" title={preview.description}>{preview.description}</span></Button>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
            <Typography.Text type="secondary" className="text-xs tabular-nums lg:hidden">{t('finances.imports.rowNumber', { index: row.rowIndex })}</Typography.Text>
            <Typography.Text type={row.status === 'error' ? 'danger' : 'secondary'} className="text-xs">{t(`finances.imports.rowStatuses.${row.status}`)}</Typography.Text>
            {row.dedupStatus !== 'new' && <Tag color={row.dedupStatus === 'matched' ? 'blue' : 'gold'} className="max-w-full !whitespace-normal">{t(`finances.imports.dedupStatuses.${row.dedupStatus}`)}</Tag>}
            {row.installmentNumber && row.installmentCount && <Typography.Text type="secondary" className="text-xs tabular-nums">{t('finances.imports.installment')} {row.installmentNumber}/{row.installmentCount}</Typography.Text>}
            {preview.occurredOn && <Typography.Text type="secondary" className="text-xs tabular-nums lg:hidden">{formatDate(preview.occurredOn)}</Typography.Text>}
          </div>
          {row.errorMessage && <Typography.Text type="danger" className="truncate text-xs" title={row.errorMessage}>{row.errorMessage}</Typography.Text>}
          {preview.amount != null && preview.currency && <span className="tabular-nums md:hidden"><CurrencyAmount amount={preview.amount} currency={preview.currency} direction={preview.direction} /></span>}
        </div>
      },
    },
    { title: t('finances.transactions.occurredOn'), key: 'date', width: 110, responsive: ['lg'], render: (_, row) => <span className="tabular-nums">{formatDate(importRowPreview(row).occurredOn) || '—'}</span> },
    {
      title: t('finances.transactions.amount'), key: 'amount', width: 124, align: 'right', responsive: ['md'],
      render: (_, row) => {
        const preview = importRowPreview(row)
        return preview.amount != null && preview.currency
          ? <span className="tabular-nums"><CurrencyAmount amount={preview.amount} currency={preview.currency} direction={preview.direction} /></span>
          : '—'
      },
    },
    {
      key: 'actions', width: 48, align: 'right',
      render: (_, row) => <span onClick={(event) => event.stopPropagation()}>
        <Dropdown trigger={['click']} placement="bottomRight" menu={{ items: [
          { key: 'details', label: t('finances.imports.rowDetails'), onClick: () => onOpen(row) },
          ...(row.pendingTransactionId || row.matchedPendingTransactionId ? [{ key: 'inbox', label: t('finances.imports.viewInbox'), onClick: onViewInbox }] : []),
          ...(row.matchedTransactionId ? [{ key: 'transaction', label: t('finances.imports.viewTransaction'), onClick: () => onViewTransaction(row.matchedTransactionId!) }] : []),
          ...(row.pendingTransactionId && row.dedupStatus !== 'matched' ? [{ key: 'link', label: t('finances.imports.link'), onClick: () => onLink(row) }] : []),
        ] }}><Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('finances.imports.rowActions', { index: row.rowIndex })} /></Dropdown>
      </span>,
    },
  ]
  return <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
    <div className="flex shrink-0 flex-wrap items-center gap-2">
      <div className="w-full sm:w-64"><Input allowClear prefix={<SearchOutlined />} placeholder={t('finances.imports.searchRows')} value={text} onChange={(event) => setText(event.target.value)} /></div>
      <Select<ImportRowStatus> className="w-full sm:w-48" allowClear value={status} onChange={setStatus} aria-label={t('finances.imports.rowStatusFilter')} placeholder={t('finances.imports.rowStatusFilter')}
        options={IMPORT_ROW_STATUSES.map((value) => ({ value, label: t(`finances.imports.rowStatuses.${value}`) }))} />
      <Select<DedupStatus> className="w-full sm:w-48" allowClear value={dedup} onChange={setDedup} aria-label={t('finances.imports.dedupStatus')} placeholder={t('finances.imports.dedupStatus')}
        options={DEDUP_STATUSES.map((value) => ({ value, label: t(`finances.imports.dedupStatuses.${value}`) }))} />
      {(text || status || dedup) && <Button type="link" onClick={() => { setText(''); setStatus(undefined); setDedup(undefined) }}>{t('finances.filters.clear')}</Button>}
    </div>
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
      <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
        <Table rowKey="id" loading={loading} dataSource={filtered} columns={columns} tableLayout="fixed" size="middle" pagination={false}
          sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
          onRow={(row) => ({ onClick: () => onOpen(row), className: 'cursor-pointer' })}
          locale={{ emptyText: t(processing && !rows.length ? 'finances.imports.waitingRows' : 'finances.imports.noRows') }} />
      </div>
      <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>{t('finances.imports.rowCount', { count: filtered.length })}</Typography.Text>
    </div>
  </div>
}
