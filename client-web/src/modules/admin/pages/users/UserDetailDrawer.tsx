import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Checkbox, Descriptions, Drawer, Empty, Input, Select, Skeleton, Tag, Typography } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { formatDateTime } from '@/modules/finances/lib/format'
import { usePermission } from '@/modules/identity/hooks/useAccess'
import { EXCEPTION_EFFECTS, type AdminUserDetailDto, type RoleDto, type UserException } from '../../models'
import {
  useAdminUser,
  useChangeUserRoles,
  useRevokeUserSessions,
  useSetUserDisabled,
  useSetUserExceptions,
} from '../../hooks/useAdmin'
import { moduleLabel, permissionLabel } from '../../lib/labels'

interface UserDetailDrawerProps {
  userId: string | null
  roles: RoleDto[]
  onClose: () => void
}

/** One account: its roles, its exceptions and every permission it ends up with, and where from. */
export function UserDetailDrawer({ userId, roles, onClose }: UserDetailDrawerProps) {
  const { t } = useTranslation()
  const { data, isLoading } = useAdminUser(userId)

  return (
    <Drawer
      open={!!userId}
      onClose={onClose}
      size={560}
      title={data?.user.name ?? t('admin.users.user')}
      destroyOnHidden
      footer={data && <DrawerFooter detail={data} />}
    >
      {isLoading || !data ? <Skeleton active /> : <DrawerBody key={`${data.user.id}:${JSON.stringify(data.exceptions)}`} detail={data} roles={roles} />}
    </Drawer>
  )
}

function DrawerBody({ detail, roles }: { detail: AdminUserDetailDto; roles: RoleDto[] }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const canManage = usePermission('admin.users.manage')
  const changeRoles = useChangeUserRoles()
  const setExceptions = useSetUserExceptions()
  const [exceptions, setDraft] = useState<UserException[]>(detail.exceptions)
  const { user } = detail
  const dirty = JSON.stringify(exceptions) !== JSON.stringify(detail.exceptions)

  async function toggleRole(roleId: string, assign: boolean) {
    try {
      await changeRoles.mutateAsync({ userIds: [user.id], roleId, assign })
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  async function saveExceptions() {
    try {
      await setExceptions.mutateAsync({ id: user.id, exceptions: exceptions.filter((e) => e.pattern.trim()) })
      message.success(t('admin.users.exceptionsSaved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  const byModule = new Map<string, typeof detail.permissions>()
  for (const permission of detail.permissions) {
    const module = permission.key.split('.')[0]
    byModule.set(module, [...(byModule.get(module) ?? []), permission])
  }

  return (
    <div className="flex flex-col gap-6">
      <Descriptions
        size="small"
        column={1}
        items={[
          { key: 'email', label: t('admin.users.email'), children: user.email },
          { key: 'username', label: t('admin.users.username'), children: `@${user.username}` },
          {
            key: 'mfa',
            label: t('admin.users.mfa'),
            children: user.mfaEnabled ? t('admin.users.mfaOn') : <Tag color="gold">{t('admin.users.mfaOff')}</Tag>,
          },
          { key: 'created', label: t('admin.users.createdAt'), children: formatDateTime(user.createdAt) },
          {
            key: 'last',
            label: t('admin.users.lastSignIn'),
            children: user.lastSignInAt ? formatDateTime(user.lastSignInAt) : '—',
          },
        ]}
      />

      <section className="flex flex-col gap-2">
        <Typography.Text strong>{t('admin.users.roles')}</Typography.Text>
        {roles.length === 0 ? (
          <Typography.Text type="secondary">—</Typography.Text>
        ) : (
          roles.map((role) => (
            <Checkbox
              key={role.id}
              checked={user.roleIds.includes(role.id)}
              disabled={!canManage || changeRoles.isPending}
              onChange={(e) => void toggleRole(role.id, e.target.checked)}
            >
              {role.name}
            </Checkbox>
          ))
        )}
      </section>

      <section className="flex flex-col gap-2">
        <Typography.Text strong>{t('admin.users.exceptions')}</Typography.Text>
        <Typography.Text type="secondary" className="text-xs">
          {t('admin.users.exceptionsHelp')}
        </Typography.Text>
        {exceptions.map((exception, index) => (
          <div key={index} className="flex gap-2">
            <Input
              value={exception.pattern}
              disabled={!canManage}
              placeholder="finances.cards.delete"
              className="font-mono"
              aria-label={t('admin.users.pattern')}
              onChange={(e) => setDraft(exceptions.map((x, i) => (i === index ? { ...x, pattern: e.target.value } : x)))}
            />
            <Select
              value={exception.effect}
              disabled={!canManage}
              className="w-28 shrink-0"
              aria-label={t('admin.users.effect')}
              options={EXCEPTION_EFFECTS.map((effect) => ({ value: effect, label: t(`admin.users.effects.${effect}`) }))}
              onChange={(effect) => setDraft(exceptions.map((x, i) => (i === index ? { ...x, effect } : x)))}
            />
            {canManage && (
              <Button
                type="text"
                icon={<DeleteOutlined />}
                aria-label={t('admin.users.removeException')}
                onClick={() => setDraft(exceptions.filter((_, i) => i !== index))}
              />
            )}
          </div>
        ))}
        {canManage && (
          <div className="flex gap-2">
            <Button icon={<PlusOutlined />} onClick={() => setDraft([...exceptions, { pattern: '', effect: 'deny' }])}>
              {t('admin.users.addException')}
            </Button>
            {dirty && (
              <Button type="primary" loading={setExceptions.isPending} onClick={() => void saveExceptions()}>
                {t('common.save')}
              </Button>
            )}
          </div>
        )}
      </section>

      <section className="flex flex-col gap-2">
        <Typography.Text strong>{t('admin.users.effective')}</Typography.Text>
        {detail.permissions.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('admin.users.noPermissions')} />
        ) : (
          [...byModule].map(([module, permissions]) => (
            <div key={module} className="flex flex-col gap-1">
              <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">
                {moduleLabel(t, module)}
              </Typography.Text>
              {permissions.map((p) => (
                <div key={p.key} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                  <span style={p.granted ? undefined : { textDecoration: 'line-through' }}>{permissionLabel(t, p.key)}</span>
                  <Typography.Text type={p.denied ? 'danger' : 'secondary'} className="text-xs">
                    {p.denied
                      ? t('admin.users.viaDeny')
                      : [...p.roles.map((name) => t('admin.users.viaRole', { name })), ...(p.allowed ? [t('admin.users.viaAllow')] : [])].join(' · ')}
                  </Typography.Text>
                </div>
              ))}
            </div>
          ))
        )}
      </section>
    </div>
  )
}

function DrawerFooter({ detail }: { detail: AdminUserDetailDto }) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const canManage = usePermission('admin.users.manage')
  const setDisabled = useSetUserDisabled()
  const revoke = useRevokeUserSessions()
  const { user } = detail

  if (!canManage) return null

  function confirmDisable() {
    modal.confirm({
      title: t('admin.users.disableConfirm'),
      content: t('admin.users.disableContent', { name: user.name }),
      okText: t('admin.users.disable'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => toggleDisabled(true),
    })
  }

  async function toggleDisabled(disabled: boolean) {
    try {
      await setDisabled.mutateAsync({ id: user.id, disabled })
      message.success(disabled ? t('admin.users.disabledDone') : t('admin.users.enabledDone'))
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  async function revokeSessions() {
    try {
      await revoke.mutateAsync(user.id)
      message.success(t('admin.users.sessionsRevoked'))
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  return (
    <div className="flex flex-wrap justify-end gap-2">
      <Button loading={revoke.isPending} onClick={() => void revokeSessions()}>
        {t('admin.users.revokeSessions')}
      </Button>
      {user.disabled ? (
        <Button loading={setDisabled.isPending} onClick={() => void toggleDisabled(false)}>
          {t('admin.users.enable')}
        </Button>
      ) : (
        <Button danger loading={setDisabled.isPending} onClick={confirmDisable}>
          {t('admin.users.disable')}
        </Button>
      )}
    </div>
  )
}
