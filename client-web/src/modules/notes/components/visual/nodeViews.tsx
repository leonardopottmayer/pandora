import { useTranslation } from 'react-i18next'
import { NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import { AlignCenterOutlined, AlignLeftOutlined, AlignRightOutlined, PicCenterOutlined } from '@ant-design/icons'
import { App } from 'antd'
import { formatBytes } from '@/lib/formatBytes'
import { useMediaLookup } from '../../hooks/useMedia'
import { useMediaPathUrls } from '../../hooks/useMediaUrls'
import { usePage } from '../../hooks/usePages'
import { plainText } from '../../lib/blocks'
import { timeAgo } from '../../lib/timeAgo'
import { fileGlyph, mediaFileName, mediaPath } from '../../lib/media'
import { downloadMedia } from '../../services/media.service'
import { useVisualEditorContext } from './visualContext'

// How our own nodes draw inside the editor.

/** A page named in the text: its icon and current title in a box, like a Confluence page link. */
export function MentionView({ node }: NodeViewProps) {
  const { t } = useTranslation()
  const { pagesById, onOpenPage, publicView } = useVisualEditorContext()
  const page = pagesById.get(String(node.attrs.pageId))

  return (
    <NodeViewWrapper
      as="span"
      className={page ? 'notes-mention' : 'notes-mention notes-mention-missing'}
      title={page ? page.title : t(publicView ? 'notes.public.privatePage' : 'notes.visual.missingPage')}
      onClick={() => page && onOpenPage(page.id)}
    >
      <span className="notes-mention-icon" aria-hidden="true">{page?.icon ?? '📄'}</span>
      {page ? page.title || t('notes.untitled') : t(publicView ? 'notes.public.privatePage' : 'notes.visual.missingPage')}
    </NodeViewWrapper>
  )
}

type ImageAlign = 'left' | 'center' | 'right'

const ALIGNMENTS: { value: ImageAlign | null; icon: React.ReactNode; labelKey: string }[] = [
  { value: null, icon: <PicCenterOutlined />, labelKey: 'notes.visual.alignInline' },
  { value: 'left', icon: <AlignLeftOutlined />, labelKey: 'notes.visual.alignLeft' },
  { value: 'center', icon: <AlignCenterOutlined />, labelKey: 'notes.visual.alignCenter' },
  { value: 'right', icon: <AlignRightOutlined />, labelKey: 'notes.visual.alignRight' },
]

export function ImageView({ node, selected, editor, updateAttributes }: NodeViewProps) {
  const { t } = useTranslation()
  const mediaId = node.attrs.mediaId as string | null
  // A library image comes through the authenticated download; any other is the browser's to fetch.
  const path = mediaId ? mediaPath(mediaId) : null
  const urls = useMediaPathUrls(path ? [path] : [])
  const shown = path ? urls.get(path) : (node.attrs.src as string | null)
  const align = node.attrs.align as ImageAlign | null
  const width = node.attrs.width as number | null
  const caption = String(node.attrs.caption ?? '')

  return (
    <NodeViewWrapper className={align ? `notes-figure notes-figure-${align}` : 'notes-figure'}>
      {shown ? <img src={shown} alt={caption} width={width ?? undefined} /> : <span className="notes-image-loading" />}
      {(caption || (selected && editor.isEditable)) && (
        <input
          className="notes-caption"
          value={caption}
          placeholder={t('notes.visual.caption')}
          aria-label={t('notes.visual.caption')}
          readOnly={!editor.isEditable}
          onChange={(event) => updateAttributes({ caption: event.target.value })}
        />
      )}
      {selected && editor.isEditable && (
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
                updateAttributes({ align: option.value })
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

/** A library file as a card: glyph, name and size. A click saves it. */
export function MediaFileView({ node }: NodeViewProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const id = node.attrs.mediaId as string | null
  const name = String(node.attrs.name ?? '')
  const item = useMediaLookup(id ? [id] : []).get(id ?? '')
  const meta = item ? [formatBytes(item.sizeBytes), item.isDeleted ? t('notes.media.removed') : null].filter(Boolean).join(' · ') : ''

  return (
    <NodeViewWrapper
      className="notes-file-card"
      onClick={() => {
        if (!id) return
        const path = mediaPath(id)
        void downloadMedia(path, mediaFileName(path, item?.originalFileName ?? name)).catch(() => message.error(t('notes.downloadError')))
      }}
    >
      <span className="notes-file-glyph">{fileGlyph(item?.contentType ?? '')}</span>
      <span className="notes-file-body">
        <span className="notes-file-name">{name}</span>
        <span className="notes-file-meta">{meta}</span>
      </span>
    </NodeViewWrapper>
  )
}

/** A block of a type this version does not draw: kept and saved back as it came. */
export function UnknownBlockView({ node }: NodeViewProps) {
  const { t } = useTranslation()
  const type = String((node.attrs.block as { type?: string } | null)?.type ?? '?')
  return <NodeViewWrapper className="notes-unknown-block">{t('notes.visual.unknownBlock', { type })}</NodeViewWrapper>
}

export type PageLinkLook = 'inline' | 'card' | 'preview'

const PAGE_LINK_LOOKS: PageLinkLook[] = ['inline', 'card', 'preview']

/** The lines a preview shows: the page's first blocks with text, tags left out. */
function previewLines(blocks: { type: string; content?: Parameters<typeof plainText>[0] }[]): string[] {
  return blocks
    .filter((block) => block.type !== 'tags')
    .map((block) => plainText(block.content).trim())
    .filter(Boolean)
    .slice(0, 4)
}

/** Another page as a block: a card with its icon, title and last edit, or a preview with its first lines. */
export function PageLinkView({ node, selected, editor, getPos, updateAttributes }: NodeViewProps) {
  const { t, i18n } = useTranslation()
  const { pagesById, onOpenPage, publicView } = useVisualEditorContext()
  const pageId = String(node.attrs.pageId)
  const look = node.attrs.look === 'preview' ? 'preview' : 'card'
  const page = pagesById.get(pageId)
  // Only a preview reads the page itself; a card has all it needs in the page list.
  const { data: full } = usePage(look === 'preview' && page ? pageId : null)
  const lines = full ? previewLines(full.content.blocks) : []

  function changeLook(next: PageLinkLook) {
    if (next !== 'inline') {
      updateAttributes({ look: next })
      return
    }
    const pos = getPos()
    if (typeof pos !== 'number') return
    editor
      .chain()
      .focus()
      .insertContentAt({ from: pos, to: pos + node.nodeSize }, { type: 'paragraph', content: [{ type: 'mention', attrs: { pageId } }] })
      .run()
  }

  return (
    <NodeViewWrapper className={`notes-page-link notes-page-link-${look}`}>
      <div
        role="link"
        tabIndex={0}
        className={page ? 'notes-page-link-card' : 'notes-page-link-card notes-mention-missing'}
        onClick={() => page && onOpenPage(page.id)}
        onKeyDown={(event) => event.key === 'Enter' && page && onOpenPage(page.id)}
      >
        <span className="notes-page-link-icon" aria-hidden="true">{page?.icon ?? '📄'}</span>
        <span className="notes-page-link-body">
          <span className="notes-page-link-title">{page ? page.title || t('notes.untitled') : t(publicView ? 'notes.public.privatePage' : 'notes.visual.missingPage')}</span>
          {page && (
            <span className="notes-page-link-meta">{t('notes.edited', { when: timeAgo(page.updatedAt ?? page.createdAt, i18n.language) })}</span>
          )}
          {look === 'preview' && lines.length > 0 && (
            <span className="notes-page-link-preview">
              {lines.map((line, index) => (
                <span key={index}>{line}</span>
              ))}
            </span>
          )}
        </span>
      </div>
      {selected && editor.isEditable && (
        <span className="notes-image-tools" contentEditable={false}>
          {PAGE_LINK_LOOKS.map((option) => (
            <button
              key={option}
              type="button"
              aria-pressed={look === option}
              onMouseDown={(event) => {
                event.preventDefault()
                changeLook(option)
              }}
            >
              {t(`notes.visual.look.${option}`)}
            </button>
          ))}
        </span>
      )}
    </NodeViewWrapper>
  )
}
