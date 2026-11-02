import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { App, Button, Dropdown, Empty, Skeleton, Switch, Tag, Typography, theme } from 'antd'
import type { MenuProps } from 'antd'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { SpaceDto } from '../models'
import { useDeleteSpace, useSetDefaultSpace, useSetSpaceArchived, useSpaces } from '../hooks/useSpaces'
import { SpaceFormModal } from '../components/SpaceFormModal'
import { SpaceIcon } from '../components/SpaceIcon'
import { timeAgo } from '../lib/timeAgo'

/** Where Notes opens: the spaces as cards, each leading to its own tree. */
export function SpacesPage() {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const [includeArchived, setIncludeArchived] = useState(false)
  const [editing, setEditing] = useState<SpaceDto | null>(null)
  const [formOpen, setFormOpen] = useState(false)

  const { data: spaces, isLoading } = useSpaces(includeArchived)
  const setDefault = useSetDefaultSpace()
  const setArchived = useSetSpaceArchived()
  const deleteSpace = useDeleteSpace()

  function openForm(space: SpaceDto | null) {
    setEditing(space)
    setFormOpen(true)
  }

  async function run(action: () => Promise<unknown>) {
    try {
      await action()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function confirmDelete(space: SpaceDto) {
    modal.confirm({
      title: t('notes.spaces.deleteConfirm'),
      content: space.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => run(() => deleteSpace.mutateAsync(space.id)),
    })
  }

  function menu(space: SpaceDto): MenuProps['items'] {
    // The default space takes pages nobody placed, so it is neither hidden nor removed.
    return [
      { key: 'edit', label: t('common.edit'), onClick: () => openForm(space) },
      ...(space.isDefault
        ? []
        : [
            { key: 'default', label: t('notes.spaces.makeDefault'), onClick: () => run(() => setDefault.mutateAsync(space.id)) },
            {
              key: 'archive',
              label: t(space.isArchived ? 'notes.unarchive' : 'notes.archive'),
              onClick: () => run(() => setArchived.mutateAsync({ id: space.id, archived: !space.isArchived })),
            },
            { type: 'divider' as const },
            // A space with pages is refused by the backend; saying so up front saves the round trip.
            space.pageCount > 0
              ? { key: 'delete', label: t('notes.spaces.deleteNotEmpty'), disabled: true }
              : { key: 'delete', danger: true, label: t('common.delete'), onClick: () => confirmDelete(space) },
          ]),
    ]
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('notes.spaces.title')} description={t('notes.spaces.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
          {t('notes.spaces.new')}
        </Button>
      </div>

      {isLoading ? (
        <Skeleton active />
      ) : (spaces ?? []).length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('notes.spaces.empty')}>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
            {t('notes.spaces.new')}
          </Button>
        </Empty>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {(spaces ?? []).map((space) => (
            <div
              key={space.id}
              role="link"
              tabIndex={0}
              aria-label={space.name}
              className="flex cursor-pointer flex-col gap-3 rounded-xl p-4 transition-shadow hover:shadow-md"
              style={{
                border: `1px solid ${token.colorBorderSecondary}`,
                background: token.colorBgContainer,
                opacity: space.isArchived ? 0.6 : 1,
              }}
              onClick={() => navigate(`/notes/s/${space.id}`)}
              onKeyDown={(e) => e.key === 'Enter' && navigate(`/notes/s/${space.id}`)}
            >
              <div className="flex items-start gap-3">
                <SpaceIcon space={space} size={40} />
                <div className="flex min-w-0 flex-1 flex-col">
                  <Typography.Text strong className="truncate text-base">
                    {space.name}
                  </Typography.Text>
                  <span className="flex gap-1">
                    {space.isDefault && <Tag className="!me-0">{t('notes.spaces.default')}</Tag>}
                    {space.isArchived && <Tag className="!me-0">{t('notes.spaces.archived')}</Tag>}
                  </span>
                </div>
                <Dropdown trigger={['click']} menu={{ items: menu(space) }}>
                  <Button
                    type="text"
                    size="small"
                    icon={<EllipsisOutlined />}
                    aria-label={t('notes.spaces.moreActions', { name: space.name })}
                    onClick={(e) => e.stopPropagation()}
                  />
                </Dropdown>
              </div>

              {space.description && (
                <Typography.Paragraph type="secondary" ellipsis={{ rows: 2 }} className="!mb-0">
                  {space.description}
                </Typography.Paragraph>
              )}

              <Typography.Text type="secondary" className="mt-auto text-xs">
                {t('notes.spaces.pageCount', { count: space.pageCount })}
                {space.lastEditedAt && ` · ${t('notes.spaces.edited', { when: timeAgo(space.lastEditedAt, i18n.language) })}`}
              </Typography.Text>
            </div>
          ))}
        </div>
      )}

      <label className="flex w-fit cursor-pointer items-center gap-2">
        <Switch size="small" checked={includeArchived} onChange={setIncludeArchived} />
        <Typography.Text type="secondary" className="text-xs">
          {t('notes.spaces.showArchived')}
        </Typography.Text>
      </label>

      <SpaceFormModal
        open={formOpen}
        space={editing}
        onClose={() => setFormOpen(false)}
        onSaved={(saved) => !editing && navigate(`/notes/s/${saved.id}`)}
      />
    </div>
  )
}
