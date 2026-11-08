import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Image, Input, Spin } from 'antd'
import { CloseOutlined, FolderOpenOutlined, PlusOutlined } from '@ant-design/icons'
import { useLinkPreview } from '../../hooks/useLinkPreview'
import { useMediaPathUrls } from '../../hooks/useMediaUrls'
import { noteKeys } from '../../hooks/queryKeys'
import { embedFrame, frameDocument, lockFrame, mediaFileKind, WRITTEN_FRAME_SANDBOX } from '../../lib/embeds'
import { mediaPath } from '../../lib/media'
import { getLinkPreview } from '../../services/links.service'
import { fetchMediaBlob } from '../../services/media.service'
import type { LinkPreviewDto } from '../../models'
import { useVisualEditorContext } from './visualContext'
import { AiBlock } from './AiBlock'
import { AgendaBlock } from './AgendaBlock'
import { FinanceBlock } from './FinanceBlock'
import { BreadcrumbBlock, MathBlock, MermaidBlock, PropertiesBlock, SubpagesBlock, SyncedCopy, TagPagesBlock } from './AdvancedBlocks'

// The media blocks: a bookmark card, an embed of another site, a video, an audio player, a PDF
// reader and an image gallery. One node draws them all, from the block's kind and props.

type Props = Record<string, unknown>
type Update = (props: Props) => void

interface GalleryItem {
  mediaId: string
  caption?: string
}

const str = (value: unknown) => (typeof value === 'string' && value ? value : null)

const hostOf = (url: string) => {
  try {
    return new URL(url).hostname.replace(/^www\./, '')
  } catch {
    return url
  }
}

/** The empty block: a field for the link, and the library when the kind takes a library item. */
function Placeholder({ kind, editable, library, onUrl, onLibrary }: {
  kind: string
  editable: boolean
  library?: 'image' | 'file'
  onUrl?: (url: string) => void
  onLibrary?: () => void
}) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState('')
  if (!editable) return <div className="notes-rich-empty">{t(`notes.rich.empty.${kind}`)}</div>
  return (
    <div className="notes-rich-empty" contentEditable={false}>
      {onUrl && (
        <Input
          size="small"
          autoFocus
          value={draft}
          placeholder={t(`notes.rich.paste.${kind}`)}
          aria-label={t(`notes.rich.paste.${kind}`)}
          onChange={(event) => setDraft(event.target.value)}
          onPressEnter={() => draft.trim() && onUrl(draft.trim())}
        />
      )}
      {library && onLibrary && (
        <Button size="small" icon={<FolderOpenOutlined />} onClick={onLibrary}>
          {t('notes.rich.fromLibrary')}
        </Button>
      )}
    </div>
  )
}

/** A frame that tells us its height (a page we wrote), so it grows with what it holds. */
function WrittenFrame({ html, title }: { html: string; title: string }) {
  const ref = useRef<HTMLIFrameElement>(null)
  const [height, setHeight] = useState(240)
  useEffect(() => {
    const listen = (event: MessageEvent) => {
      if (event.source !== ref.current?.contentWindow) return
      const next = Number((event.data as { pandoraFrameHeight?: unknown } | null)?.pandoraFrameHeight)
      if (Number.isFinite(next) && next > 0) setHeight(Math.min(Math.max(next, 80), 2000))
    }
    window.addEventListener('message', listen)
    return () => window.removeEventListener('message', listen)
  }, [])
  return <iframe ref={ref} className="notes-rich-frame" title={title} srcDoc={frameDocument(html)} sandbox={WRITTEN_FRAME_SANDBOX} style={{ height }} loading="lazy" />
}

/** A frame of another site, locked down the way every frame of a note is. */
function SiteFrame({ src, title, height }: { src: string; title: string; height?: number }) {
  return (
    <iframe
      ref={(frame) => {
        if (frame) lockFrame(frame)
      }}
      className={height ? 'notes-rich-frame' : 'notes-rich-frame notes-rich-video'}
      title={title}
      src={src}
      style={height ? { height } : undefined}
    />
  )
}

/** A link as a card: its image, title, description and site. */
function LinkCard({ url, preview }: { url: string; preview: Partial<LinkPreviewDto> | null }) {
  const title = preview?.title || hostOf(url)
  return (
    <a className="notes-bookmark" href={url} target="_blank" rel="noopener noreferrer" contentEditable={false}>
      <span className="notes-bookmark-body">
        <span className="notes-bookmark-title">{title}</span>
        {preview?.description && <span className="notes-bookmark-description">{preview.description}</span>}
        <span className="notes-bookmark-site">
          {preview?.iconUrl && <img src={preview.iconUrl} alt="" width={14} height={14} referrerPolicy="no-referrer" onError={(event) => (event.currentTarget.style.display = 'none')} />}
          {preview?.siteName || hostOf(url)}
        </span>
      </span>
      {preview?.imageUrl && <img className="notes-bookmark-image" src={preview.imageUrl} alt="" referrerPolicy="no-referrer" onError={(event) => (event.currentTarget.style.display = 'none')} />}
    </a>
  )
}

/** What a link draws as: a known site's frame, a file the browser plays, the site's oEmbed, or a card. */
function LinkEmbed({ url }: { url: string }) {
  const { t } = useTranslation()
  const frame = embedFrame(url)
  const file = mediaFileKind(url)
  // Only a site off the list is asked about; the card is what is left when it has no embed.
  const { data, isLoading, isError } = useLinkPreview(frame || file ? null : url)

  if (frame?.src) return <SiteFrame src={frame.src} title={frame.provider} height={frame.height} />
  if (frame?.html) return <WrittenFrame html={frame.html} title={frame.provider} />
  if (file === 'video') return <video className="notes-rich-video" src={url} controls preload="metadata" />
  if (file === 'audio') return <audio className="notes-rich-audio" src={url} controls preload="metadata" />
  if (isLoading) return <Spin size="small" className="notes-rich-loading" aria-label={t('notes.rich.loading')} />
  const embed = isError ? null : data?.embed
  if (embed?.html && (embed.type === 'video' || embed.type === 'rich')) return <WrittenFrame html={embed.html} title={data?.siteName ?? hostOf(url)} />
  if (embed?.type === 'photo' && embed.url) return <img className="notes-rich-photo" src={embed.url} alt={data?.title ?? ''} referrerPolicy="no-referrer" />
  return <LinkCard url={url} preview={data ?? null} />
}

/** A library item as an address of our own, typed as the block says: a PDF stays a PDF, whatever it was uploaded as. */
function useTypedMediaUrl(mediaId: string | null, type: string) {
  return useQuery({
    queryKey: [...noteKeys.mediaBlob(mediaId ? mediaPath(mediaId) : ''), type],
    queryFn: async () => URL.createObjectURL(new Blob([await fetchMediaBlob(mediaPath(mediaId!))], { type })),
    enabled: !!mediaId,
    staleTime: Infinity,
    gcTime: Infinity,
    retry: false,
  }).data
}

function Bookmark({ props, editable, update }: { props: Props; editable: boolean; update: Update }) {
  const queryClient = useQueryClient()
  const url = str(props.url)
  async function save(link: string) {
    update({ url: link })
    try {
      // Kept in the block: the card reads the same tomorrow, and opening the page asks no one.
      const preview = await queryClient.fetchQuery({ queryKey: noteKeys.linkPreview(link), queryFn: () => getLinkPreview(link), staleTime: Infinity, retry: false })
      update({ url: link, title: preview.title, description: preview.description, siteName: preview.siteName, imageUrl: preview.imageUrl, iconUrl: preview.iconUrl })
    } catch {
      // A page that cannot be read is a card with its address.
    }
  }
  if (!url) return <Placeholder kind="bookmark" editable={editable} onUrl={(link) => void save(link)} />
  return <LinkCard url={url} preview={props as Partial<LinkPreviewDto>} />
}

function Audio({ props, editable, update, pick }: { props: Props; editable: boolean; update: Update; pick: () => void }) {
  const mediaId = str(props.mediaId)
  const urls = useMediaPathUrls(mediaId ? [mediaPath(mediaId)] : [])
  const src = mediaId ? urls.get(mediaPath(mediaId)) : str(props.url)
  if (!mediaId && !str(props.url)) return <Placeholder kind="audio" editable={editable} library="file" onUrl={(url) => update({ url })} onLibrary={pick} />
  return (
    <div className="notes-rich-audio-box">
      {str(props.name) && <span className="notes-rich-caption">{str(props.name)}</span>}
      {src ? <audio className="notes-rich-audio" src={src} controls preload="metadata" /> : <Spin size="small" />}
    </div>
  )
}

function Pdf({ props, editable, update, pick }: { props: Props; editable: boolean; update: Update; pick: () => void }) {
  const { t } = useTranslation()
  const mediaId = str(props.mediaId)
  const local = useTypedMediaUrl(mediaId, 'application/pdf')
  const src = mediaId ? local : str(props.url)
  if (!mediaId && !str(props.url)) return <Placeholder kind="pdf" editable={editable} library="file" onUrl={(url) => update({ url })} onLibrary={pick} />
  return src ? <iframe className="notes-rich-pdf" src={src} title={str(props.name) ?? t('notes.rich.kinds.pdf')} /> : <Spin size="small" />
}

function Gallery({ props, editable, update, pick }: { props: Props; editable: boolean; update: Update; pick: () => Promise<string | null> }) {
  const { t } = useTranslation()
  const items = ((props.items as GalleryItem[] | undefined) ?? []).filter((item) => item?.mediaId)
  const paths = items.map((item) => mediaPath(item.mediaId))
  const urls = useMediaPathUrls(paths)

  async function add() {
    const mediaId = await pick()
    if (mediaId) update({ ...props, items: [...items, { mediaId }] })
  }

  return (
    <div className="notes-gallery" contentEditable={false}>
      <Image.PreviewGroup>
        {items.map((item, index) => (
          <div key={`${item.mediaId}-${index}`} className="notes-gallery-item">
            {urls.get(paths[index]) ? <Image src={urls.get(paths[index])} alt={item.caption ?? ''} /> : <span className="notes-image-loading" />}
            {editable && (
              <button
                type="button"
                className="notes-gallery-remove"
                aria-label={t('notes.rich.removeImage')}
                onClick={() => update({ ...props, items: items.filter((_, other) => other !== index) })}
              >
                <CloseOutlined />
              </button>
            )}
          </div>
        ))}
      </Image.PreviewGroup>
      {editable && (
        <button type="button" className="notes-gallery-add" onClick={() => void add()}>
          <PlusOutlined /> {t('notes.rich.addImage')}
        </button>
      )}
      {!editable && items.length === 0 && <div className="notes-rich-empty">{t('notes.rich.empty.gallery')}</div>}
    </div>
  )
}

export function RichBlockView({ node, editor, selected, updateAttributes, getPos }: NodeViewProps) {
  const { pickMedia } = useVisualEditorContext()
  const kind = String(node.attrs.kind)
  const props = (node.attrs.props ?? {}) as Props
  const editable = editor.isEditable
  const update: Update = (next) => updateAttributes({ props: Object.fromEntries(Object.entries(next).filter(([, value]) => value != null && value !== '')) })

  // Audio and PDF come from the library as files; a pick of the wrong type is still played or read.
  const pickFile = () =>
    void pickMedia('file').then((item) => item && update({ mediaId: item.id, name: item.name }))
  const pickImage = async () => (await pickMedia('image'))?.id ?? null

  const url = str(props.url)
  const body =
    kind === 'bookmark' ? <Bookmark props={props} editable={editable} update={update} />
      : kind === 'audio' ? <Audio props={props} editable={editable} update={update} pick={pickFile} />
      : kind === 'pdf' ? <Pdf props={props} editable={editable} update={update} pick={pickFile} />
      : kind === 'gallery' ? <Gallery props={props} editable={editable} update={update} pick={pickImage} />
      : kind === 'mermaid' ? <MermaidBlock props={props} editable={editable} selected={selected} update={update} />
      : kind === 'math' ? <MathBlock props={props} editable={editable} selected={selected} update={update} />
      : kind === 'synced-ref' ? <SyncedCopy props={props} />
      : kind === 'breadcrumb' ? <BreadcrumbBlock />
      : kind === 'subpages' ? <SubpagesBlock />
      : kind === 'tag-pages' ? <TagPagesBlock props={props} editable={editable} update={update} />
      : kind === 'properties' ? <PropertiesBlock props={props} editable={editable} update={update} />
      : kind === 'ai' ? <AiBlock node={node} editor={editor} getPos={getPos} updateAttributes={updateAttributes} />
      : kind === 'agenda-list' ? <AgendaBlock props={props} editable={editable} update={update} />
      : kind === 'finance-summary' ? <FinanceBlock props={props} editable={editable} update={update} />
      : url ? <LinkEmbed url={url} />
      : <Placeholder kind={kind} editable={editable} onUrl={(link) => update({ url: link })} />

  return <NodeViewWrapper className={`notes-rich notes-rich-${kind}`}>{body}</NodeViewWrapper>
}
