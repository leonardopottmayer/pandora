import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePermission } from '@/modules/identity/hooks/useAccess'
import type { RoleDto } from '../../models'
import { useDeleteRole, useRoles } from '../../hooks/useAdmin'
import { AdminError } from '../../components/AdminError'
import { RoleFormModal } from './RoleFormModal'

export function RolesPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const canManage = usePermission('admin.roles.manage')
  const { data, isLoading, error } = useRoles()
  const deleteRole = useDeleteRole()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<RoleDto | null>(null)

  if (error) return <AdminError error={error} />

  function open(role: RoleDto | null) {
    setEditing(role)
    setModalOpen(true)
  }

  function confirmDelete(role: RoleDto) {
    modal.confirm({
      title: t('admin.roles.deleteConfirm'),
      content: t('admin.roles.deleteContent', { name: role.name, count: role.holders }),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await deleteRole.mutateAsync(role.id)
          message.success(t('admin.roles.deleted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('admin.saveError')))
        }
      },
    })
  }

  const columns: ColumnsType<RoleDto> = [
    {
      title: t('admin.roles.role'),
      dataIndex: 'name',
      render: (name: string, role) => (
        <div className="flex min-w-0 flex-col">
          <span className="flex flex-wrap items-center gap-2">
            <span className="truncate">{name}</span>
            {role.isSystem && <Tag>{t('admin.roles.system')}</Tag>}
            {role.isDefault && <Tag color="blue">{t('admin.roles.default')}</Tag>}
          </span>
          {role.description && (
            <Typography.Text type="secondary" className="truncate text-xs">
              {role.description}
            </Typography.Text>
          )}
        </div>
      ),
    },
    {
      title: t('admin.roles.holders'),
      dataIndex: 'holders',
      width: 110,
      align: 'right',
      render: (holders: number) => <span className="tabular-nums">{holders}</span>,
    },
    {
      key: 'actions',
      width: 48,
      align: 'right',
      render: (_, role) =>
        canManage && !role.isSystem ? (
          <span onClick={(e) => e.stopPropagation()}>
            <Dropdown
              trigger={['click']}
              placement="bottomRight"
              menu={{
                items: [
                  { key: 'edit', label: t('common.edit'), onClick: () => open(role) },
                  ...(role.isDefault
                    ? []
                    : [
                        { type: 'divider' as const },
                        { key: 'delete', danger: true, label: t('common.delete'), onClick: () => confirmDelete(role) },
                      ]),
                ],
              }}
            >
              <Button
                type="text"
                size="small"
                icon={<EllipsisOutlined />}
                aria-label={t('admin.roles.moreActions', { name: role.name })}
              />
            </Dropdown>
          </span>
        ) : null,
    },
  ]

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.adminRoles')} description={t('admin.roles.subtitle')} />
        {canManage && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => open(null)}>
            {t('admin.roles.new')}
          </Button>
        )}
      </div>

      <SettingsSection>
        <Table
          rowKey="id"
          tableLayout="fixed"
          loading={isLoading}
          dataSource={data ?? []}
          columns={columns}
          pagination={false}
          size="middle"
          onRow={(role) => ({ onClick: () => open(role), className: 'cursor-pointer' })}
          locale={{ emptyText: t('admin.roles.empty') }}
        />
      </SettingsSection>

      <RoleFormModal open={modalOpen} role={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
