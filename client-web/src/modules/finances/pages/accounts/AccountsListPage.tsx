import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Checkbox, Flex, Popconfirm, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AccountDto } from '../../models'
import { ACCOUNT_TYPE_META } from '../../lib/enums'
import { EnumTag } from '../../components/EnumTag'
import { useAccounts, useDeleteAccount, useSetAccountArchived } from '../../hooks/useAccounts'
import { AccountFormModal } from './AccountFormModal'

export function AccountsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [includeArchived, setIncludeArchived] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<AccountDto | null>(null)

  const { data, isLoading } = useAccounts({ includeArchived })
  const deleteMutation = useDeleteAccount()
  const archiveMutation = useSetAccountArchived()

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

  async function handleDelete(account: AccountDto) {
    try {
      await deleteMutation.mutateAsync(account.id)
      message.success(t('finances.accounts.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.accounts.deleteError')))
    }
  }

  const columns: ColumnsType<AccountDto> = [
    {
      title: t('finances.accounts.name'),
      dataIndex: 'name',
      render: (name: string, account) => (
        <Link to={`/finances/accounts/${account.id}`}>{name}</Link>
      ),
    },
    {
      title: t('finances.accounts.type'),
      dataIndex: 'type',
      render: (_, account) => <EnumTag meta={ACCOUNT_TYPE_META[account.type]} />,
    },
    { title: t('finances.accounts.currency'), dataIndex: 'currency' },
    {
      title: t('finances.accounts.institution'),
      dataIndex: 'institution',
      render: (value: string | null) => value ?? '—',
    },
    {
      title: t('finances.accounts.status'),
      dataIndex: 'archivedAt',
      render: (archivedAt: string | null) =>
        archivedAt ? <Tag>{t('finances.accounts.archivedStatus')}</Tag> : <Tag color="green">{t('finances.accounts.activeStatus')}</Tag>,
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, account) => (
        <Space>
          <Button size="small" onClick={() => openEdit(account)}>
            {t('common.edit')}
          </Button>
          <Button size="small" onClick={() => handleArchive(account)}>
            {account.archivedAt ? t('finances.accounts.unarchive') : t('finances.accounts.archive')}
          </Button>
          <Popconfirm
            title={t('finances.accounts.deleteConfirm')}
            okText={t('common.delete')}
            cancelText={t('common.cancel')}
            onConfirm={() => handleDelete(account)}
          >
            <Button size="small" danger>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Card>
      <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('nav.accounts')}
        </Typography.Title>
        <Space>
          <Checkbox checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)}>
            {t('finances.accounts.showArchived')}
          </Checkbox>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('finances.accounts.new')}
          </Button>
        </Space>
      </Flex>

      <Table
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        pagination={false}
        size="middle"
      />

      <AccountFormModal open={modalOpen} account={editing} onClose={() => setModalOpen(false)} />
    </Card>
  )
}
