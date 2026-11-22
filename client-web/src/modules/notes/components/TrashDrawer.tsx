import { useTranslation } from 'react-i18next'
import { App, Button, Drawer, Empty, Spin, Typography } from 'antd'
import { DeleteOutlined, UndoOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useTrash, useTrashAction } from '../hooks/usePages'
import { timeAgo } from '../lib/timeAgo'

interface TrashDrawerProps {
  open: boolean
  onClose: () => void
  onRestored: (pageId: string) => void
}

/** Deleted pages, newest first: each comes back with its sub-pages, or goes for good. */
export function TrashDrawer({ open, onClose, onRestored }: TrashDrawerProps) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { data: items, isLoading } = useTrash(open)
  const action = useTrashAction()

  async function restore(id: string) {
    try {
      await action.mutateAsync({ id, action: 'restore' })
      message.success(t('notes.trash.restored'))
      onRestored(id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function remove(id: string | null, title?: string) {
    modal.confirm({
      title: id ? t('notes.trash.deleteConfirm', { title: title || t('notes.untitled') }) : t('notes.trash.emptyConfirm'),
      content: t('notes.trash.forGood'),
      okText: id ? t('notes.trash.deleteForGood') : t('notes.trash.empty'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await action.mutateAsync({ id, action: 'delete' })
        } catch (err) {
          message.error(toErrorMessage(err, t('notes.actionError')))
        }
      },
    })
  }

  return (
    <Drawer
      title={t('notes.trash.title')}
      open={open}
      onClose={onClose}
      size={420}
      extra={items?.length ? <Button danger size="small" onClick={() => remove(null)}>{t('notes.trash.empty')}</Button> : null}
    >
      {isLoading ? <Spin /> : !items?.length ? <Empty description={t('notes.trash.nothing')} /> : (
        <ul className="m-0 flex list-none flex-col p-0">
          {items.map((item) => (
            <li key={item.id} className="flex items-center gap-3 py-2.5" style={{ borderBottom: '1px solid var(--ant-color-border-secondary)' }}>
              <span aria-hidden="true" className="text-lg">{item.icon ?? '📄'}</span>
              <span className="flex min-w-0 flex-1 flex-col">
                <Typography.Text className="truncate">{item.title || t('notes.untitled')}</Typography.Text>
                <Typography.Text type="secondary" className="text-xs">
                  {[t('notes.trash.deletedWhen', { when: timeAgo(item.deletedAt, i18n.language) }), item.subpageCount ? t('notes.subpageCount', { count: item.subpageCount }) : null].filter(Boolean).join(' · ')}
                </Typography.Text>
              </span>
              <Button size="small" icon={<UndoOutlined />} onClick={() => void restore(item.id)}>{t('notes.trash.restore')}</Button>
              <Button size="small" danger type="text" icon={<DeleteOutlined />} aria-label={t('notes.trash.deleteForGood')} onClick={() => remove(item.id, item.title)} />
            </li>
          ))}
        </ul>
      )}
    </Drawer>
  )
}
