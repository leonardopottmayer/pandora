import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { App, Breadcrumb, Button, Dropdown, Form, Input, Modal, Radio, Skeleton, Tag, Typography, theme } from 'antd'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { ASSET_BEHAVIORS, type AssetBehavior, type AssetClassDto } from '../../models'
import { assetClassLabel } from '../../lib/investments'
import {
  useAssetClasses,
  useCreateAssetClass,
  useRenameAssetClass,
  useSetAssetClassArchived,
} from '../../hooks/useInvestments'

/** The system's asset classes, read-only, and the user's own, which can be created, renamed and archived. */
export function AssetClassesPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const [form] = Form.useForm<{ name: string; behavior: AssetBehavior }>()
  const [editing, setEditing] = useState<AssetClassDto | 'new' | null>(null)
  const { data: classes, isLoading } = useAssetClasses()
  const create = useCreateAssetClass()
  const rename = useRenameAssetClass()
  const archive = useSetAssetClassArchived()

  function openForm(item: AssetClassDto | 'new') {
    setEditing(item)
    form.setFieldsValue(item === 'new' ? { name: '', behavior: 'unit' } : { name: item.name, behavior: item.behavior })
  }

  async function handleSubmit(values: { name: string; behavior: AssetBehavior }) {
    try {
      if (editing === 'new') await create.mutateAsync({ name: values.name.trim(), behavior: values.behavior })
      else if (editing) await rename.mutateAsync({ id: editing.id, name: values.name.trim() })
      message.success(t('finances.investments.classSaved'))
      setEditing(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  async function toggleArchived(item: AssetClassDto) {
    try {
      await archive.mutateAsync({ id: item.id, archived: !item.archivedAt })
      message.success(t('finances.investments.classSaved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  const divider = `1px solid ${token.colorBorderSecondary}`
  const own = (classes ?? []).filter((c) => !c.isSystem)
  const system = (classes ?? []).filter((c) => c.isSystem)

  const row = (c: AssetClassDto, index: number) => (
    <div key={c.id} className="flex items-center gap-3 px-4 py-2.5" style={{ borderTop: index === 0 ? undefined : divider }}>
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="flex min-w-0 items-center gap-2">
          <Typography.Text type={c.archivedAt ? 'secondary' : undefined} className="truncate">
            {assetClassLabel(c, t)}
          </Typography.Text>
          {c.archivedAt && <Tag>{t('finances.investments.archivedStatus')}</Tag>}
        </span>
        <Typography.Text type="secondary" className="text-xs">
          {t(`finances.investments.behaviors.${c.behavior}`)}
        </Typography.Text>
      </span>
      {!c.isSystem && (
        <Dropdown
          trigger={['click']}
          placement="bottomRight"
          menu={{
            items: [
              ...(c.archivedAt ? [] : [{ key: 'rename', label: t('finances.investments.rename'), onClick: () => openForm(c) }]),
              {
                key: 'archive',
                label: c.archivedAt ? t('finances.investments.unarchive') : t('finances.investments.archive'),
                onClick: () => void toggleArchived(c),
              },
            ],
          }}
        >
          <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('finances.investments.classActions', { name: c.name })} />
        </Dropdown>
      )}
    </div>
  )

  const sectionTitle = (text: string) => (
    <Typography.Text strong type="secondary" className="px-0.5 text-xs uppercase tracking-wider">
      {text}
    </Typography.Text>
  )

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <Breadcrumb
        items={[{ title: <Link to="/finances/investments">{t('nav.investments')}</Link> }, { title: t('finances.investments.classesTitle') }]}
      />
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('finances.investments.classesTitle')} description={t('finances.investments.classesSubtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm('new')}>
          {t('finances.investments.newClass')}
        </Button>
      </div>

      {isLoading ? (
        <Skeleton active />
      ) : (
        <>
          <div className="flex flex-col gap-2">
            {sectionTitle(t('finances.investments.ownClasses'))}
            <div className="overflow-hidden rounded-xl" style={{ border: divider, background: token.colorBgContainer }}>
              {own.length === 0 ? (
                <Typography.Text type="secondary" className="block px-4 py-2.5">
                  {t('finances.investments.noOwnClasses')}
                </Typography.Text>
              ) : (
                own.map(row)
              )}
            </div>
          </div>
          <div className="flex flex-col gap-2">
            {sectionTitle(t('finances.investments.systemClasses'))}
            <div className="overflow-hidden rounded-xl" style={{ border: divider, background: token.colorBgContainer }}>
              {system.map(row)}
            </div>
          </div>
        </>
      )}

      <Modal
        open={!!editing}
        title={editing === 'new' ? t('finances.investments.newClass') : t('finances.investments.renameClass')}
        onCancel={() => setEditing(null)}
        onOk={() => form.submit()}
        okButtonProps={{ loading: create.isPending || rename.isPending }}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        destroyOnHidden
        forceRender
      >
        <Form form={form} layout="vertical" onFinish={handleSubmit}>
          <Form.Item name="name" label={t('finances.investments.className')} rules={[{ required: true, message: t('finances.investments.required.className') }]}>
            <Input maxLength={60} autoFocus />
          </Form.Item>
          <Form.Item
            name="behavior"
            label={t('finances.investments.behavior')}
            extra={t('finances.investments.behaviorHint')}
          >
            <Radio.Group
              disabled={editing !== 'new'}
              options={ASSET_BEHAVIORS.map((b) => ({ value: b, label: t(`finances.investments.behaviors.${b}`) }))}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}
