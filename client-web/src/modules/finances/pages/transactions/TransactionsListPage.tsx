import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Badge, Button, DatePicker, Dropdown, Input, InputNumber, Select, Space, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, FilterOutlined, PaperClipOutlined, PlusOutlined, SwapOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  TRANSACTION_KINDS,
  TRANSACTION_STATUSES,
  type SystemCategoryDto,
  type TransactionDto,
  type TransactionKind,
  type TransactionStatus,
  type UserCategoryDto,
} from '../../models'
import { TRANSACTION_STATUS_META, kindDirection, transactionKindLabelKey } from '../../lib/enums'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { MonthNavigator } from '../../components/MonthNavigator'
import { StatTile } from '../../components/StatTile'
import { TransactionDetailDrawer } from '../../components/TransactionDetailDrawer'
import { useAccounts, useAccountNames } from '../../hooks/useAccounts'
import { useCards, useCardNames } from '../../hooks/useCards'
import { useCategoryNames, useSystemCategories, useUserCategories } from '../../hooks/useCategories'
import { useAllTransactions, usePostTransaction, useUnvoidTransaction, useVoidTransaction } from '../../hooks/useTransactions'
import { TransactionFormModal } from './TransactionFormModal'
import { TransferFormModal } from './TransferFormModal'

/** The filters under "More filters"; the bar's own are separate state. */
interface MoreFilters {
  statuses?: TransactionStatus[]
  kinds?: TransactionKind[]
  referenceMonths?: string[]
  dueDateFrom?: string
  dueDateTo?: string
  amountMin?: number
  amountMax?: number
  /** A range of its own replaces the month. */
  from?: string
  to?: string
}

/**
 * Moving money between the user's own places is not income or spending: transfers, and paying a card
 * statement, whose purchases already count as they happen.
 */
const NOT_IN_TOTALS: ReadonlySet<TransactionKind> = new Set(['transfer-in', 'transfer-out', 'card-statement-payment'])

function flattenCats(cats: Array<SystemCategoryDto | UserCategoryDto>): Array<{ id: string; name: string }> {
  const result: Array<{ id: string; name: string }> = []
  function visit(list: typeof cats) {
    for (const c of list) {
      result.push({ id: c.id, name: c.name })
      if (c.children.length) visit(c.children as typeof cats)
    }
  }
  visit(cats)
  return result
}

export function TransactionsListPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()

  const [month, setMonth] = useState<Dayjs>(() => dayjs().startOf('month'))
  const [text, setText] = useState('')
  const [places, setPlaces] = useState<string[]>([])
  const [categoryIds, setCategoryIds] = useState<string[]>([])
  const [more, setMore] = useState<MoreFilters>({})
  const [moreOpen, setMoreOpen] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [transferOpen, setTransferOpen] = useState(false)
  const [detailId, setDetailId] = useState<string | null>(null)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)

  const customRange = !!(more.from || more.to)
  const { data, isLoading } = useAllTransactions(
    customRange
      ? { from: more.from, to: more.to }
      : { from: month.format('YYYY-MM-DD'), to: month.endOf('month').format('YYYY-MM-DD') },
  )
  const { data: accounts } = useAccounts()
  const { data: cards } = useCards()
  const { data: systemCategories } = useSystemCategories()
  const { data: userCategories } = useUserCategories()
  const categoryNames = useCategoryNames()
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const postMutation = usePostTransaction()
  const voidMutation = useVoidTransaction()
  const unvoidMutation = useUnvoidTransaction()

  function setMoreFilter<K extends keyof MoreFilters>(key: K, value: MoreFilters[K]) {
    setMore((f) => {
      const next = { ...f, [key]: value }
      if (value === undefined || value === null || (Array.isArray(value) && value.length === 0)) delete next[key]
      return next
    })
  }

  function clearFilters() {
    setText('')
    setPlaces([])
    setCategoryIds([])
    setMore({})
  }

  const moreCount = [
    more.statuses,
    more.kinds,
    more.referenceMonths,
    more.dueDateFrom || more.dueDateTo,
    more.amountMin != null || more.amountMax != null,
    customRange,
  ].filter(Boolean).length
  const anyFilter = moreCount > 0 || !!text || places.length > 0 || categoryIds.length > 0

  const referenceMonthOptions = useMemo(() => {
    const months = new Set((data ?? []).map((tx) => tx.statementReferenceMonth).filter((m): m is string => !!m))
    return [...months].sort().map((value) => ({ value, label: formatReferenceMonth(value) }))
  }, [data])

  const categoryOptions = useMemo(
    () =>
      [
        { label: t('finances.categories.systemGroup'), options: flattenCats(systemCategories ?? []) },
        { label: t('finances.categories.userGroup'), options: flattenCats(userCategories ?? []) },
      ]
        .filter((g) => g.options.length > 0)
        .map((g) => ({ label: g.label, options: g.options.map((c) => ({ value: c.id, label: c.name })) })),
    [systemCategories, userCategories, t],
  )

  const placeOptions = [
    {
      label: t('finances.filters.accounts'),
      options: (accounts ?? []).map((a) => ({ value: `account:${a.id}`, label: a.name })),
    },
    {
      label: t('finances.filters.cards'),
      options: (cards ?? []).map((c) => ({
        value: `card:${c.id}`,
        label: c.lastFour ? `${c.name} ····${c.lastFour}` : c.name,
      })),
    },
  ].filter((g) => g.options.length > 0)

  const filtered = useMemo(() => {
    const q = text.trim().toLowerCase()
    return (data ?? []).filter((tx) => {
      const categoryId = tx.systemCategoryId ?? tx.userCategoryId
      if (q) {
        const category = categoryId ? (categoryNames.get(categoryId) ?? '') : ''
        if (![tx.description, tx.payee ?? '', category].some((s) => s.toLowerCase().includes(q))) return false
      }
      if (places.length > 0) {
        const place = tx.cardId ? `card:${tx.cardId}` : tx.accountId ? `account:${tx.accountId}` : ''
        if (!places.includes(place)) return false
      }
      if (categoryIds.length > 0 && !(categoryId && categoryIds.includes(categoryId))) return false
      if (more.statuses && !more.statuses.includes(tx.status)) return false
      if (more.kinds && !more.kinds.includes(tx.kind)) return false
      if (more.referenceMonths && !(tx.statementReferenceMonth && more.referenceMonths.includes(tx.statementReferenceMonth)))
        return false
      if (more.dueDateFrom && (!tx.statementDueDate || tx.statementDueDate < more.dueDateFrom)) return false
      if (more.dueDateTo && (!tx.statementDueDate || tx.statementDueDate > more.dueDateTo)) return false
      if (more.amountMin != null && tx.amount < more.amountMin) return false
      if (more.amountMax != null && tx.amount > more.amountMax) return false
      return true
    })
  }, [data, text, places, categoryIds, more, categoryNames])

  // In, out and the difference of what is on screen, per currency.
  const totals = useMemo(() => {
    const byCurrency = new Map<string, { in: number; out: number }>()
    for (const tx of filtered) {
      const direction = kindDirection(tx.kind)
      if (tx.status === 'void' || direction === 'neutral' || NOT_IN_TOTALS.has(tx.kind)) continue
      const sums = byCurrency.get(tx.currency) ?? { in: 0, out: 0 }
      sums[direction] += tx.amount
      byCurrency.set(tx.currency, sums)
    }
    return byCurrency
  }, [filtered])

  async function handlePost(tx: TransactionDto) {
    try {
      await postMutation.mutateAsync(tx.id)
      message.success(t('finances.transactions.posted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  function confirmVoid(tx: TransactionDto) {
    modal.confirm({
      title: t('finances.transactions.voidConfirm'),
      content: tx.description,
      okText: t('finances.transactions.void'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await voidMutation.mutateAsync({ id: tx.id })
          message.success(t('finances.transactions.voided'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.transactions.saveError')))
        }
      },
    })
  }

  async function handleUnvoid(tx: TransactionDto) {
    try {
      await unvoidMutation.mutateAsync(tx.id)
      message.success(t('finances.transactions.unvoided'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  const money = (amount: number, currency: string) => <span className="tabular-nums">{formatMoney(amount, currency)}</span>

  const columns: ColumnsType<TransactionDto> = [
    {
      title: t('finances.transactions.occurredOn'),
      dataIndex: 'occurredOn',
      width: 110,
      responsive: ['md'],
      render: (value: string) => <span className="tabular-nums">{formatDate(value)}</span>,
      sorter: (a, b) => a.occurredOn.localeCompare(b.occurredOn),
      defaultSortOrder: 'descend',
    },
    {
      title: t('finances.transactions.description'),
      dataIndex: 'description',
      render: (_, tx) => {
        const categoryId = tx.systemCategoryId ?? tx.userCategoryId
        const voided = tx.status === 'void'
        const place = tx.cardId ? cardNames.get(tx.cardId) : tx.accountId ? accountNames.get(tx.accountId) : null
        const details = [
          categoryId ? categoryNames.get(categoryId) : t(transactionKindLabelKey(tx.kind)),
          place,
          tx.installmentPlanId && tx.installmentNumber != null && t('finances.transactions.installmentN', { n: tx.installmentNumber }),
          tx.statementReferenceMonth &&
            (tx.statementDueDate
              ? t('finances.transactions.statementDue', {
                  month: formatReferenceMonth(tx.statementReferenceMonth),
                  due: formatDate(tx.statementDueDate),
                })
              : t('finances.transactions.statementOf', { month: formatReferenceMonth(tx.statementReferenceMonth) })),
        ].filter(Boolean)
        return (
          <div className="flex min-w-0 flex-col">
            <span className="flex min-w-0 items-center gap-2">
              <Typography.Text delete={voided} type={voided ? 'secondary' : undefined} className="truncate">
                {tx.description}
              </Typography.Text>
              {tx.attachmentCount > 0 && (
                <PaperClipOutlined
                  aria-label={t('finances.transactions.withAttachments', { count: tx.attachmentCount })}
                  style={{ color: token.colorTextTertiary }}
                />
              )}
              {tx.status === 'pending' && <Tag color="gold">{t(TRANSACTION_STATUS_META.pending.labelKey)}</Tag>}
            </span>
            <Typography.Text type="secondary" className="block truncate text-xs">
              {/* The date column hides on a narrow screen; the date moves here. */}
              <span className="md:hidden">{formatDate(tx.occurredOn)} · </span>
              {details.join(' · ')}
            </Typography.Text>
          </div>
        )
      },
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      width: 124,
      align: 'right',
      render: (_, tx) => (
        <span className="tabular-nums">
          <CurrencyAmount
            amount={tx.amount}
            currency={tx.currency}
            direction={tx.status === 'void' ? undefined : kindDirection(tx.kind)}
          />
        </span>
      ),
      sorter: (a, b) => a.amount - b.amount,
    },
    {
      key: 'actions',
      width: 48,
      align: 'right',
      render: (_, tx) => (
        // The row opens the transaction; its menu must not.
        <span onClick={(e) => e.stopPropagation()}>
          <Dropdown
            trigger={['click']}
            placement="bottomRight"
            menu={{
              items: [
                { key: 'open', label: t('finances.transactions.open'), onClick: () => setDetailId(tx.id) },
                ...(tx.status === 'pending'
                  ? [{ key: 'post', label: t('finances.transactions.post'), onClick: () => void handlePost(tx) }]
                  : []),
                { type: 'divider' as const },
                tx.status === 'void'
                  ? { key: 'unvoid', label: t('finances.transactions.unvoid'), onClick: () => void handleUnvoid(tx) }
                  : { key: 'void', danger: true, label: t('finances.transactions.void'), onClick: () => confirmVoid(tx) },
              ],
            }}
          >
            <Button
              type="text"
              size="small"
              icon={<EllipsisOutlined />}
              aria-label={t('finances.transactions.moreActions', { description: tx.description })}
            />
          </Dropdown>
        </span>
      ),
    },
  ]

  // From lg up the page holds the screen's height (the layout's header and padding are 112px): what is
  // above the table stays put. Below it, the filters wrap too much to leave the table room, and the
  // page scrolls as usual.
  return (
    <div className="flex flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.transactions')} description={t('finances.transactions.subtitle')} />
        <Space>
          <Button icon={<SwapOutlined />} onClick={() => setTransferOpen(true)}>
            {t('finances.transactions.transfer')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setFormOpen(true)}>
            {t('finances.transactions.new')}
          </Button>
        </Space>
      </div>

      <div className="flex shrink-0 flex-wrap gap-3">
        {/* With nothing on screen the tiles stay, empty, so the table does not jump up. */}
        {totals.size === 0 &&
          (['totalIn', 'totalOut', 'totalNet'] as const).map((key) => (
            <StatTile key={key} label={t(`finances.transactions.${key}`)} value="—" />
          ))}
        {[...totals].flatMap(([currency, sums]) => [
          <StatTile
            key={`${currency}-in`}
            label={`${t('finances.transactions.totalIn')} · ${currency}`}
            value={money(sums.in, currency)}
          />,
          <StatTile
            key={`${currency}-out`}
            label={`${t('finances.transactions.totalOut')} · ${currency}`}
            value={money(sums.out, currency)}
          />,
          <StatTile
            key={`${currency}-net`}
            label={`${t('finances.transactions.totalNet')} · ${currency}`}
            value={
              <span style={{ color: sums.in - sums.out < 0 ? token.colorError : undefined }}>
                {money(sums.in - sums.out, currency)}
              </span>
            }
          />,
        ])}
      </div>

      {/* "More filters" opens over the table, so nothing above it changes size. */}
      <div className="relative z-20 flex shrink-0 flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {customRange ? (
            <Tag closable onClose={() => setMore((f) => ({ ...f, from: undefined, to: undefined }))}>
              {formatDate(more.from) || '…'} – {formatDate(more.to) || '…'}
            </Tag>
          ) : (
            <MonthNavigator value={month} onChange={setMonth} />
          )}
          <Input.Search
            allowClear
            className="w-full sm:w-64"
            placeholder={t('common.search')}
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
          <Select
            mode="multiple"
            allowClear
            maxTagCount="responsive"
            className="w-full sm:w-56"
            placeholder={t('finances.filters.place')}
            optionFilterProp="label"
            value={places}
            onChange={setPlaces}
            options={placeOptions}
          />
          <Select
            mode="multiple"
            allowClear
            maxTagCount="responsive"
            className="w-full sm:w-56"
            placeholder={t('finances.transactions.category')}
            optionFilterProp="label"
            value={categoryIds}
            onChange={setCategoryIds}
            options={categoryOptions}
          />
          <Badge count={moreCount} size="small">
            <Button icon={<FilterOutlined />} aria-expanded={moreOpen} onClick={() => setMoreOpen((o) => !o)}>
              {t('finances.filters.more')}
            </Button>
          </Badge>
          {anyFilter && (
            <Button type="link" onClick={clearFilters}>
              {t('finances.filters.clear')}
            </Button>
          )}
        </div>

        {moreOpen && (
          <div
            className="absolute inset-x-0 top-full mt-2 grid grid-cols-1 gap-2 rounded-xl p-3 sm:grid-cols-2 lg:grid-cols-3"
            style={{
              border: `1px solid ${token.colorBorderSecondary}`,
              background: token.colorBgElevated,
              boxShadow: token.boxShadowSecondary,
            }}
          >
            <DatePicker.RangePicker
              placeholder={[t('finances.filters.periodFrom'), t('finances.filters.periodTo')]}
              value={[more.from ? dayjs(more.from) : null, more.to ? dayjs(more.to) : null]}
              allowEmpty={[true, true]}
              onChange={(range) => {
                setMoreFilter('from', range?.[0]?.format('YYYY-MM-DD'))
                setMoreFilter('to', range?.[1]?.format('YYYY-MM-DD'))
              }}
            />
            <Select
              mode="multiple"
              allowClear
              placeholder={t('finances.transactions.status')}
              value={more.statuses ?? []}
              onChange={(v: TransactionStatus[]) => setMoreFilter('statuses', v)}
              options={TRANSACTION_STATUSES.map((s) => ({ value: s, label: t(TRANSACTION_STATUS_META[s].labelKey) }))}
            />
            <Select
              mode="multiple"
              allowClear
              placeholder={t('finances.transactions.kind')}
              value={more.kinds ?? []}
              onChange={(v: TransactionKind[]) => setMoreFilter('kinds', v)}
              options={TRANSACTION_KINDS.map((k) => ({ value: k, label: t(transactionKindLabelKey(k)) }))}
            />
            <Select
              mode="multiple"
              allowClear
              placeholder={t('finances.filters.referenceMonth')}
              value={more.referenceMonths ?? []}
              onChange={(v: string[]) => setMoreFilter('referenceMonths', v)}
              options={referenceMonthOptions}
            />
            <DatePicker.RangePicker
              placeholder={[t('finances.filters.dueFrom'), t('finances.filters.dueTo')]}
              value={[more.dueDateFrom ? dayjs(more.dueDateFrom) : null, more.dueDateTo ? dayjs(more.dueDateTo) : null]}
              allowEmpty={[true, true]}
              onChange={(range) => {
                setMoreFilter('dueDateFrom', range?.[0]?.format('YYYY-MM-DD'))
                setMoreFilter('dueDateTo', range?.[1]?.format('YYYY-MM-DD'))
              }}
            />
            <Space.Compact>
              <InputNumber<number>
                className="w-1/2"
                placeholder={t('finances.filters.amountFrom')}
                value={more.amountMin}
                onChange={(v) => setMoreFilter('amountMin', v ?? undefined)}
              />
              <InputNumber<number>
                className="w-1/2"
                placeholder={t('finances.filters.amountTo')}
                value={more.amountMax}
                onChange={(v) => setMoreFilter('amountMax', v ?? undefined)}
              />
            </Space.Compact>
          </div>
        )}
      </div>

      {/* The rest of the height is the table's, and it scrolls inside with its header held. */}
      <div
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
      >
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          <Table
            rowKey="id"
            loading={isLoading}
            dataSource={filtered}
            columns={columns}
            size="middle"
            // Fixed: the description takes what the other columns leave and truncates, instead of widening
            // the table past a phone's width.
            tableLayout="fixed"
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            onRow={(tx) => ({ onClick: () => setDetailId(tx.id), className: 'cursor-pointer' })}
            locale={{ emptyText: t('finances.transactions.empty') }}
            pagination={false}
          />
        </div>
        {filtered.length > 0 && (
          <Typography.Text
            type="secondary"
            className="shrink-0 px-4 py-2 text-xs tabular-nums"
            style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}
          >
            {t('finances.transactions.count', { count: filtered.length })}
          </Typography.Text>
        )}
      </div>

      <TransactionFormModal open={formOpen} onClose={() => setFormOpen(false)} />
      <TransferFormModal open={transferOpen} onClose={() => setTransferOpen(false)} />
      <TransactionDetailDrawer transactionId={detailId} onClose={() => setDetailId(null)} />
    </div>
  )
}
