import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Switch, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AccountBalanceDto, AccountDto } from '../../models'
import { ACCOUNT_TYPE_META } from '../../lib/enums'
import { formatMoney } from '../../lib/format'
import { EnumTag } from '../../components/EnumTag'
import { useAccountBalances, useAccounts, useDeleteAccount, useSetAccountArchived } from '../../hooks/useAccounts'
import { AccountFormModal } from './AccountFormModal'

export function AccountsListPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [includeArchived, setIncludeArchived] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<AccountDto | null>(null)

  const { data, isLoading } = useAccounts({ includeArchived })
  const accounts = data ?? []
  const balanceQueries = useAccountBalances(accounts.map((a) => a.id))
  const balanceOf = new Map<string, AccountBalanceDto>()
  balanceQueries.forEach((q) => q.data && balanceOf.set(q.data.accountId, q.data))

  const deleteMutation = useDeleteAccount()
  const archiveMutation = useSetAccountArchived()

  // What the open accounts hold, per currency: amounts in different currencies are never added up.
  const totals = new Map<string, number>()
  for (const account of accounts) {
    const balance = balanceOf.get(account.id)
    if (!account.archivedAt && balance) totals.set(balance.currency, (totals.get(balance.currency) ?? 0) + balance.posted)
  }

  function openCreate() {
    setEditing(null)
    setModalOpen(true)
  }

  function openEdit(account: AccountDto) {
    setEditing(account)
    setModalOpen(true)
  }

  async function handleArchive(account: AccountDto) {
    const archived = !account.archivedAt
    try {
      await archiveMutation.mutateAsync({ id: account.id, archived })
      message.success(archived ? t('finances.accounts.archived') : t('finances.accounts.unarchived'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.accounts.saveError')))
    }
  }

  function confirmDelete(account: AccountDto) {
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
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.accounts.deleteError')))
        }
      },
    })
  }

  const money = (amount: number, currency: string) => (
    <span className="tabular-nums" style={amount < 0 ? { color: token.colorError } : undefined}>
      {formatMoney(amount, currency)}
    </span>
  )

  const columns: ColumnsType<AccountDto> = [
    {
      title: t('finances.accounts.account'),
      dataIndex: 'name',
      render: (name: string, account) => (
        <div className="flex min-w-0 flex-col">
          <span className="flex flex-wrap items-center gap-2">
            <Link to={`/finances/accounts/${account.id}`} style={account.archivedAt ? { color: token.colorTextTertiary } : undefined}>
              {name}
            </Link>
            {account.archivedAt && <Tag>{t('finances.accounts.archivedStatus')}</Tag>}
          </span>
          {account.institution && (
            <Typography.Text type="secondary" className="text-xs">
              {account.institution}
            </Typography.Text>
          )}
        </div>
      ),
    },
    {
      title: t('finances.accounts.type'),
      dataIndex: 'type',
      responsive: ['md'],
      render: (_, account) => <EnumTag meta={ACCOUNT_TYPE_META[account.type]} />,
    },
    {
      title: t('finances.accounts.balance'),
      key: 'balance',
      align: 'right',
      sorter: (a, b) => (balanceOf.get(a.id)?.posted ?? 0) - (balanceOf.get(b.id)?.posted ?? 0),
      render: (_, account) => {
        const balance = balanceOf.get(account.id)
        if (!balance) return <Typography.Text type="secondary">—</Typography.Text>
        return (
          <div className="flex flex-col items-end">
            {money(balance.posted, balance.currency)}
            {balance.projected !== balance.posted && (
              <Typography.Text type="secondary" className="text-xs tabular-nums">
                {t('finances.accounts.projected', { amount: formatMoney(balance.projected, balance.currency) })}
              </Typography.Text>
            )}
          </div>
        )
      },
    },
    {
      key: 'actions',
      width: 48,
      align: 'right',
      render: (_, account) => (
        <Dropdown
          trigger={['click']}
          placement="bottomRight"
          menu={{
            items: [
              { key: 'edit', label: t('common.edit'), onClick: () => openEdit(account) },
              {
                key: 'archive',
                label: account.archivedAt ? t('finances.accounts.unarchive') : t('finances.accounts.archive'),
                onClick: () => void handleArchive(account),
              },
              { type: 'divider' as const },
              { key: 'delete', danger: true, label: t('common.delete'), onClick: () => confirmDelete(account) },
            ],
          }}
        >
          <Button
            type="text"
            size="small"
            icon={<EllipsisOutlined />}
            aria-label={t('finances.accounts.moreActions', { name: account.name })}
          />
        </Dropdown>
      ),
    },
  ]

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.accounts')} description={t('finances.accounts.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('finances.accounts.new')}
        </Button>
      </div>

      {totals.size > 0 && (
        <div className="flex flex-wrap gap-3">
          {[...totals].map(([currency, total]) => (
            <div
              key={currency}
              className="flex min-w-48 flex-col gap-0.5 rounded-xl px-4 py-3"
              style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
            >
              <Typography.Text type="secondary" className="text-xs uppercase tracking-wider">
                {t('finances.accounts.total', { currency })}
              </Typography.Text>
              <span className="text-2xl font-semibold">{money(total, currency)}</span>
            </div>
          ))}
        </div>
      )}

      <SettingsSection
        extra={
          <label className="flex cursor-pointer items-center gap-2 text-sm" style={{ color: token.colorTextSecondary }}>
            <Switch size="small" checked={includeArchived} onChange={setIncludeArchived} />
            {t('finances.accounts.showArchived')}
          </label>
        }
      >
        <Table
          rowKey="id"
          loading={isLoading}
          dataSource={accounts}
          columns={columns}
          pagination={false}
          size="middle"
          locale={{ emptyText: t('finances.accounts.empty') }}
        />
      </SettingsSection>

      <AccountFormModal open={modalOpen} account={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
