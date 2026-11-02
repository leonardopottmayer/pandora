import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { App, Breadcrumb, Button, DatePicker, Dropdown, Empty, Skeleton, Spin, Tag, Typography, theme } from 'antd'
import { EllipsisOutlined, LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import { SUPPORTED_BANKS, type TransactionDto } from '../../models'
import { ACCOUNT_TYPE_META, kindDirection, transactionKindLabelKey } from '../../lib/enums'
import { formatDay, formatMoney } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { StatTile } from '../../components/StatTile'
import { TransactionDetailModal } from '../../components/TransactionDetailModal'
import {
  useAccount,
  useAccountBalance,
  useAccountTransactions,
  useDeleteAccount,
  useSetAccountArchived,
} from '../../hooks/useAccounts'
import { useCategoryNames } from '../../hooks/useCategories'
import { TransactionFormModal } from '../transactions/TransactionFormModal'
import { AccountFormModal } from './AccountFormModal'

/** The most a month of the statement asks for, which is the endpoint's own cap. */
const MONTH_TAKE = 200

export function AccountDetailPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const { id = '' } = useParams<{ id: string }>()

  const [month, setMonth] = useState<Dayjs>(() => dayjs().startOf('month'))
  const [editing, setEditing] = useState(false)
  const [adding, setAdding] = useState(false)
  const [detailId, setDetailId] = useState<string | null>(null)

  const { data: account, isLoading: loadingAccount } = useAccount(id)
  const { data: balance } = useAccountBalance(id)
  const { data: transactions, isLoading: loadingTx } = useAccountTransactions(id, {
    from: month.format('YYYY-MM-DD'),
    to: month.endOf('month').format('YYYY-MM-DD'),
    take: MONTH_TAKE,
  })
  const categoryNames = useCategoryNames()
  const deleteMutation = useDeleteAccount()
  const archiveMutation = useSetAccountArchived()

  const currency = account?.currency ?? balance?.currency ?? 'BRL'

  async function handleArchive() {
    if (!account) return
    const archived = !account.archivedAt
    try {
      await archiveMutation.mutateAsync({ id: account.id, archived })
      message.success(archived ? t('finances.accounts.archived') : t('finances.accounts.unarchived'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.accounts.saveError')))
    }
  }

  function confirmDelete() {
    if (!account) return
    modal.confirm({
      title: t('finances.accounts.deleteConfirm'),
      content: account.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync(account.id)
          message.success(t('finances.accounts.deleted'))
          navigate('/finances/accounts')
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.accounts.deleteError')))
        }
      },
    })
  }

  // Newest day first; within a day, as the server sent them.
  const days = new Map<string, TransactionDto[]>()
  for (const tx of [...(transactions ?? [])].sort((a, b) => b.occurredOn.localeCompare(a.occurredOn)))
    days.set(tx.occurredOn, [...(days.get(tx.occurredOn) ?? []), tx])

  function renderTransaction(tx: TransactionDto, first: boolean) {
    const categoryId = tx.systemCategoryId ?? tx.userCategoryId
    const voided = tx.status === 'void'
    return (
      <button
        key={tx.id}
        type="button"
        onClick={() => setDetailId(tx.id)}
        className="flex w-full cursor-pointer items-center gap-3 border-0 bg-transparent px-4 py-2.5 text-left"
        style={{ borderTop: first ? undefined : `1px solid ${token.colorBorderSecondary}`, color: token.colorText }}
      >
        <span className="flex min-w-0 flex-1 flex-col">
          <span className="flex min-w-0 items-center gap-2">
            <Typography.Text delete={voided} type={voided ? 'secondary' : undefined} className="truncate">
              {tx.description}
            </Typography.Text>
            {tx.status === 'pending' && <Tag color="gold">{t('finances.enums.transactionStatus.pending')}</Tag>}
          </span>
          <Typography.Text type="secondary" className="truncate text-xs">
            {(categoryId && categoryNames.get(categoryId)) || t(transactionKindLabelKey(tx.kind))}
          </Typography.Text>
        </span>
        <span className="shrink-0 tabular-nums">
          <CurrencyAmount
            amount={tx.amount}
            currency={tx.currency}
            direction={voided ? undefined : kindDirection(tx.kind)}
          />
        </span>
      </button>
    )
  }

  if (loadingAccount) return <Skeleton active />
  if (!account) return <Empty className="mt-16" image={Empty.PRESENTED_IMAGE_SIMPLE} />

  const bank = SUPPORTED_BANKS.find((b) => b.code === account.bankCode)?.name
  const facts = [account.institution, bank && bank !== account.institution ? bank : null, account.currency]
    .filter(Boolean)
    .join(' · ')
  const toPost = balance ? balance.projected - balance.posted : 0

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <Breadcrumb
        items={[{ title: <Link to="/finances/accounts">{t('nav.accounts')}</Link> }, { title: account.name }]}
      />

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <span className="flex flex-wrap items-center gap-2">
            <Typography.Title level={4} className="!mb-0">
              {account.name}
            </Typography.Title>
            <EnumTag meta={ACCOUNT_TYPE_META[account.type]} />
            {account.archivedAt && <Tag>{t('finances.accounts.archivedStatus')}</Tag>}
          </span>
          <Typography.Text type="secondary">{facts}</Typography.Text>
          {account.description && (
            <Typography.Text type="secondary" className="max-w-prose text-sm">
              {account.description}
            </Typography.Text>
          )}
        </div>
        <span className="flex items-center gap-1">
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setAdding(true)}>
            {t('finances.transactions.new')}
          </Button>
          <Dropdown
            trigger={['click']}
            placement="bottomRight"
            menu={{
              items: [
                { key: 'edit', label: t('common.edit'), onClick: () => setEditing(true) },
                {
                  key: 'archive',
                  label: account.archivedAt ? t('finances.accounts.unarchive') : t('finances.accounts.archive'),
                  onClick: () => void handleArchive(),
                },
                { type: 'divider' as const },
                { key: 'delete', danger: true, label: t('common.delete'), onClick: confirmDelete },
              ],
            }}
          >
            <Button icon={<EllipsisOutlined />} aria-label={t('finances.accounts.moreActions', { name: account.name })} />
          </Dropdown>
        </span>
      </div>

      <div className="flex flex-wrap gap-3">
        <StatTile
          label={t('finances.accounts.postedBalance')}
          value={balance ? <span className="tabular-nums">{formatMoney(balance.posted, currency)}</span> : '—'}
        />
        <StatTile
          label={t('finances.accounts.projectedBalance')}
          value={balance ? <span className="tabular-nums">{formatMoney(balance.projected, currency)}</span> : '—'}
          note={
            toPost !== 0 &&
            t(toPost > 0 ? 'finances.accounts.toComeIn' : 'finances.accounts.toGoOut', {
              amount: formatMoney(Math.abs(toPost), currency),
            })
          }
        />
      </div>

      <section className="flex flex-col gap-2.5">
        <div className="flex flex-wrap items-center justify-between gap-2 px-0.5">
          <Typography.Text strong type="secondary" className="text-xs uppercase tracking-wider">
            {t('finances.accounts.statement')}
          </Typography.Text>
          <span className="flex items-center gap-1">
            <Button
              type="text"
              size="small"
              icon={<LeftOutlined />}
              aria-label={t('finances.accounts.previousMonth')}
              onClick={() => setMonth(month.subtract(1, 'month'))}
            />
            <DatePicker
              picker="month"
              size="small"
              variant="borderless"
              allowClear={false}
              format="MMMM YYYY"
              value={month}
              onChange={(value) => value && setMonth(value.startOf('month'))}
              aria-label={t('finances.accounts.month')}
            />
            <Button
              type="text"
              size="small"
              icon={<RightOutlined />}
              aria-label={t('finances.accounts.nextMonth')}
              onClick={() => setMonth(month.add(1, 'month'))}
            />
          </span>
        </div>

        {loadingTx ? (
          <Spin className="m-6" />
        ) : days.size === 0 ? (
          <SettingsSection>
            <Empty className="my-6" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.accounts.noTransactions')} />
          </SettingsSection>
        ) : (
          [...days].map(([day, items]) => (
            <SettingsSection key={day} title={formatDay(day)}>
              {items.map((tx, index) => renderTransaction(tx, index === 0))}
            </SettingsSection>
          ))
        )}
        {(transactions?.length ?? 0) >= MONTH_TAKE && (
          <Typography.Text type="secondary" className="px-0.5 text-xs">
            {t('finances.accounts.monthCapped', { count: MONTH_TAKE })}
          </Typography.Text>
        )}
      </section>

      <AccountFormModal open={editing} account={account} onClose={() => setEditing(false)} />
      <TransactionFormModal open={adding} defaultAccountId={account.id} onClose={() => setAdding(false)} />
      <TransactionDetailModal transactionId={detailId} open={!!detailId} onClose={() => setDetailId(null)} />
    </div>
  )
}
