import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Empty, Flex, Input, Modal, Segmented, Spin, Typography, theme } from 'antd'
import { FileTextOutlined, SearchOutlined } from '@ant-design/icons'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useSearchPages } from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'
import { SpaceIcon } from './SpaceIcon'
import { TagFilter } from './TagFilter'

interface SearchPaletteProps {
  open: boolean
  /** Opened by Ctrl+K from within, or by the sidebar's search button from outside. */
  onOpenChange: (open: boolean) => void
  /** Opens the chosen page. */
  onSelect: (id: string) => void
  /** The space on screen: the search starts there, and can widen to every space. */
  spaceId?: string | null
}

/**
 * Ctrl+K (Cmd+K on a Mac) command palette: full-text search over the user's pages, driven from the
 * keyboard end to end — type, arrow through the hits, Enter to open, Esc to close. The shortcut
 * lives here; whether the palette is open is the page's state, so the sidebar can raise it too.
 */
export function SearchPalette({ open, onOpenChange, onSelect, spaceId = null }: SearchPaletteProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  const [term, setTerm] = useState('')
  const [tagIds, setTagIds] = useState<string[]>([])
  const [activeIndex, setActiveIndex] = useState(0)
  const [everySpace, setEverySpace] = useState(false)
  const { data: spaces = [] } = useSpaces(true)
  const spaceOf = new Map(spaces.map((space) => [space.id, space]))

  const debouncedTerm = useDebouncedValue(term)
  const { data: results = [], isFetching } = useSearchPages(
    debouncedTerm,
    tagIds,
    everySpace ? null : spaceId,
  )

  // Capture phase: the editor below has its own key handling, and the palette wins over it.
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        onOpenChange(true)
      }
    }
    window.addEventListener('keydown', handleKeyDown, true)
    return () => window.removeEventListener('keydown', handleKeyDown, true)
  }, [onOpenChange])

  // A shorter list may no longer reach where the cursor was.
  if (activeIndex > 0 && activeIndex >= results.length) setActiveIndex(0)

  function close() {
    onOpenChange(false)
    setTerm('')
    setTagIds([])
    setActiveIndex(0)
    setEverySpace(false)
  }

  function choose(id: string) {
    close()
    onSelect(id)
  }

  function handleKeyDown(event: React.KeyboardEvent) {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActiveIndex((index) => (results.length === 0 ? 0 : (index + 1) % results.length))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActiveIndex((index) => (results.length === 0 ? 0 : (index - 1 + results.length) % results.length))
    } else if (event.key === 'Enter' && results[activeIndex]) {
      choose(results[activeIndex].id)
    }
  }

  return (
    <Modal
      open={open}
      onCancel={close}
      footer={null}
      closable={false}
      destroyOnHidden
      width={560}
      styles={{ body: { padding: 0 } }}
      style={{ top: 96 }}
    >
      <Input
        autoFocus
        variant="borderless"
        size="large"
        prefix={<SearchOutlined style={{ color: token.colorTextTertiary }} />}
        suffix={isFetching ? <Spin size="small" /> : null}
        placeholder={t('notes.searchPlaceholder')}
        value={term}
        onChange={(e) => {
          setTerm(e.target.value)
          setActiveIndex(0)
        }}
        onKeyDown={handleKeyDown}
        style={{ borderBottom: `1px solid ${token.colorBorderSecondary}`, borderRadius: 0 }}
      />

      <div
        className="flex flex-wrap items-center gap-2"
        style={{ padding: 8, borderBottom: `1px solid ${token.colorBorderSecondary}` }}
      >
        <div className="min-w-48 flex-1">
          <TagFilter value={tagIds} onChange={setTagIds} placeholder={t('notes.searchByTag')} />
        </div>
        {spaceId && (
          <Segmented
            size="small"
            value={everySpace ? 'all' : 'here'}
            onChange={(value) => setEverySpace(value === 'all')}
            options={[
              { value: 'here', label: t('notes.spaces.thisSpace') },
              { value: 'all', label: t('notes.spaces.all') },
            ]}
          />
        )}
      </div>

      <div style={{ maxHeight: 360, overflowY: 'auto', padding: 4 }}>
        {results.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              debouncedTerm.trim() || tagIds.length > 0 ? t('notes.searchEmpty') : t('notes.searchHint')
            }
            style={{ margin: '24px 0' }}
          />
        ) : (
          results.map((result, index) => (
            <div
              key={result.id}
              role="option"
              aria-selected={index === activeIndex}
              onClick={() => choose(result.id)}
              onMouseEnter={() => setActiveIndex(index)}
              style={{
                padding: '8px 12px',
                borderRadius: token.borderRadius,
                cursor: 'pointer',
                background: index === activeIndex ? token.controlItemBgHover : undefined,
                opacity: result.isArchived ? 0.6 : 1,
              }}
            >
              <Flex align="center" gap={8}>
                {result.icon ? <span>{result.icon}</span> : <FileTextOutlined />}
                <Typography.Text strong ellipsis className="flex-1">
                  {result.title || t('notes.untitled')}
                </Typography.Text>
                {/* Which space a hit is in only matters once the search spans them. */}
                {(everySpace || !spaceId) && spaceOf.get(result.spaceId) && (
                  <span className="flex shrink-0 items-center gap-1 text-xs" style={{ color: token.colorTextTertiary }}>
                    <SpaceIcon space={spaceOf.get(result.spaceId)!} size={16} />
                    {spaceOf.get(result.spaceId)!.name}
                  </span>
                )}
              </Flex>
              {result.excerpt && (
                <Typography.Paragraph
                  type="secondary"
                  ellipsis={{ rows: 1 }}
                  style={{ margin: '2px 0 0 24px', fontSize: 12 }}
                >
                  {result.excerpt}
                </Typography.Paragraph>
              )}
            </div>
          ))
        )}
      </div>
    </Modal>
  )
}
