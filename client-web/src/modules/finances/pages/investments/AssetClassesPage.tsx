import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { App, Breadcrumb, Button, Dropdown, Form, Input, Modal, Radio, Skeleton, Space, Tag, Typography, theme } from 'antd'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { ASSET_BEHAVIORS, type AssetBehavior, type AssetClassDto, type AssetDto } from '../../models'
import { assetClassLabel } from '../../lib/investments'
import {
  useAssetClasses,
  useAssetClassMap,
  useAssets,
  useCreateAssetClass,
  useDeleteAsset,
  useDeleteAssetClass,
  usePositions,
  useRenameAssetClass,
  useSetAssetArchived,
  useSetAssetClassArchived,
} from '../../hooks/useInvestments'
import { AssetFormModal } from './AssetFormModal'

/**
 * The user's assets, which can be created, edited and archived even without a position, and deleted while
 * they never had one; the system's
 * asset classes, read-only; and the user's own classes, which can be created, renamed, archived, and
 * deleted while no asset uses them.
 */
export function AssetClassesPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [form] = Form.useForm<{ name: string; behavior: AssetBehavior }>()
  const [editing, setEditing] = useState<AssetClassDto | 'new' | null>(null)
  const { data: classes, isLoading } = useAssetClasses()
  const create = useCreateAssetClass()
  const rename = useRenameAssetClass()
  const archive = useSetAssetClassArchived()
  const remove = useDeleteAssetClass()
  const { data: assets, isLoading: assetsLoading } = useAssets()
  // Closed positions count too: an asset that ever had one is history, so it is archived, not deleted.
  const { data: positions } = usePositions({ includeClosed: true })
  const classMap = useAssetClassMap()
  const archiveAsset = useSetAssetArchived()
  const removeAsset = useDeleteAsset()
  const [assetForm, setAssetForm] = useState<AssetDto | 'new' | null>(null)

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

  function confirmDelete(item: AssetClassDto) {
    modal.confirm({
      title: t('finances.investments.deleteClassConfirm'),
      content: item.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await remove.mutateAsync(item.id)
          message.success(t('finances.investments.classDeleted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.investments.saveError')))
        }
      },
    })
  }

  function confirmDeleteAsset(item: AssetDto) {
    modal.confirm({
      title: t('finances.investments.deleteAssetConfirm'),
      content: item.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await removeAsset.mutateAsync(item.id)
          message.success(t('finances.investments.assetDeleted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.investments.saveError')))
        }
      },
    })
  }

  async function toggleAssetArchived(item: AssetDto) {
    try {
      await archiveAsset.mutateAsync({ id: item.id, archived: !item.archivedAt })
      message.success(t('finances.investments.assetSaved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  const divider = `1px solid ${token.colorBorderSecondary}`
  const openCount = new Map<string, number>()
  for (const p of positions ?? []) if (p.status === 'open') openCount.set(p.assetId, (openCount.get(p.assetId) ?? 0) + 1)
  const withPositions = new Set((positions ?? []).map((p) => p.assetId))
  // Archived assets go last; the API already sorts by name.
  const sortedAssets = [...(assets ?? [])].sort((a, b) => Number(!!a.archivedAt) - Number(!!b.archivedAt))

  const assetRow = (a: AssetDto, index: number) => {
    const assetClass = classMap.get(a.assetClassId)
    const count = openCount.get(a.id) ?? 0
    const details = [
      assetClass ? assetClassLabel(assetClass, t) : null,
      a.code,
      a.currency,
      count ? t('finances.investments.openPositionsCount', { count }) : t('finances.investments.noOpenPositions'),
    ].filter(Boolean)
    return (
      <div key={a.id} className="flex items-center gap-3 px-4 py-2.5" style={{ borderTop: index === 0 ? undefined : divider }}>
        <span className="flex min-w-0 flex-1 flex-col">
          <span className="flex min-w-0 items-center gap-2">
            <Typography.Text type={a.archivedAt ? 'secondary' : undefined} className="truncate">
              {a.name}
            </Typography.Text>
            {a.archivedAt && <Tag>{t('finances.investments.archivedStatus')}</Tag>}
          </span>
          <Typography.Text type="secondary" className="text-xs">
            {details.join(' · ')}
          </Typography.Text>
        </span>
        <Dropdown
          trigger={['click']}
          placement="bottomRight"
          menu={{
            items: [
              ...(a.archivedAt ? [] : [{ key: 'edit', label: t('common.edit'), onClick: () => setAssetForm(a) }]),
              {
                key: 'archive',
                label: a.archivedAt ? t('finances.investments.unarchive') : t('finances.investments.archive'),
                onClick: () => void toggleAssetArchived(a),
              },
              ...(withPositions.has(a.id) ? [] : [{ key: 'delete', label: t('common.delete'), danger: true, onClick: () => confirmDeleteAsset(a) }]),
            ],
          }}
        >
          <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('finances.investments.assetActions', { name: a.name })} />
        </Dropdown>
      </div>
    )
  }
  const own = (classes ?? []).filter((c) => !c.isSystem)
  // Archived assets count too: the class can only go once nothing points at it.
  const usedClasses = new Set((assets ?? []).map((a) => a.assetClassId))
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
              ...(usedClasses.has(c.id) ? [] : [{ key: 'delete', label: t('common.delete'), danger: true, onClick: () => confirmDelete(c) }]),
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
        <Space wrap>
          <Button onClick={() => openForm('new')}>{t('finances.investments.newClass')}</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setAssetForm('new')}>
            {t('finances.investments.newAsset')}
          </Button>
        </Space>
      </div>

      {isLoading || assetsLoading ? (
        <Skeleton active />
      ) : (
        <>
          <div className="flex flex-col gap-2">
            {sectionTitle(t('finances.investments.assets'))}
            <div className="overflow-hidden rounded-xl" style={{ border: divider, background: token.colorBgContainer }}>
              {sortedAssets.length === 0 ? (
                <Typography.Text type="secondary" className="block px-4 py-2.5">
                  {t('finances.investments.noAssets')}
                </Typography.Text>
              ) : (
                sortedAssets.map(assetRow)
              )}
            </div>
          </div>
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

      <AssetFormModal
        open={!!assetForm}
        asset={assetForm === 'new' ? undefined : (assetForm ?? undefined)}
        onClose={() => setAssetForm(null)}
      />

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
