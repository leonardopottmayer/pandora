import { useTranslation } from 'react-i18next'
import { Input, Tag, Typography, theme } from 'antd'
import type { PageTagDto } from '../models'
import { timeAgo } from '../lib/timeAgo'
import { PageIconPicker } from './PageIconPicker'

interface PageHeaderProps {
  icon: string | null
  title: string
  /** When the page last changed; null while it has never been saved. */
  editedAt: string | null
  /** What autosave is doing, already worded ("Saving...", "Saved"); empty when idle. */
  status: string
  statusIsError: boolean
  tags: PageTagDto[]
  /** Off on a phone reading a page: the title is text, and nothing raises the keyboard. */
  editable: boolean
  onIconChange: (icon: string | null) => void
  onTitleChange: (title: string) => void
  onSelectTag: (tagId: string) => void
}

/** The top of a page, Notion-style: the emoji and the title as part of the document, then a quiet line. */
export function PageHeader({
  icon,
  title,
  editedAt,
  status,
  statusIsError,
  tags,
  editable,
  onIconChange,
  onTitleChange,
  onSelectTag,
}: PageHeaderProps) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()

  return (
    <div className="flex flex-col gap-2 pb-6">
      {editable ? (
        <div className="-ms-1.5">
          <PageIconPicker value={icon} onChange={onIconChange} size="large" />
        </div>
      ) : (
        icon && <span className="text-4xl leading-none">{icon}</span>
      )}

      {editable ? (
        <Input.TextArea
          variant="borderless"
          autoSize
          value={title}
          placeholder={t('notes.titlePlaceholder')}
          aria-label={t('notes.titlePlaceholder')}
          onChange={(e) => onTitleChange(e.target.value.replace(/\n/g, ' '))}
          // A title is one line: Enter would only add a break that the save throws away.
          onPressEnter={(e) => e.preventDefault()}
          className="!p-0 !text-3xl !font-bold !leading-tight"
          style={{ resize: 'none' }}
        />
      ) : (
        <Typography.Title level={1} className="!mb-0 !text-3xl !leading-tight">
          {title || t('notes.untitled')}
        </Typography.Title>
      )}

      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs" style={{ color: token.colorTextTertiary }}>
        {editedAt && <span>{t('notes.edited', { when: timeAgo(editedAt, i18n.language) })}</span>}
        {status && <span style={statusIsError ? { color: token.colorError } : undefined}>{status}</span>}
        {/* The tags the saved body mentions — clicking one narrows the sidebar to it. */}
        {tags.map((tag) => (
          <Tag
            key={tag.id}
            color={tag.color ?? undefined}
            className="!me-0 cursor-pointer"
            onClick={() => onSelectTag(tag.id)}
          >
            #{tag.name}
          </Tag>
        ))}
      </div>
    </div>
  )
}
