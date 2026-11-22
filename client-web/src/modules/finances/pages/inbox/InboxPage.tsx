import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Checkbox, Dropdown, Empty, Input, Modal, Popconfirm, Skeleton, Space, Tag, Typography, theme } from 'antd'
import { CheckOutlined, EllipsisOutlined, PaperClipOutlined, SwapOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { PendingSource, PendingTransactionDto } from '../../models'
import { kindDirection } from '../../lib/enums'
import { formatDate, formatMoney } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { StatTile } from '../../components/StatTile'
import { AttachmentsModal } from '../../components/AttachmentsModal'
import { AttachmentsPanel } from '../../components/AttachmentsPanel'
import { TransactionDetailDrawer } from '../../components/TransactionDetailDrawer'
import { LinkTransactionModal } from '../../components/LinkTransactionModal'
import { useAccountNames } from '../../hooks/useAccounts'
import { useAttachments } from '../../hooks/useAttachments'
import { useCardNames } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import {
  useApprovePendingTransaction,
  useApprovePendingTransactionBatch,
  useCreateTransferFromPending,
  useLinkPendingTransaction,
  usePendingTransactions,
  useRejectPendingTransaction,
} from '../../hooks/usePendingTransactions'
import { PendingTransactionEditModal } from './PendingTransactionEditModal'
import { TransferFromPendingModal } from './TransferFromPendingModal'

// A whole import is usually handled at once, so imports come first.
const SOURCE_ORDER: PendingSource[] = ['import', 'recurrence', 'manual']

export function InboxPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [editing, setEditing] = useState<PendingTransactionDto | null>(null)
  const [editOpen, setEditOpen] = useState(false)
  const [rejecting, setRejecting] = useState<PendingTransactionDto | null>(null)
  const [rejectReason, setRejectReason] = useState('')
  const [viewTxId, setViewTxId] = useState<string | null>(null)
  const [linking, setLinking] = useState<PendingTransactionDto | null>(null)
  const [transferOpen, setTransferOpen] = useState(false)
  const [attachmentsFor, setAttachmentsFor] = useState<string | null>(null)

  const { data, isLoading } = usePendingTransactions({ take: 1000 })
  const { data: queuedFiles } = useAttachments({ queued: true })
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const categoryNames = useCategoryNames()
  const approveMutation = useApprovePendingTransaction()
  const rejectMutation = useRejectPendingTransaction()
  const batchMutation = useApprovePendingTransactionBatch()
  const linkMutation = useLinkPendingTransaction()
  const transferMutation = useCreateTransferFromPending()

  const all = data ?? []
  const groups = SOURCE_ORDER.map((source) => ({
    source,
    rows: all.filter((p) => p.source === source).sort((a, b) => a.occurredOn.localeCompare(b.occurredOn)),
  })).filter((g) => g.rows.length > 0)

  // What approving everything would move, per currency.
  const comesIn = new Map<string, number>()
  const goesOut = new Map<string, number>()
  for (const p of all) {
    const direction = kindDirection(p.kind)
    if (p.amount == null || direction === 'neutral') continue
    const sums = direction === 'in' ? comesIn : goesOut
    sums.set(p.currency, (sums.get(p.currency) ?? 0) + p.amount)
  }
  const sumsText = (sums: Map<string, number>) =>
    sums.size === 0 ? '—' : [...sums].map(([currency, amount]) => formatMoney(amount, currency)).join(' · ')

  // Resolve the two selected suggestions into a transfer's legs (one outflow + one inflow on accounts).
  const selectedPendings = all.filter((p) => selectedIds.includes(p.id))
  const outflowLeg =
    selectedPendings.length === 2
      ? selectedPendings.find((p) => p.accountId && !p.cardId && kindDirection(p.kind) === 'out') ?? null
      : null
  const inflowLeg =
    selectedPendings.length === 2
      ? selectedPendings.find((p) => p.accountId && !p.cardId && kindDirection(p.kind) === 'in') ?? null
      : null
  const canTransfer =
    !!outflowLeg && !!inflowLeg && outflowLeg.accountId !== inflowLeg.accountId

  function toggle(ids: string[], on: boolean) {
    setSelectedIds((current) => (on ? [...new Set([...current, ...ids])] : current.filter((id) => !ids.includes(id))))
  }

  async function handleLink(transactionId: string) {
    if (!linking) return
    try {
      await linkMutation.mutateAsync({ id: linking.id, transactionId })
      message.success(t('finances.imports.linked'))
      setLinking(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  async function handleTransfer(description: string, occurredOn: string) {
    if (!outflowLeg || !inflowLeg) return
    try {
      await transferMutation.mutateAsync({
        outflowPendingId: outflowLeg.id,
        inflowPendingId: inflowLeg.id,
        description,
        occurredOn,
      })
      message.success(t('finances.inbox.transferDone'))
      setTransferOpen(false)
      setSelectedIds([])
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  function openEdit(pending: PendingTransactionDto) {
    setEditing(pending)
    setEditOpen(true)
  }

  async function handleApprove(p: PendingTransactionDto) {
    try {
      await approveMutation.mutateAsync(p.id)
      message.success(t('finances.inbox.approved'))
      toggle([p.id], false)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  async function confirmReject() {
    if (!rejecting) return
    try {
      await rejectMutation.mutateAsync({ id: rejecting.id, reason: rejectReason || null })
      message.success(t('finances.inbox.rejected'))
      toggle([rejecting.id], false)
      setRejecting(null)
      setRejectReason('')
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  async function handleApproveSelected() {
    try {
      const count = await batchMutation.mutateAsync(selectedIds)
      message.success(t('finances.inbox.batchApproved', { count }))
      setSelectedIds([])
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  const divider = `1px solid ${token.colorBorderSecondary}`

  function renderRow(p: PendingTransactionDto, first: boolean) {
    const categoryId = p.systemCategoryId ?? p.userCategoryId
    const place = p.cardId ? cardNames.get(p.cardId) : p.accountId ? accountNames.get(p.accountId) : null
    const details = [categoryId && categoryNames.get(categoryId), place].filter(Boolean)
    const stop = (e: React.SyntheticEvent) => e.stopPropagation()

    return (
      <div
        key={p.id}
        role="button"
        tabIndex={0}
        onClick={() => openEdit(p)}
        onKeyDown={(e) => e.key === 'Enter' && openEdit(p)}
        className="flex cursor-pointer items-center gap-3 px-4 py-2.5"
        style={{ borderTop: first ? undefined : divider }}
      >
        <span onClick={stop} onKeyDown={stop}>
          <Checkbox
            checked={selectedIds.includes(p.id)}
            onChange={(e) => toggle([p.id], e.target.checked)}
            aria-label={t('finances.inbox.select', { name: p.description })}
          />
        </span>
        <span className="hidden w-20 shrink-0 text-sm tabular-nums sm:inline">{formatDate(p.occurredOn)}</span>
        <div className="flex min-w-0 flex-1 flex-col">
          <div className="flex min-w-0 items-center gap-2">
            <Typography.Text strong className="truncate">
              {p.description}
            </Typography.Text>
            {p.duplicateOfTransactionId && (
              <Tag
                color="gold"
                className="m-0 shrink-0 cursor-pointer"
                onClick={(e) => {
                  e.stopPropagation()
                  setViewTxId(p.duplicateOfTransactionId)
                }}
              >
                {t('finances.inbox.possibleDuplicate')}
              </Tag>
            )}
          </div>
          <Typography.Text type="secondary" className="truncate text-xs">
            <span className="sm:hidden">{formatDate(p.occurredOn)} · </span>
            {details.join(' · ')}
            {p.attachmentCount > 0 && (
              <>
                {details.length > 0 && ' · '}
                <PaperClipOutlined /> {p.attachmentCount}
              </>
            )}
          </Typography.Text>
        </div>
        <div className="w-28 shrink-0 text-right tabular-nums">
          {p.amount == null ? (
            <Typography.Text type="secondary">—</Typography.Text>
          ) : (
            <CurrencyAmount amount={p.amount} currency={p.currency} direction={kindDirection(p.kind)} />
          )}
        </div>
        <span className="flex shrink-0 items-center gap-1" onClick={stop} onKeyDown={stop}>
          <Button
            size="small"
            icon={<CheckOutlined />}
            loading={approveMutation.isPending && approveMutation.variables === p.id}
            onClick={() => void handleApprove(p)}
            aria-label={t('finances.inbox.approveItem', { name: p.description })}
          >
            <span className="hidden sm:inline">{t('finances.inbox.approve')}</span>
          </Button>
          <Dropdown
            trigger={['click']}
            placement="bottomRight"
            menu={{
              items: [
                { key: 'edit', label: t('common.edit'), onClick: () => openEdit(p) },
                { key: 'attachments', label: t('finances.attachments.button'), onClick: () => setAttachmentsFor(p.id) },
                ...(p.source === 'import'
                  ? [{ key: 'link', label: t('finances.imports.link'), onClick: () => setLinking(p) }]
                  : []),
                { type: 'divider' as const },
                { key: 'reject', danger: true, label: t('finances.inbox.reject'), onClick: () => setRejecting(p) },
              ],
            }}
          >
            <Button
              type="text"
              size="small"
              icon={<EllipsisOutlined />}
              aria-label={t('finances.inbox.moreActions', { name: p.description })}
            />
          </Dropdown>
        </span>
      </div>
    )
  }

  const groupHeading = (source: PendingSource, rows: PendingTransactionDto[]): ReactNode => {
    const ids = rows.map((p) => p.id)
    const picked = ids.filter((id) => selectedIds.includes(id)).length
    return (
      <div
        className="sticky top-0 z-10 flex items-center gap-3 border-x-0 border-b border-t border-solid px-4 py-1.5 text-xs font-semibold uppercase tracking-wider first:border-t-0"
        style={{
          color: token.colorTextSecondary,
          borderColor: token.colorBorderSecondary,
          // Opaque, so the rows going under it do not show through; tinted like a table header.
          background: token.colorBgContainer,
          boxShadow: `inset 0 0 0 9999px ${token.colorFillAlter}`,
        }}
      >
        <Checkbox
          checked={picked === ids.length}
          indeterminate={picked > 0 && picked < ids.length}
          onChange={(e) => toggle(ids, e.target.checked)}
          aria-label={t('finances.inbox.selectGroup', { name: t(`finances.inbox.sources.${source}`) })}
        />
        {t(`finances.inbox.sources.${source}`)} · {rows.length}
      </div>
    )
  }

  return (
    // From lg up the page holds the screen's height and the list scrolls inside, as in Transactions.
    <div className="flex flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.inbox')} description={t('finances.inbox.subtitle')} />
        {selectedIds.length > 0 && (
          <Space wrap>
            {canTransfer && (
              <Button icon={<SwapOutlined />} onClick={() => setTransferOpen(true)}>
                {t('finances.inbox.makeTransfer')}
              </Button>
            )}
            <Popconfirm
              title={t('finances.inbox.batchApproveConfirm', { count: selectedIds.length })}
              okText={t('finances.inbox.approve')}
              cancelText={t('common.cancel')}
              onConfirm={handleApproveSelected}
            >
              <Button type="primary" icon={<CheckOutlined />} loading={batchMutation.isPending}>
                {t('finances.inbox.approveSelected', { count: selectedIds.length })}
              </Button>
            </Popconfirm>
          </Space>
        )}
      </div>

      <div className="flex shrink-0 flex-wrap gap-3">
        <StatTile label={t('finances.inbox.comesIn')} value={sumsText(comesIn)} />
        <StatTile label={t('finances.inbox.goesOut')} value={sumsText(goesOut)} />
      </div>

      {!!queuedFiles?.length && (
        <div className="max-h-48 shrink-0 overflow-auto rounded-xl p-4" style={{ border: divider, background: token.colorBgContainer }}>
          <AttachmentsPanel owner={{ queued: true }} />
        </div>
      )}

      {/* One panel: only its rows scroll, and each source's heading holds at the top. */}
      <div
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border: divider, background: token.colorBgContainer }}
      >
        <div className="min-h-0 flex-1 overflow-auto">
          {isLoading ? (
            <Skeleton active className="p-4" />
          ) : groups.length === 0 ? (
            <Empty className="my-8" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.inbox.empty')} />
          ) : (
            groups.map((g) => (
              <div key={g.source}>
                {groupHeading(g.source, g.rows)}
                {g.rows.map((p, index) => renderRow(p, index === 0))}
              </div>
            ))
          )}
        </div>
        <div className="shrink-0 px-4 py-2 text-xs" style={{ borderTop: divider, color: token.colorTextSecondary }}>
          {t('finances.inbox.count', { count: all.length })}
        </div>
      </div>

      <PendingTransactionEditModal open={editOpen} pending={editing} onClose={() => setEditOpen(false)} />

      <AttachmentsModal
        owner={attachmentsFor ? { pendingTransactionId: attachmentsFor } : null}
        onClose={() => setAttachmentsFor(null)}
      />
      <TransactionDetailDrawer transactionId={viewTxId} onClose={() => setViewTxId(null)} />

      <LinkTransactionModal
        open={!!linking}
        defaultSearch={linking?.description}
        accountId={linking?.accountId}
        loading={linkMutation.isPending}
        onClose={() => setLinking(null)}
        onPick={handleLink}
      />

      <TransferFromPendingModal
        open={transferOpen}
        outflow={outflowLeg}
        inflow={inflowLeg}
        accountNames={accountNames}
        loading={transferMutation.isPending}
        onClose={() => setTransferOpen(false)}
        onConfirm={handleTransfer}
      />

      <Modal
        open={!!rejecting}
        title={t('finances.inbox.rejectTitle')}
        onCancel={() => {
          setRejecting(null)
          setRejectReason('')
        }}
        onOk={confirmReject}
        okButtonProps={{ danger: true, loading: rejectMutation.isPending }}
        okText={t('finances.inbox.reject')}
        cancelText={t('common.cancel')}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">{rejecting?.description}</Typography.Paragraph>
        <Input.TextArea
          rows={3}
          maxLength={500}
          value={rejectReason}
          onChange={(e) => setRejectReason(e.target.value)}
          placeholder={t('finances.inbox.rejectReasonPlaceholder')}
        />
      </Modal>
    </div>
  )
}
