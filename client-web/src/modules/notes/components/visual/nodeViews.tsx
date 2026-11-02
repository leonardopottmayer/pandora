import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import {
  AlignCenterOutlined,
  AlignLeftOutlined,
  AlignRightOutlined,
  CodeOutlined,
  PicCenterOutlined,
} from '@ant-design/icons'
import { App } from 'antd'
import { formatBytes } from '@/lib/formatBytes'
import { useMediaLookup } from '../../hooks/useMedia'
import { useMediaPathUrls } from '../../hooks/useMediaUrls'
import { sanitizeNoteHtml } from '../../lib/embeds'
import { isMediaUrl, mediaFileName } from '../../lib/media'
import { fileGlyph, formatImageAlt, mediaIdOf, parseImageAlt, type ImageAlign } from '../../lib/mediaMarkdown'
import { downloadMedia } from '../../services/media.service'
import { resolveWikilink } from '../../lib/wikilinks'
import { MarkdownPreview } from '../MarkdownPreview'
import { useVisualEditorContext } from './visualContext'

// How our own nodes draw inside the visual editor. Each one looks the way the preview draws it, so
// writing and reading show the same page.

export function WikilinkView({ node }: NodeViewProps) {
  const { pageIndex, onOpenPage, onCreatePage } = useVisualEditorContext()
  const target = String(node.attrs.target).trim()
  const page = resolveWikilink(target, pageIndex)
  const label = String(node.attrs.alias ?? '').trim() || (page ? page.title : target)

  return (
    <NodeViewWrapper
      as="span"
      className={page ? 'notes-wikilink' : 'notes-wikilink notes-wikilink-missing'}
      onClick={() => (page ? onOpenPage(page.id) : onCreatePage(target))}
    >
      {label}
    </NodeViewWrapper>
  )
}

const ALIGNMENTS: { value: ImageAlign | null; icon: React.ReactNode; labelKey: string }[] = [
  { value: null, icon: <PicCenterOutlined />, labelKey: 'notes.visual.alignInline' },
  { value: 'left', icon: <AlignLeftOutlined />, labelKey: 'notes.visual.alignLeft' },
  { value: 'center', icon: <AlignCenterOutlined />, labelKey: 'notes.visual.alignCenter' },
  { value: 'right', icon: <AlignRightOutlined />, labelKey: 'notes.visual.alignRight' },
]

export function ImageView({ node, selected, updateAttributes }: NodeViewProps) {
  const { t } = useTranslation()
  const src = String(node.attrs.src)
  // A library image comes through the authenticated download; any other is the browser's to fetch.
  const urls = useMediaPathUrls(isMediaUrl(src) ? [src] : [])
  const shown = isMediaUrl(src) ? urls.get(src) : src
  const { text, width, align, caption } = parseImageAlt(String(node.attrs.alt ?? ''))

  return (
    <NodeViewWrapper as="span" className={align ? `notes-figure notes-figure-${align}` : 'notes-figure'}>
      {shown ? (
        <img src={shown} alt={text} width={width ?? undefined} />
      ) : (
        <img data-media-src={src} alt={text} width={width ?? undefined} />
      )}
      {caption && <span className="notes-caption">{caption}</span>}
      {selected && (
        <span className="notes-image-tools" contentEditable={false}>
          {ALIGNMENTS.map((option) => (
            <button
              key={option.labelKey}
              type="button"
              title={t(option.labelKey)}
              aria-label={t(option.labelKey)}
              aria-pressed={align === option.value}
              // Down, not click: a click would first move the selection off the image.
              onMouseDown={(event) => {
                event.preventDefault()
                updateAttributes({ alt: formatImageAlt(text, width, option.value) })
              }}
            >
              {option.icon}
            </button>
          ))}
        </span>
      )}
    </NodeViewWrapper>
  )
}

/** A library file, as the preview's card: glyph, name and size. A click saves it, as in reading mode. */
export function MediaFileView({ node }: NodeViewProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const href = String(node.attrs.href)
  const label = String(node.attrs.label)
  const id = mediaIdOf(href)
  const item = useMediaLookup(id ? [id] : []).get(id ?? '')
  const meta = item ? [formatBytes(item.sizeBytes), item.isDeleted ? t('notes.media.removed') : null].filter(Boolean).join(' · ') : ''

  return (
    <NodeViewWrapper
      as="span"
      className="notes-file-card"
      onClick={() =>
        void downloadMedia(href, mediaFileName(href, item?.originalFileName ?? label)).catch(() => message.error(t('notes.downloadError')))
      }
    >
      <span className="notes-file-glyph">{fileGlyph(item?.contentType ?? '')}</span>
      <span className="notes-file-body">
        <span className="notes-file-name">{label}</span>
        <span className="notes-file-meta">{meta}</span>
      </span>
    </NodeViewWrapper>
  )
}

/** Inline HTML, drawn through the preview's sanitizer; its source is edited in Markdown mode. */
export function InlineHtmlView({ node }: NodeViewProps) {
  return (
    <NodeViewWrapper as="span" className="notes-inline-html" dangerouslySetInnerHTML={{ __html: sanitizeNoteHtml(String(node.attrs.source)) }} />
  )
}

/**
 * Unsupported HTML: drawn by the preview, and edited as the markdown it is. Its
 * source opens on a double click or the corner button, and goes back in on blur, Esc or Ctrl+Enter.
 */
export function RawBlockView({ node, updateAttributes, deleteNode, editor }: NodeViewProps) {
  const { t } = useTranslation()
  const context = useVisualEditorContext()
  const source = String(node.attrs.source)
  const [editing, setEditing] = useState<string | null>(null)

  function commit() {
    if (editing === null) return
    const value = editing.replace(/\s+$/, '')
    setEditing(null)
    if (value.trim().length === 0) deleteNode()
    else updateAttributes({ source: value })
    editor.commands.focus()
  }

  return (
    <NodeViewWrapper className="notes-raw-block">
      {editing !== null ? (
        <textarea
          autoFocus
          className="notes-raw-source"
          value={editing}
          spellCheck={false}
          rows={Math.max(2, editing.split('\n').length)}
          onChange={(event) => setEditing(event.target.value)}
          onBlur={commit}
          onKeyDown={(event) => {
            if (event.key === 'Escape' || (event.key === 'Enter' && (event.ctrlKey || event.metaKey))) {
              event.preventDefault()
              commit()
            }
          }}
        />
      ) : (
        <>
          <div onDoubleClick={() => setEditing(source)}>
            <MarkdownPreview markdown={source} {...context} />
          </div>
          <button
            type="button"
            className="notes-raw-edit"
            title={t('notes.visual.editSource')}
            aria-label={t('notes.visual.editSource')}
            onClick={() => setEditing(source)}
          >
            <CodeOutlined />
          </button>
        </>
      )}
    </NodeViewWrapper>
  )
}
