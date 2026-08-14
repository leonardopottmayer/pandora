import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Empty, Flex, Input, Modal, Spin, Typography, theme } from 'antd'
import { FileTextOutlined, SearchOutlined } from '@ant-design/icons'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useSearchPages } from '../hooks/usePages'

interface SearchPaletteProps {
  /** Opens the chosen page. */
  onSelect: (id: string) => void
}

/**
 * Ctrl+K (Cmd+K on a Mac) command palette: full-text search over the user's pages, driven from the
 * keyboard end to end — type, arrow through the hits, Enter to open, Esc to close.
 */
export function SearchPalette({ onSelect }: SearchPaletteProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  const [open, setOpen] = useState(false)
  const [term, setTerm] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)

  const debouncedTerm = useDebouncedValue(term)
  const { data: results = [], isFetching } = useSearchPages(debouncedTerm)

  // Capture phase: the editor below has its own key handling, and the palette wins over it.
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        setOpen(true)
      }
    }
    window.addEventListener('keydown', handleKeyDown, true)
    return () => window.removeEventListener('keydown', handleKeyDown, true)
  }, [])

  // A shorter list may no longer reach where the cursor was.
  if (activeIndex > 0 && activeIndex >= results.length) setActiveIndex(0)

  function close() {
    setOpen(false)
    setTerm('')
    setActiveIndex(0)
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

      <div style={{ maxHeight: 360, overflowY: 'auto', padding: 4 }}>
        {results.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={debouncedTerm.trim() ? t('notes.searchEmpty') : t('notes.searchHint')}
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
                <Typography.Text strong ellipsis>
                  {result.title || t('notes.untitled')}
                </Typography.Text>
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
