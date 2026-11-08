import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { App } from 'antd'
import { CalendarOutlined, LoadingOutlined } from '@ant-design/icons'
import { useFeature, usePermission } from '@/modules/identity/hooks/useAccess'
import { toErrorMessage } from '@/lib/api/envelope'
import { useOpenDailyNote } from '../hooks/useDailyNotes'

/** Opens the account's note for today; the server decides the date in the account's zone. */
export function DailyNoteButton({ onOpen }: { onOpen?: (id: string) => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { message } = App.useApp()
  const open = useOpenDailyNote()
  const enabled = useFeature('notes')
  const canRead = usePermission('notes.pages.read')
  const canCreate = usePermission('notes.pages.create')
  const canCreateFolder = usePermission('notes.folders.create')
  const canReadSpaces = usePermission('notes.spaces.read')
  if (!enabled || !canRead || !canCreate || !canCreateFolder || !canReadSpaces) return null

  async function handleOpen() {
    try {
      const page = await open.mutateAsync(undefined)
      if (onOpen) onOpen(page.id)
      else navigate(`/notes/${page.id}`)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.daily.openError')))
    }
  }

  return (
    <button type="button" className="notes-shortcut" aria-label={t('notes.daily.today')} disabled={open.isPending} onClick={() => void handleOpen()}>
      {open.isPending ? <LoadingOutlined /> : <CalendarOutlined />}
      <span>{t('notes.daily.today')}</span>
    </button>
  )
}
