import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Input, Table, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, PlusOutlined, SearchOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TagDto } from '../../models'
import { useDeleteTag, useTags } from '../../hooks/useTags'
import { TagFormModal } from './TagFormModal'
import { TagLabel } from './TagLabel'

export function TagsListPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TagDto | null>(null)
  const [text, setText] = useState('')
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const { data: tags = [], isLoading } = useTags()
  const deleteMutation = useDeleteTag()
  const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase().trim()
  const filtered = tags.filter((tag) => normalize(tag.name).includes(normalize(text)))

  function openForm(tag: TagDto | null) {
    setEditing(tag)
    setModalOpen(true)
  }

  function confirmDelete(tag: TagDto) {
    modal.confirm({
      title: t('finances.tags.deleteConfirm'),
      content: tag.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync(tag.id)
          message.success(t('finances.tags.deleted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.tags.deleteError')))
          throw err
        }
      },
    })
  }

  const columns: ColumnsType<TagDto> = [
    { title: t('finances.tags.name'), dataIndex: 'name', render: (name: string, tag) => <TagLabel name={name} color={tag.color} /> },
    {
      key: 'actions', width: 48, align: 'right',
      render: (_, tag) => (
        <span onClick={(event) => event.stopPropagation()}>
          <Dropdown trigger={['click']} placement="bottomRight" menu={{ items: [
            { key: 'edit', label: t('common.edit'), onClick: () => openForm(tag) },
            { type: 'divider' },
            { key: 'delete', label: t('common.delete'), danger: true, onClick: () => confirmDelete(tag) },
          ] }}>
            <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('finances.tags.moreActions', { name: tag.name })} />
          </Dropdown>
        </span>
      ),
    },
  ]

  return (
    <div className="mx-auto flex w-full min-w-0 max-w-5xl flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.tags')} description={t('finances.tags.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>{t('finances.tags.new')}</Button>
      </div>
      <div className="w-full shrink-0 sm:w-64">
        <Input allowClear prefix={<SearchOutlined />} placeholder={t('finances.tags.search')} value={text} onChange={(event) => setText(event.target.value)} />
      </div>
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          <Table rowKey="id" loading={isLoading} dataSource={filtered} columns={columns} tableLayout="fixed" size="middle" pagination={false}
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            onRow={(tag) => ({ onClick: () => openForm(tag), className: 'cursor-pointer' })}
            locale={{ emptyText: t(tags.length ? 'finances.tags.noMatches' : 'finances.tags.empty') }} />
        </div>
        <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
          {t('finances.tags.count', { count: filtered.length })}
        </Typography.Text>
      </div>
      <TagFormModal open={modalOpen} tag={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
