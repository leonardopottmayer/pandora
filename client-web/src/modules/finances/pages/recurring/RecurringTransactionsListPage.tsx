import { useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Empty, Skeleton, Typography, theme } from 'antd'
import { DownOutlined, EllipsisOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { todayIn } from '@/lib/today'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { RecurringTransactionDto } from '../../models'
import { kindDirection, recurrenceFrequencyLabelKey } from '../../lib/enums'
import { formatDate, formatMoney } from '../../lib/format'
import { monthlyAmount, upcomingDates } from '../../lib/recurrence'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { StatTile } from '../../components/StatTile'
import { useAccountNames, useAccounts } from '../../hooks/useAccounts'
import { useCardNames, useCards } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import {
  useDeleteRecurringTransaction,
  useRecurringTransactions,
  useSetRecurringPaused,
} from '../../hooks/useRecurringTransactions'
import { RecurringTransactionFormModal } from './RecurringTransactionFormModal'
import { GenerateOccurrenceModal } from './GenerateOccurrenceModal'

const UPCOMING_DAYS = 30

type Sums = { in: number; out: number }

function add(map: Map<string, Sums>, currency: string, direction: 'in' | 'out', amount: number) {
  const sums = map.get(currency) ?? { in: 0, out: 0 }
  sums[direction] += amount
  map.set(currency, sums)
}

export function RecurringTransactionsListPage() {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<RecurringTransactionDto | null>(null)
  const [generating, setGenerating] = useState<RecurringTransactionDto | null>(null)
  const [showFinished, setShowFinished] = useState(false)

  const { data, isLoading } = useRecurringTransactions()
  const { data: accounts } = useAccounts()
  const { data: cards } = useCards()
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const categoryNames = useCategoryNames()
  const deleteMutation = useDeleteRecurringTransaction()
  const pauseMutation = useSetRecurringPaused()

  const currencyOf = useMemo(() => {
    const byAccount = new Map((accounts ?? []).map((a) => [a.id, a.currency]))
    const byCard = new Map((cards ?? []).map((c) => [c.id, c.currency]))
    return (r: RecurringTransactionDto) =>
      (r.cardId ? byCard.get(r.cardId) : r.accountId ? byAccount.get(r.accountId) : undefined) ?? 'BRL'
  }, [accounts, cards])

  const today = todayIn(timeZone)
  const all = data ?? []
  const byNext = (a: RecurringTransactionDto, b: RecurringTransactionDto) => a.nextOccurrenceOn.localeCompare(b.nextOccurrenceOn)
  const active = all.filter((r) => r.status === 'active').sort(byNext)
  const paused = all.filter((r) => r.status === 'paused').sort(byNext)
  const finished = all.filter((r) => r.status === 'finished').sort((a, b) => a.name.localeCompare(b.name))

  // What the active ones commit to in a month, and what comes in the next days; estimates count as estimated.
  const monthly = new Map<string, Sums>()
  const upcoming = new Map<string, Sums>()
  let upcomingCount = 0
  for (const r of active) {
    const direction = kindDirection(r.kind)
    const dates = upcomingDates(r, UPCOMING_DAYS, today)
    upcomingCount += dates.length
    if (r.amount == null || direction === 'neutral') continue
    add(monthly, currencyOf(r), direction, monthlyAmount(r))
    if (dates.length) add(upcoming, currencyOf(r), direction, r.amount * dates.length)
  }
  const anyEstimate = active.some((r) => r.amountIsEstimate)

  function openCreate() {
    setEditing(null)
    setModalOpen(true)
  }

  function openEdit(recurring: RecurringTransactionDto) {
    setEditing(recurring)
    setModalOpen(true)
  }

  function frequencyText(r: RecurringTransactionDto): string {
    const unit = t(recurrenceFrequencyLabelKey(r.frequency))
    return r.interval === 1 ? unit : t('finances.recurring.everyN', { n: r.interval, unit })
  }

  async function handlePauseToggle(r: RecurringTransactionDto) {
    const pausing = r.status === 'active'
    try {
      await pauseMutation.mutateAsync({ id: r.id, paused: pausing })
      message.success(pausing ? t('finances.recurring.paused') : t('finances.recurring.resumed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.recurring.saveError')))
    }
  }

  function confirmDelete(r: RecurringTransactionDto) {
    modal.confirm({
      title: t('finances.recurring.deleteConfirm'),
      content: r.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync(r.id)
          message.success(t('finances.recurring.deleted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.recurring.deleteError')))
        }
      },
    })
  }

  function whenText(r: RecurringTransactionDto): { text: string; late: boolean } {
    const days = dayjs(r.nextOccurrenceOn).diff(today, 'day')
    if (days < 0) return { text: t('finances.recurring.late'), late: r.status === 'active' }
    if (days === 0) return { text: t('finances.recurring.today'), late: false }
    if (days === 1) return { text: t('finances.recurring.tomorrow'), late: false }
    return { text: t('finances.recurring.inDays', { count: days }), late: false }
  }

  function renderRow(r: RecurringTransactionDto, first: boolean) {
    const categoryId = r.systemCategoryId ?? r.userCategoryId
    const place = r.cardId ? cardNames.get(r.cardId) : r.accountId ? accountNames.get(r.accountId) : null
    const details = [
      frequencyText(r),
      place,
      categoryId && categoryNames.get(categoryId),
      r.maxOccurrences != null && `${r.occurrencesCount}/${r.maxOccurrences}`,
    ].filter(Boolean)
    const ended = r.status === 'finished'
    const when = whenText(r)

    return (
      <div
        key={r.id}
        role="button"
        tabIndex={0}
        onClick={() => openEdit(r)}
        onKeyDown={(e) => e.key === 'Enter' && openEdit(r)}
        className="flex cursor-pointer items-center gap-3 px-4 py-2.5"
        style={{ borderTop: first ? undefined : `1px solid ${token.colorBorderSecondary}` }}
      >
        <div className="flex min-w-0 flex-1 flex-col">
          <Typography.Text strong={!ended} type={ended ? 'secondary' : undefined} className="truncate">
            {r.name}
          </Typography.Text>
          <Typography.Text type="secondary" className="truncate text-xs">
            {details.join(' · ')}
          </Typography.Text>
        </div>
        {!ended && (
          <div className="hidden w-28 shrink-0 flex-col items-end sm:flex">
            <span className="text-sm tabular-nums">{formatDate(r.nextOccurrenceOn)}</span>
            <span className="text-xs" style={{ color: when.late ? token.colorError : token.colorTextTertiary }}>
              {when.text}
            </span>
          </div>
        )}
        <div className="w-28 shrink-0 text-right tabular-nums">
          {r.amount == null ? (
            <Typography.Text type="secondary">—</Typography.Text>
          ) : (
            <span title={r.amountIsEstimate ? t('finances.recurring.estimate') : undefined}>
              {r.amountIsEstimate && <Typography.Text type="secondary">≈ </Typography.Text>}
              <CurrencyAmount
                amount={r.amount}
                currency={currencyOf(r)}
                direction={ended ? undefined : kindDirection(r.kind)}
              />
            </span>
          )}
        </div>
        <span onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
          <Dropdown
            trigger={['click']}
            placement="bottomRight"
            menu={{
              items: [
                ...(ended
                  ? []
                  : [{ key: 'generate', label: t('finances.recurring.generate'), onClick: () => setGenerating(r) }]),
                { key: 'edit', label: t('common.edit'), onClick: () => openEdit(r) },
                ...(ended
                  ? []
                  : [
                      {
                        key: 'pause',
                        label: r.status === 'active' ? t('finances.recurring.pause') : t('finances.recurring.resume'),
                        onClick: () => void handlePauseToggle(r),
                      },
                    ]),
                { type: 'divider' as const },
                { key: 'delete', danger: true, label: t('common.delete'), onClick: () => confirmDelete(r) },
              ],
            }}
          >
            <Button
              type="text"
              size="small"
              icon={<EllipsisOutlined />}
              aria-label={t('finances.recurring.moreActions', { name: r.name })}
            />
          </Dropdown>
        </span>
      </div>
    )
  }

  const divider = `1px solid ${token.colorBorderSecondary}`

  const groupHeading = (label: ReactNode) => (
    <div
      className="sticky top-0 z-10 border-x-0 border-b border-t border-solid px-4 py-1.5 text-xs font-semibold uppercase tracking-wider first:border-t-0"
      style={{
        color: token.colorTextSecondary,
        borderColor: token.colorBorderSecondary,
        // Opaque, so the rows going under it do not show through; tinted like a table header.
        background: token.colorBgContainer,
        boxShadow: `inset 0 0 0 9999px ${token.colorFillAlter}`,
      }}
    >
      {label}
    </div>
  )

  const money = (amount: number, currency: string) => <span className="tabular-nums">{formatMoney(amount, currency)}</span>
  const sumsNote = (sums: Sums, currency: string) =>
    [
      sums.in > 0 && t('finances.recurring.comesIn', { amount: formatMoney(sums.in, currency) }),
      sums.out > 0 && t('finances.recurring.goesOut', { amount: formatMoney(sums.out, currency) }),
    ]
      .filter(Boolean)
      .join(' · ')

  return (
    // From lg up the page holds the screen's height and the list scrolls inside, as in Transactions.
    <div className="flex flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.recurring')} description={t('finances.recurring.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('finances.recurring.new')}
        </Button>
      </div>

      <div className="flex shrink-0 flex-wrap gap-3">
        {monthly.size === 0 ? (
          <StatTile label={t('finances.recurring.perMonth')} value="—" />
        ) : (
          [...monthly].map(([currency, sums]) => (
            <StatTile
              key={currency}
              label={`${t('finances.recurring.perMonth')} · ${currency}`}
              value={
                <span style={{ color: sums.in - sums.out < 0 ? token.colorError : undefined }}>
                  {money(sums.in - sums.out, currency)}
                </span>
              }
              note={[sumsNote(sums, currency), anyEstimate && t('finances.recurring.withEstimates')].filter(Boolean).join(' · ')}
            />
          ))
        )}
        <StatTile
          label={t('finances.recurring.nextDays', { count: UPCOMING_DAYS })}
          value={t('finances.recurring.occurrenceCount', { count: upcomingCount })}
          note={[...upcoming].map(([currency, sums]) => sumsNote(sums, currency)).join(' · ') || undefined}
        />
      </div>

      {/* One panel, as in Transactions: only its rows scroll, and each group's heading holds at the top. */}
      <div
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border: divider, background: token.colorBgContainer }}
      >
        <div className="min-h-0 flex-1 overflow-auto">
          {isLoading ? (
            <Skeleton active className="p-4" />
          ) : all.length === 0 ? (
            <Empty className="my-8" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.recurring.empty')} />
          ) : (
            <>
              {active.length > 0 && (
                <>
                  {groupHeading(`${t('finances.recurring.activeGroup')} · ${active.length}`)}
                  {active.map((r, index) => renderRow(r, index === 0))}
                </>
              )}
              {paused.length > 0 && (
                <>
                  {groupHeading(`${t('finances.recurring.pausedGroup')} · ${paused.length}`)}
                  {paused.map((r, index) => renderRow(r, index === 0))}
                </>
              )}
              {finished.length > 0 && (
                <>
                  {groupHeading(
                    <button
                      type="button"
                      aria-expanded={showFinished}
                      onClick={() => setShowFinished((open) => !open)}
                      className="flex cursor-pointer items-center gap-1.5 border-0 bg-transparent p-0 font-semibold uppercase tracking-wider"
                      style={{ color: 'inherit' }}
                    >
                      {showFinished ? <DownOutlined /> : <RightOutlined />}
                      {t('finances.recurring.finishedGroup')} · {finished.length}
                    </button>,
                  )}
                  {showFinished && finished.map((r, index) => renderRow(r, index === 0))}
                </>
              )}
            </>
          )}
        </div>
      </div>

      <RecurringTransactionFormModal open={modalOpen} recurring={editing} onClose={() => setModalOpen(false)} />
      <GenerateOccurrenceModal open={!!generating} recurring={generating} onClose={() => setGenerating(null)} />
    </div>
  )
}
