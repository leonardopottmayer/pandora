import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input, Select, Space, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { SearchOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { formatDateTime } from '@/modules/finances/lib/format'
import { usePermission } from '@/modules/identity/hooks/useAccess'
import type { AdminUserDto } from '../../models'
import { useAdminUsers, useApproveUsers, useChangeUserRoles, useRoles } from '../../hooks/useAdmin'
import { AdminError } from '../../components/AdminError'
import { UserDetailDrawer } from './UserDetailDrawer'

export function UsersPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const canManage = usePermission('admin.users.manage')
  const canReadRoles = usePermission('admin.roles.read')
  const { data, isLoading, error } = useAdminUsers()
  const { data: roles } = useRoles(canReadRoles)
  const changeRoles = useChangeUserRoles()
  const approve = useApproveUsers()

  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<string | undefined>()
  const [onlyWaiting, setOnlyWaiting] = useState(false)
  const [selected, setSelected] = useState<string[]>([])
  const [bulkRole, setBulkRole] = useState<string | undefined>()
  const [openId, setOpenId] = useState<string | null>(null)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)

  const roleName = useMemo(() => new Map((roles ?? []).map((r) => [r.id, r.name])), [roles])
  const users = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (data ?? [])
      .filter(
        (u) =>
          (!onlyWaiting || u.awaitingApproval) &&
          (!roleFilter || u.roleIds.includes(roleFilter)) &&
          (!term || [u.name, u.email, u.username].some((v) => v.toLowerCase().includes(term))),
      )
      // Who is waiting comes first: it is what needs doing.
      .sort((a, b) => Number(b.awaitingApproval) - Number(a.awaitingApproval))
  }, [data, search, roleFilter, onlyWaiting])
  const waitingCount = (data ?? []).filter((u) => u.awaitingApproval).length
  const selectedWaiting = selected.filter((id) => data?.find((u) => u.id === id)?.awaitingApproval)

  if (error) return <AdminError error={error} />

  async function bulk(assign: boolean) {
    if (!bulkRole) return
    try {
      const changed = await changeRoles.mutateAsync({ userIds: selected, roleId: bulkRole, assign })
      message.success(t(assign ? 'admin.users.rolesAdded' : 'admin.users.rolesRemoved', { count: changed }))
      setSelected([])
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  async function approveSelected() {
    try {
      const count = await approve.mutateAsync(selectedWaiting)
      message.success(t('admin.users.approvedDone', { count }))
      setSelected([])
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  const columns: ColumnsType<AdminUserDto> = [
    {
      title: t('admin.users.user'),
      key: 'user',
      render: (_, user) => (
        <div className="flex min-w-0 flex-col">
          <span className="flex flex-wrap items-center gap-2">
            <span className="truncate" style={user.disabled ? { color: token.colorTextTertiary } : undefined}>
              {user.name}
            </span>
            {user.disabled && <Tag color="red">{t('admin.users.disabled')}</Tag>}
            {user.awaitingApproval && !user.disabled && <Tag color="gold">{t('admin.users.awaitingApproval')}</Tag>}
            {!user.activated && <Tag color="gold">{t('admin.users.notActivated')}</Tag>}
          </span>
          <Typography.Text type="secondary" className="truncate text-xs">
            {user.email} · @{user.username}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: t('admin.users.roles'),
      key: 'roles',
      responsive: ['md'],
      render: (_, user) => (
        <span className="flex flex-wrap gap-1">
          {user.roleIds.map((id) => (
            <Tag key={id}>{roleName.get(id) ?? '…'}</Tag>
          ))}
        </span>
      ),
    },
    {
      title: t('admin.users.lastSignIn'),
      dataIndex: 'lastSignInAt',
      width: 160,
      responsive: ['lg'],
      render: (value: string | null) => (
        <span className="tabular-nums">{value ? formatDateTime(value) : '—'}</span>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.adminUsers')} description={t('admin.users.subtitle')} />
      </div>

      {waitingCount > 0 && (
        <Alert
          className="shrink-0"
          type="warning"
          showIcon
          title={t('admin.users.waitingNotice', { count: waitingCount })}
          action={
            <Button size="small" onClick={() => setOnlyWaiting(!onlyWaiting)}>
              {onlyWaiting ? t('admin.users.showEveryone') : t('admin.users.showWaiting')}
            </Button>
          }
        />
      )}

      <div className="flex shrink-0 flex-wrap items-center gap-3">
        <Input
          allowClear
          prefix={<SearchOutlined />}
          placeholder={t('admin.users.search')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full sm:w-64"
        />
        {canReadRoles && (
          <Select
            allowClear
            placeholder={t('admin.users.anyRole')}
            value={roleFilter}
            onChange={setRoleFilter}
            options={(roles ?? []).map((r) => ({ value: r.id, label: r.name }))}
            className="w-full sm:w-48"
            aria-label={t('admin.users.filterByRole')}
          />
        )}
        {canManage && selectedWaiting.length > 0 && (
          <Button type="primary" loading={approve.isPending} onClick={() => void approveSelected()}>
            {t('admin.users.approveSelected', { count: selectedWaiting.length })}
          </Button>
        )}
        {canManage && canReadRoles && selected.length > 0 && (
          <Space.Compact className="w-full sm:w-auto">
            <Select
              placeholder={t('admin.users.pickRole', { count: selected.length })}
              value={bulkRole}
              onChange={setBulkRole}
              options={(roles ?? []).map((r) => ({ value: r.id, label: r.name }))}
              className="w-full sm:w-56"
              aria-label={t('admin.users.bulkRole')}
            />
            <Button disabled={!bulkRole} loading={changeRoles.isPending} onClick={() => void bulk(true)}>
              {t('admin.users.addRole')}
            </Button>
            <Button disabled={!bulkRole} loading={changeRoles.isPending} onClick={() => void bulk(false)}>
              {t('admin.users.removeRole')}
            </Button>
          </Space.Compact>
        )}
      </div>

      <div
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
      >
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          <Table
            rowKey="id"
            tableLayout="fixed"
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            loading={isLoading}
            dataSource={users}
            columns={columns}
            pagination={false}
            rowSelection={
              canManage ? { selectedRowKeys: selected, onChange: (keys) => setSelected(keys as string[]) } : undefined
            }
            onRow={(user) => ({ onClick: () => setOpenId(user.id), className: 'cursor-pointer' })}
            locale={{ emptyText: t('admin.users.empty') }}
          />
        </div>
        <div
          className="shrink-0 border-x-0 border-b-0 border-t border-solid px-4 py-2 text-xs"
          style={{ borderColor: token.colorBorderSecondary, color: token.colorTextSecondary }}
        >
          {t('admin.users.count', { count: users.length })}
        </div>
      </div>

      <UserDetailDrawer userId={openId} roles={roles ?? []} onClose={() => setOpenId(null)} />
    </div>
  )
}
