import { useTranslation } from 'react-i18next'
import { ClockCircleOutlined } from '@ant-design/icons'
import { useRecentPages } from '../hooks/usePages'

/** The pages opened last, any space, a click away above the tree. */
export function RecentPages({ selectedId, onSelect }: { selectedId: string | null; onSelect: (id: string) => void }) {
  const { t } = useTranslation()
  const { data: pages } = useRecentPages()
  const shown = (pages ?? []).filter((page) => page.id !== selectedId).slice(0, 5)
  if (!shown.length) return null
  return (
    <details className="notes-recent mb-1">
      <summary className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1 hover:bg-[var(--ant-color-fill-tertiary)]">
        <ClockCircleOutlined />
        <span>{t('notes.recent.title')}</span>
      </summary>
      <ul className="m-0 list-none p-0 ps-5">
        {shown.map((page) => (
          <li key={page.id}>
            <button
              type="button"
              className="flex w-full cursor-pointer items-center gap-2 rounded px-2 py-0.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
              style={{ border: 0, background: 'transparent', color: 'inherit' }}
              onClick={() => onSelect(page.id)}
            >
              <span aria-hidden="true">{page.icon ?? '📄'}</span>
              <span className="truncate">{page.title || t('notes.untitled')}</span>
            </button>
          </li>
        ))}
      </ul>
    </details>
  )
}
