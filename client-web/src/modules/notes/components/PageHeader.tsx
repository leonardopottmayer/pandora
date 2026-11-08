import { useTranslation } from 'react-i18next'
import { Input, Select, Tag, Typography, theme } from 'antd'
import type { TagDto } from '../models'
import { tagSlug } from '../lib/tags'
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
  /** The page's tags block, as written. */
  tags: string[]
  /** Every tag of the user, to offer and to color. */
  knownTags: TagDto[]
  /** Off on a phone reading a page: the title is text, and nothing raises the keyboard. */
  editable: boolean
  onIconChange: (icon: string | null) => void
  onTitleChange: (title: string) => void
  onTagsChange: (tags: string[]) => void
  onSelectTag: (tagId: string) => void
  /** Where the page stands (locked, verified), beside the edit time. */
  badges?: React.ReactNode
}

/** One tag per slug, in the order written; names that normalize to nothing are dropped. */
function distinctTags(names: string[]): string[] {
  const seen = new Set<string>()
  return names.map((name) => name.trim().replace(/^#/, '')).filter((name) => {
    const slug = tagSlug(name)
    if (!slug || seen.has(slug)) return false
    seen.add(slug)
    return true
  })
}

/** The top of a page, Notion-style: the emoji, the title, the tags block, then a quiet line. */
export function PageHeader({
  icon,
  title,
  editedAt,
  status,
  statusIsError,
  tags,
  knownTags,
  editable,
  onIconChange,
  onTitleChange,
  onTagsChange,
  onSelectTag,
  badges,
}: PageHeaderProps) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()
  const known = (name: string) => knownTags.find((tag) => tag.slug === tagSlug(name))

  const chip = (name: string, onClose?: () => void) => {
    const tag = known(name)
    return (
      <Tag
        key={name}
        color={tag?.color ?? undefined}
        closable={!!onClose}
        onClose={onClose}
        className="!me-1 cursor-pointer"
        // Down, not click, inside the field: a click would open its dropdown first.
        onMouseDown={(event) => event.stopPropagation()}
        onClick={() => tag && onSelectTag(tag.id)}
      >
        #{name}
      </Tag>
    )
  }

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

      {editable ? (
        <Select
          mode="tags"
          variant="borderless"
          className="notes-tags-field -ms-2.5"
          value={tags}
          placeholder={t('notes.tagsPlaceholder')}
          aria-label={t('notes.tagsLabel')}
          tokenSeparators={[',', ' ']}
          options={knownTags.map((tag) => ({ value: tag.name, label: `#${tag.name}` }))}
          tagRender={({ value, onClose }) => chip(String(value), onClose)}
          onChange={(values: string[]) => onTagsChange(distinctTags(values))}
        />
      ) : (
        tags.length > 0 && <div className="flex flex-wrap gap-y-1">{tags.map((name) => chip(name))}</div>
      )}

      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs" style={{ color: token.colorTextTertiary }}>
        {badges}
        {editedAt && <span>{t('notes.edited', { when: timeAgo(editedAt, i18n.language) })}</span>}
        {status && <span style={statusIsError ? { color: token.colorError } : undefined}>{status}</span>}
      </div>
    </div>
  )
}
