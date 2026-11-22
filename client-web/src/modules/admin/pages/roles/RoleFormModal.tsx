import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Alert, Form, Input, Modal, Skeleton } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useAccess, usePermission } from '@/modules/identity/hooks/useAccess'
import { isFeatureOn } from '@/modules/identity/lib/access'
import type { RoleDto } from '../../models'
import { useCatalog, useCreateRole, useUpdateRole } from '../../hooks/useAdmin'
import { coveredKeys, toPatterns } from '../../lib/patterns'
import { PermissionMatrix } from './PermissionMatrix'

interface RoleFormModalProps {
  open: boolean
  /** The role to edit, or null to create one. */
  role: RoleDto | null
  onClose: () => void
}

interface RoleForm {
  name: string
  description?: string
}

export function RoleFormModal({ open, role, onClose }: RoleFormModalProps) {
  return open ? <RoleFormModalBody role={role} onClose={onClose} /> : null
}

// Mounted only while open, so each opening starts from the role as it is now.
function RoleFormModalBody({ role, onClose }: Omit<RoleFormModalProps, 'open'>) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<RoleForm>()
  const access = useAccess()
  const { data: catalog, isLoading } = useCatalog()
  const create = useCreateRole()
  const update = useUpdateRole()
  const [keys, setKeys] = useState<Set<string> | null>(null)

  const canManage = usePermission('admin.roles.manage')
  const readOnly = !!role?.isSystem || !canManage
  const current = keys ?? (catalog ? coveredKeys(role?.patterns ?? [], catalog) : new Set<string>())

  async function handleOk() {
    if (readOnly) {
      onClose()
      return
    }
    const values = await form.validateFields()
    const body = {
      name: values.name.trim(),
      description: values.description?.trim() || null,
      patterns: catalog ? toPatterns(current, catalog) : (role?.patterns ?? []),
    }
    try {
      if (role) {
        await update.mutateAsync({ id: role.id, body })
        message.success(t('admin.roles.updated'))
      } else {
        await create.mutateAsync(body)
        message.success(t('admin.roles.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  return (
    <Modal
      open
      width={720}
      title={role ? role.name : t('admin.roles.new')}
      onCancel={onClose}
      onOk={() => void handleOk()}
      okText={readOnly ? t('common.close') : t('common.save')}
      cancelButtonProps={readOnly ? { style: { display: 'none' } } : undefined}
      confirmLoading={create.isPending || update.isPending}
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
      destroyOnHidden
    >
      {role?.isSystem && <Alert type="info" showIcon className="mb-4" title={t('admin.roles.systemReadOnly')} />}
      <Form
        form={form}
        layout="vertical"
        disabled={readOnly}
        initialValues={{ name: role?.name ?? '', description: role?.description ?? '' }}
      >
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <Form.Item
            name="name"
            label={t('admin.roles.name')}
            rules={[{ required: true, whitespace: true, message: t('admin.roles.nameRequired') }]}
          >
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="description" label={t('admin.roles.description')}>
            <Input maxLength={500} />
          </Form.Item>
        </div>
      </Form>
      {isLoading || !catalog ? (
        <Skeleton active />
      ) : (
        <PermissionMatrix
          catalog={catalog}
          value={current}
          onChange={setKeys}
          readOnly={readOnly}
          isFeatureOn={(feature) => isFeatureOn(access, feature)}
        />
      )}
    </Modal>
  )
}
