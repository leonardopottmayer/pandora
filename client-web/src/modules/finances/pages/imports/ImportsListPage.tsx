import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Dropdown, Input, Select, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { CloudUploadOutlined, EllipsisOutlined, SearchOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { IMPORT_STATUSES, type ImportFileDto, type ImportStatus } from '../../models'
import { formatDateTime } from '../../lib/format'
import { useAccounts } from '../../hooks/useAccounts'
import { useCards } from '../../hooks/useCards'
import { useAbortImportFile, useAllImportFiles, useRetryImportFile } from '../../hooks/useImports'
import { ImportUploadModal } from './ImportUploadModal'
import { usePreferences } from '@/modules/identity/context/preferences-context'

const STATUS_COLORS: Record<ImportStatus, string> = { received: 'blue', parsing: 'processing', completed: 'green', failed: 'red', aborted: 'default' }

export function ImportsListPage() {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const [uploadOpen, setUploadOpen] = useState(false)
  const [text, setText] = useState('')
  const [status, setStatus] = useState<ImportStatus>()
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const { data: files = [], isLoading, isError, error } = useAllImportFiles()
  const { data: accounts = [], isLoading: loadingAccounts } = useAccounts()
  const { data: cards = [], isLoading: loadingCards } = useCards()
  const abortMutation = useAbortImportFile()
  const retryMutation = useRetryImportFile()
  const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase().trim()
  const filtered = files.filter((file) => (!status || file.status === status) && normalize(file.fileName).includes(normalize(text)))
  const accountNames = new Map(accounts.map((account) => [account.id, account.name]))
  const cardNames = new Map(cards.map((card) => [card.id, card.name]))
  const destination = (file: ImportFileDto) => file.accountId
    ? accountNames.get(file.accountId) ?? t('finances.imports.account')
    : cardNames.get(file.cardId ?? '') ?? t('finances.imports.card')

  function confirmAbort(file: ImportFileDto) {
    modal.confirm({
      title: t('finances.imports.abortConfirm'), content: file.fileName,
      okText: t('finances.imports.abort'), okButtonProps: { danger: true }, cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await abortMutation.mutateAsync(file.id)
          message.success(t('finances.imports.aborted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.imports.actionError')))
          throw err
        }
      },
    })
  }
  async function retry(file: ImportFileDto) {
    try {
      await retryMutation.mutateAsync(file.id)
      message.success(t('finances.imports.retried'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.imports.actionError')))
    }
  }

  const columns: ColumnsType<ImportFileDto> = [
    {
      title: t('finances.imports.fileName'), dataIndex: 'fileName',
      render: (name: string, file) => <div className="flex min-w-0 flex-col gap-1">
        <Link to={`/finances/imports/${file.id}`} className="truncate" title={name}>{name}</Link>
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <Tag color={STATUS_COLORS[file.status]}>{t(`finances.imports.statuses.${file.status}`)}</Tag>
          <Typography.Text type="secondary" className="min-w-0 truncate text-xs" title={destination(file)}>{destination(file)}</Typography.Text>
        </div>
        {(file.status === 'completed' || file.totalRows > 0) && <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs tabular-nums">
          <Typography.Text type="secondary" className="text-xs">{t('finances.imports.parsedSummary', { parsed: file.parsedRows, total: file.totalRows })}</Typography.Text>
          {file.suggestionRows > 0 && <Typography.Text type="secondary" className="text-xs">{t('finances.imports.suggestionCount', { count: file.suggestionRows })}</Typography.Text>}
          {file.duplicateRows > 0 && <Tag color="gold">{t('finances.imports.duplicateCount', { count: file.duplicateRows })}</Tag>}
          {file.errorRows > 0 && <Tag color="red">{t('finances.imports.errorCount', { count: file.errorRows })}</Tag>}
        </div>}
        {file.status === 'failed' && file.errorMessage && <Typography.Text className="truncate text-xs" style={{ color: token.colorError }} title={file.errorMessage}>{file.errorMessage}</Typography.Text>}
        <Typography.Text type="secondary" className="text-xs tabular-nums md:hidden">{formatDateTime(file.createdAt, timeZone)}</Typography.Text>
      </div>,
    },
    { title: t('finances.imports.createdAt'), dataIndex: 'createdAt', width: 180, responsive: ['md'], render: (value: string) => <span className="tabular-nums">{formatDateTime(value, timeZone)}</span> },
    {
      key: 'actions', width: 48, align: 'right',
      render: (_, file) => <span onClick={(event) => event.stopPropagation()}>
        <Dropdown trigger={['click']} placement="bottomRight" menu={{ items: [
          { key: 'open', label: t('finances.imports.viewDetails'), onClick: () => navigate(`/finances/imports/${file.id}`) },
          ...(file.status === 'failed' ? [{ key: 'retry', label: t('finances.imports.retry'), disabled: retryMutation.isPending, onClick: () => retry(file) }] : []),
          ...(['received', 'parsing', 'failed'].includes(file.status) ? [
            { type: 'divider' as const }, { key: 'abort', danger: true, label: t('finances.imports.abort'), disabled: abortMutation.isPending, onClick: () => confirmAbort(file) },
          ] : []),
        ] }}>
          <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('finances.imports.moreActions', { name: file.fileName })} />
        </Dropdown>
      </span>,
    },
  ]

  return <div className="flex min-w-0 flex-col gap-6 lg:h-[calc(100dvh-112px)]">
    <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
      <PageHeading title={t('finances.imports.title')} description={t('finances.imports.subtitle')} />
      <Button type="primary" icon={<CloudUploadOutlined />} onClick={() => setUploadOpen(true)}>{t('finances.imports.upload')}</Button>
    </div>
    <div className="flex shrink-0 flex-wrap items-center gap-2">
      <div className="w-full sm:w-64"><Input allowClear prefix={<SearchOutlined />} placeholder={t('finances.imports.search')} value={text} onChange={(event) => setText(event.target.value)} /></div>
      <Select<ImportStatus> className="w-full sm:w-48" allowClear value={status} onChange={setStatus} aria-label={t('finances.imports.status')} placeholder={t('finances.imports.status')}
        options={IMPORT_STATUSES.map((value) => ({ value, label: t(`finances.imports.statuses.${value}`) }))} />
      {(text || status) && <Button type="link" onClick={() => { setText(''); setStatus(undefined) }}>{t('finances.filters.clear')}</Button>}
    </div>
    {isError && <Alert type="error" title={error.message} showIcon className="shrink-0" />}
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
      <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
        <Table rowKey="id" loading={isLoading} dataSource={filtered} columns={columns} size="middle" tableLayout="fixed" pagination={false}
          sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
          onRow={(file) => ({ onClick: () => navigate(`/finances/imports/${file.id}`), className: 'cursor-pointer' })}
          locale={{ emptyText: t(files.length ? 'finances.imports.noMatches' : 'finances.imports.empty') }} />
      </div>
      <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>{t('finances.imports.count', { count: filtered.length })}</Typography.Text>
    </div>
    {uploadOpen && <ImportUploadModal onClose={() => setUploadOpen(false)} loadingDestinations={loadingAccounts || loadingCards}
      accountOptions={accounts.map((account) => ({ value: account.id, label: `${account.name} (${account.currency})` }))}
      cardOptions={cards.map((card) => ({ value: card.id, label: `${card.name} (${card.currency})` }))} />}
  </div>
}
